using Platform.Shared.Common;
using Platform.Shared.Entities.Common;

namespace Platform.Shared.Entities.Inventory;

/// <summary>
/// Materialised cache of the ledger: how much of one SKU is in one warehouse
/// (P2). Written only in the same transaction as the ledger entry that
/// justifies it, and always rebuildable from the ledger. If the two disagree,
/// the ledger is right.
/// </summary>
/// <remarks>
/// Its id is derived from warehouse and SKU (<see cref="IdFor"/>), so it is
/// read and written by key inside a transaction, without a query.
/// </remarks>
public class StockBalance : BaseEntity, IOrgScoped, INotAudited
{
    /// <inheritdoc />
    public Guid OrgId { get; set; }

    /// <summary>The warehouse.</summary>
    public Guid WarehouseId { get; set; }

    /// <summary>The SKU.</summary>
    public Guid SkuId { get; set; }

    /// <summary>SKU code, copied for sorting and searching the stock list.</summary>
    public string SkuCode { get; set; } = string.Empty;

    /// <summary>"Product · variant", copied for display.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Physically in the warehouse, in <see cref="Uom"/>.</summary>
    public decimal OnHand { get; set; }

    /// <summary>Promised to orders but not dispatched (P4); zero until Phase 3.</summary>
    public decimal Reserved { get; set; }

    /// <summary>The SKU's base unit.</summary>
    public string Uom { get; set; } = string.Empty;

    /// <summary>True while anything is on hand; lets the stock list hide empty rows in the query.</summary>
    public bool IsInStock { get; set; }

    /// <summary>When the last movement happened.</summary>
    public DateTime? LastMovedAt { get; set; }

    /// <summary>Free to promise: on hand minus reserved.</summary>
    /// <returns>Available quantity in <see cref="Uom"/>.</returns>
    public decimal Available() => OnHand - Reserved;

    /// <summary>
    /// The id of the balance for one SKU in one warehouse.
    /// </summary>
    /// <param name="warehouseId">Warehouse.</param>
    /// <param name="skuId">SKU.</param>
    /// <returns>The derived id.</returns>
    public static Guid IdFor(Guid warehouseId, Guid skuId) =>
        IdGenerator.FromName($"stock_balance:{warehouseId:N}:{skuId:N}");
}
