using Google.Cloud.Firestore;
using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Mapping;
using Platform.Api.Repositories;
using Platform.Api.Security;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Sales;
using Platform.Shared.Entities.Common;
using Platform.Shared.Entities.Pricing;
using Platform.Shared.Entities.Sales;

namespace Platform.Api.Services.Sales;

/// <summary>
/// Customer accounts as staff see them. Customers open their own accounts on
/// the store; staff set the price tier and business details, or block an
/// account (which also stops its sign-in).
/// </summary>
public interface ICustomerService
{
    /// <summary>
    /// Lists customers, newest first, or by email prefix.
    /// </summary>
    /// <param name="request">Search and page.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One page of customers.</returns>
    Task<PagedResult<CustomerDto>> ListAsync(PagedRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads one customer.
    /// </summary>
    /// <param name="id">Customer id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The customer.</returns>
    Task<CustomerDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes a customer.
    /// </summary>
    /// <param name="id">Customer id.</param>
    /// <param name="request">New values.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The customer.</returns>
    Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Firestore-backed <see cref="ICustomerService"/>.
/// </summary>
public sealed class CustomerService : ICustomerService
{
    private static readonly string CreatedAtField = FirestoreNaming.Field(nameof(BaseEntity.CreatedAt));
    private static readonly string EmailField = FirestoreNaming.Field(nameof(Customer.Email));

    private readonly IRepository<Customer> _customers;
    private readonly IRepository<PriceList> _lists;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="customers">Customer data access.</param>
    /// <param name="lists">Price list data access, for tiers.</param>
    /// <param name="identityProvider">Blocks or restores sign-in.</param>
    /// <param name="unitOfWork">Commits changes.</param>
    public CustomerService(IRepository<Customer> customers, IRepository<PriceList> lists, IIdentityProvider identityProvider, IUnitOfWork unitOfWork)
    {
        _customers = customers;
        _lists = lists;
        _identityProvider = identityProvider;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<PagedResult<CustomerDto>> ListAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        Query query = string.IsNullOrWhiteSpace(request.Search)
            ? _customers.Query().OrderByDescending(CreatedAtField)
            : _customers.Query().WhereStartsWith(EmailField, request.Search.Trim().ToLowerInvariant());
        PagedResult<Customer> page = await _customers.GetPagedAsync(query, request, cancellationToken);
        var tiers = (await _lists.GetByIdsAsync(page.Items.Where(c => c.TierPriceListId.HasValue).Select(c => c.TierPriceListId!.Value), cancellationToken))
            .ToDictionary(l => l.Id, l => l.Name);
        return new PagedResult<CustomerDto>
        {
            Items = page.Items.Select(c => ToDto(c, tiers)).ToList(),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
        };
    }

    /// <inheritdoc />
    public async Task<CustomerDto> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await ToDtoAsync(await LoadAsync(id, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public async Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        Customer customer = await LoadAsync(id, cancellationToken);
        if (request.TierPriceListId is { } tierId
            && await _lists.GetByIdAsync(tierId, cancellationToken) is not { Type: PriceListType.Tier, IsActive: true })
        {
            throw new FieldValidationException(nameof(UpdateCustomerRequest.TierPriceListId), "Choose an active tier price list.");
        }

        bool wasActive = customer.IsActive;
        customer.Name = DtoMapping.Clean(request.Name);
        customer.Phone = DtoMapping.CleanOptional(request.Phone);
        customer.CompanyName = DtoMapping.CleanOptional(request.CompanyName);
        customer.Gstin = DtoMapping.CleanOptional(request.Gstin)?.ToUpperInvariant();
        customer.TierPriceListId = request.TierPriceListId;
        customer.IsActive = request.IsActive;

        if (wasActive != customer.IsActive && !string.IsNullOrEmpty(customer.AuthUid))
        {
            await _identityProvider.SetDisabledAsync(customer.AuthUid, disabled: !customer.IsActive, cancellationToken);
        }

        _customers.Update(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(customer, cancellationToken);
    }

    /// <summary>
    /// Loads a customer or fails with 404.
    /// </summary>
    /// <param name="id">Customer id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The customer.</returns>
    private async Task<Customer> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await _customers.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Customer");

    /// <summary>
    /// Maps one customer with its tier name.
    /// </summary>
    /// <param name="customer">Customer.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The DTO.</returns>
    private async Task<CustomerDto> ToDtoAsync(Customer customer, CancellationToken cancellationToken)
    {
        var tiers = new Dictionary<Guid, string>();
        if (customer.TierPriceListId is { } tierId && await _lists.GetByIdAsync(tierId, cancellationToken) is { } tier)
        {
            tiers[tier.Id] = tier.Name;
        }

        return ToDto(customer, tiers);
    }

    /// <summary>
    /// Maps a customer.
    /// </summary>
    /// <param name="customer">Customer.</param>
    /// <param name="tiers">Tier list names by id.</param>
    /// <returns>The DTO.</returns>
    private static CustomerDto ToDto(Customer customer, IReadOnlyDictionary<Guid, string> tiers) => new CustomerDto
    {
        Name = customer.Name,
        Email = customer.Email,
        Phone = customer.Phone,
        CompanyName = customer.CompanyName,
        Gstin = customer.Gstin,
        TierPriceListId = customer.TierPriceListId,
        TierPriceListName = customer.TierPriceListId is { } id ? tiers.GetValueOrDefault(id) : null,
        Address = customer.Address is null ? null : DtoMapping.ToAddressDto(customer.Address),
        IsActive = customer.IsActive,
    }.WithAuditFrom(customer);
}
