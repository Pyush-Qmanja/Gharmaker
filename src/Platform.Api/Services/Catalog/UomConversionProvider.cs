using Platform.Api.Repositories;
using Platform.Shared.Entities.Catalog;
using Platform.Shared.Units;

namespace Platform.Api.Services.Catalog;

/// <summary>
/// Supplies the <see cref="IUomConversionService"/> for the caller's
/// organisation, built from its units. Every API conversion goes through this (P7).
/// </summary>
public interface IUomConversionProvider
{
    /// <summary>
    /// Returns the conversion service for the current organisation, loading its units once per request.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The conversion service.</returns>
    Task<IUomConversionService> GetAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IUomConversionProvider"/>. Scoped: one unit load per request.
/// </summary>
public sealed class UomConversionProvider : IUomConversionProvider
{
    private readonly IRepository<Uom> _uoms;
    private IUomConversionService? _service;

    /// <summary>
    /// Creates the provider.
    /// </summary>
    /// <param name="uoms">Unit data access (org-scoped).</param>
    public UomConversionProvider(IRepository<Uom> uoms)
    {
        _uoms = uoms;
    }

    /// <inheritdoc />
    public async Task<IUomConversionService> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_service is null)
        {
            IReadOnlyList<Uom> units = await _uoms.ListAsync(_uoms.Query(), cancellationToken);
            _service = new UomConversionService(units
                .Where(u => u.IsActive)
                .Select(u => new UnitDefinition(u.Code, u.Dimension, u.BaseFactor)));
        }

        return _service;
    }
}
