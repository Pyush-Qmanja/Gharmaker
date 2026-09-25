using Platform.Api.Firestore;
using Platform.Api.Repositories;
using Platform.Shared.Commerce;
using Platform.Shared.Entities.Inventory;

namespace Platform.Api.Services.Inventory;

/// <summary>
/// A warehouse that delivers to a PIN code, and how fast.
/// </summary>
/// <param name="WarehouseId">Warehouse.</param>
/// <param name="LeadTimeDays">Days to deliver.</param>
public sealed record ServingWarehouse(Guid WarehouseId, int LeadTimeDays);

/// <summary>
/// Available to promise (blueprint, "Key calculations"): the free stock of a
/// SKU across every active warehouse that delivers to a PIN code. Internal
/// only — the storefront turns it into one number and a date range (P1).
/// </summary>
public interface IAvailabilityService
{
    /// <summary>
    /// Lists the active warehouses delivering to a PIN code.
    /// </summary>
    /// <param name="pincode">Customer's PIN code.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The warehouses and their lead times.</returns>
    Task<IReadOnlyList<ServingWarehouse>> ServingAsync(string pincode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the free stock of each SKU in each serving warehouse.
    /// </summary>
    /// <param name="skuIds">SKUs.</param>
    /// <param name="serving">Warehouses delivering to the PIN code.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Stock by SKU id; a SKU nobody holds has an empty list.</returns>
    Task<IReadOnlyDictionary<Guid, List<WarehouseStock>>> GetStockAsync(
        IReadOnlyCollection<Guid> skuIds, IReadOnlyList<ServingWarehouse> serving, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the free stock of each SKU from balances already read (e.g. inside a transaction).
    /// </summary>
    /// <param name="skuIds">SKUs.</param>
    /// <param name="serving">Warehouses delivering to the PIN code.</param>
    /// <param name="balances">Balances by id; missing ones hold nothing.</param>
    /// <returns>Stock by SKU id.</returns>
    IReadOnlyDictionary<Guid, List<WarehouseStock>> FromBalances(
        IReadOnlyCollection<Guid> skuIds, IReadOnlyList<ServingWarehouse> serving, IReadOnlyDictionary<Guid, StockBalance> balances);
}

/// <summary>
/// Firestore-backed <see cref="IAvailabilityService"/>. Balance ids are derived
/// from warehouse and SKU, so stock is read by key, not by query.
/// </summary>
public sealed class AvailabilityService : IAvailabilityService
{
    private static readonly string PincodeField = FirestoreNaming.Field(nameof(DeliveryArea.Pincode));
    private static readonly string IsActiveField = FirestoreNaming.Field(nameof(DeliveryArea.IsActive));

    private readonly IRepository<DeliveryArea> _areas;
    private readonly IRepository<Warehouse> _warehouses;
    private readonly IRepository<StockBalance> _balances;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="areas">Delivery area data access.</param>
    /// <param name="warehouses">Warehouse data access.</param>
    /// <param name="balances">Stock balance data access.</param>
    public AvailabilityService(IRepository<DeliveryArea> areas, IRepository<Warehouse> warehouses, IRepository<StockBalance> balances)
    {
        _areas = areas;
        _warehouses = warehouses;
        _balances = balances;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ServingWarehouse>> ServingAsync(string pincode, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DeliveryArea> areas = await _areas.ListAsync(
            _areas.Query().WhereEqualTo(PincodeField, pincode).WhereEqualTo(IsActiveField, true),
            cancellationToken);
        var active = (await _warehouses.GetByIdsAsync(areas.Select(a => a.WarehouseId), cancellationToken))
            .Where(w => w.IsActive)
            .Select(w => w.Id)
            .ToHashSet();
        return areas.Where(a => active.Contains(a.WarehouseId))
            .Select(a => new ServingWarehouse(a.WarehouseId, a.LeadTimeDays))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, List<WarehouseStock>>> GetStockAsync(
        IReadOnlyCollection<Guid> skuIds, IReadOnlyList<ServingWarehouse> serving, CancellationToken cancellationToken = default)
    {
        var ids = serving.SelectMany(w => skuIds.Select(sku => StockBalance.IdFor(w.WarehouseId, sku)));
        var balances = (await _balances.GetByIdsAsync(ids, cancellationToken)).ToDictionary(b => b.Id);
        return FromBalances(skuIds, serving, balances);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<Guid, List<WarehouseStock>> FromBalances(
        IReadOnlyCollection<Guid> skuIds, IReadOnlyList<ServingWarehouse> serving, IReadOnlyDictionary<Guid, StockBalance> balances) =>
        skuIds.Distinct().ToDictionary(
            sku => sku,
            sku => serving
                .Select(w => new WarehouseStock(
                    w.WarehouseId,
                    w.LeadTimeDays,
                    balances.TryGetValue(StockBalance.IdFor(w.WarehouseId, sku), out StockBalance? balance) ? Math.Max(0, balance.Available()) : 0))
                .ToList());
}
