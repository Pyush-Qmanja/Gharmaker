using Platform.Api.Common;
using Platform.Api.Common.Exceptions;
using Platform.Api.Mapping;
using Platform.Api.Repositories;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Identity;
using Platform.Shared.Entities.Identity;

namespace Platform.Api.Services.Identity;

/// <summary>
/// The business details GST needs: legal name, GSTIN and the one registered
/// address the business invoices from (decision 1). The state of that address
/// decides CGST + SGST or IGST for every order.
/// </summary>
public interface IBusinessSettingsService
{
    /// <summary>
    /// Reads the caller's organisation's business details.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The details.</returns>
    Task<BusinessSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the business details.
    /// </summary>
    /// <param name="request">New details.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The saved details.</returns>
    Task<BusinessSettingsDto> UpdateAsync(UpdateBusinessSettingsRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the seller's registered state, when the store can charge GST.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The state name, or null when the business details are not filled in.</returns>
    Task<string?> GetSellerStateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Firestore-backed <see cref="IBusinessSettingsService"/>. The organisation is
/// always the caller's own; there is no id to pass, so none can be guessed.
/// </summary>
public sealed class BusinessSettingsService : IBusinessSettingsService
{
    private readonly IRepository<Organisation> _organisations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private Organisation? _loaded;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="organisations">Organisation data access.</param>
    /// <param name="unitOfWork">Commits changes.</param>
    /// <param name="currentUser">Caller's organisation.</param>
    public BusinessSettingsService(IRepository<Organisation> organisations, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _organisations = organisations;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<BusinessSettingsDto> GetAsync(CancellationToken cancellationToken = default) =>
        ToDto(await LoadAsync(cancellationToken));

    /// <inheritdoc />
    public async Task<BusinessSettingsDto> UpdateAsync(UpdateBusinessSettingsRequest request, CancellationToken cancellationToken = default)
    {
        Organisation organisation = await LoadAsync(cancellationToken);
        organisation.LegalName = DtoMapping.Clean(request.LegalName);
        organisation.Gstin = DtoMapping.Clean(request.Gstin).ToUpperInvariant();
        organisation.Address = DtoMapping.ToAddress(request.Address);
        organisation.Address.State = IndianStates.Find(organisation.Address.State)?.Name ?? organisation.Address.State;
        _organisations.Update(organisation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(organisation);
    }

    /// <inheritdoc />
    public async Task<string?> GetSellerStateAsync(CancellationToken cancellationToken = default)
    {
        Organisation organisation = await LoadAsync(cancellationToken);
        return IsComplete(organisation) ? organisation.Address!.State : null;
    }

    /// <summary>
    /// Loads the caller's organisation once per request.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The organisation.</returns>
    private async Task<Organisation> LoadAsync(CancellationToken cancellationToken)
    {
        if (_loaded is not null)
        {
            return _loaded;
        }

        Guid orgId = _currentUser.OrgId ?? throw new NotFoundException("Organisation");
        return _loaded = await _organisations.GetByIdAsync(orgId, cancellationToken) ?? throw new NotFoundException("Organisation");
    }

    /// <summary>
    /// Checks whether GST can be charged: legal name, GSTIN and a recognised state.
    /// </summary>
    /// <param name="organisation">Organisation.</param>
    /// <returns>True when complete.</returns>
    private static bool IsComplete(Organisation organisation) =>
        !string.IsNullOrWhiteSpace(organisation.LegalName)
        && !string.IsNullOrWhiteSpace(organisation.Gstin)
        && IndianStates.IsKnown(organisation.Address?.State);

    /// <summary>
    /// Maps the organisation.
    /// </summary>
    /// <param name="organisation">Organisation.</param>
    /// <returns>The DTO.</returns>
    private static BusinessSettingsDto ToDto(Organisation organisation) => new()
    {
        Name = organisation.Name,
        LegalName = organisation.LegalName,
        Gstin = organisation.Gstin,
        Address = organisation.Address is null ? null : DtoMapping.ToAddressDto(organisation.Address),
        IsComplete = IsComplete(organisation),
    };
}
