using Platform.Shared.Common;
using Platform.Shared.Entities.Common;

namespace Platform.Shared.Entities.Inventory;

/// <summary>
/// Where a stock hold is in its life.
/// </summary>
public enum StockHoldStatus
{
    /// <summary>Holding stock: counted in the balance's <c>Reserved</c>.</summary>
    Active = 0,

    /// <summary>Given back because the order was cancelled.</summary>
    Released = 1,

    /// <summary>Given back because the order was not confirmed before <see cref="StockHold.ExpiresAt"/>.</summary>
    Expired = 2,

    /// <summary>The order is confirmed: the stock stays held, without expiry, until dispatch.</summary>
    Confirmed = 3,
}

/// <summary>
/// A soft hold on stock for an order (P4): placed at checkout, with an expiry,
/// and counted in <see cref="StockBalance.Reserved"/> of one warehouse while
/// active. It keeps the promised quantity from being sold twice; the warehouse,
/// bin and batch that actually ship are allocated later, after payment or
/// credit approval, and may differ. Holds are the trail that justifies
/// <c>Reserved</c>, the way ledger entries justify <c>OnHand</c> (P2).
/// </summary>
public class StockHold : BaseEntity, IOrgScoped, INotAudited
{
    /// <inheritdoc />
    public Guid OrgId { get; set; }

    /// <summary>Order the stock is held for.</summary>
    public Guid OrderId { get; set; }

    /// <summary>The order's reference, for staff screens.</summary>
    public string ReferenceNo { get; set; } = string.Empty;

    /// <summary>Warehouse whose balance carries the hold.</summary>
    public Guid WarehouseId { get; set; }

    /// <summary>SKU held.</summary>
    public Guid SkuId { get; set; }

    /// <summary>SKU code, for staff screens.</summary>
    public string SkuCode { get; set; } = string.Empty;

    /// <summary>Amount held, in the SKU's base unit.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Unit of <see cref="Quantity"/> (the SKU's base unit).</summary>
    public string Uom { get; set; } = string.Empty;

    /// <summary>Where the hold is in its life.</summary>
    public StockHoldStatus Status { get; set; }

    /// <summary>UTC instant after which an unconfirmed hold is given back.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>UTC instant the hold was given back, if it was.</summary>
    public DateTime? ReleasedAt { get; set; }

    /// <summary>
    /// Derives the id of an order's hold on one SKU in one warehouse.
    /// </summary>
    /// <param name="orderId">Order.</param>
    /// <param name="warehouseId">Warehouse.</param>
    /// <param name="skuId">SKU.</param>
    /// <returns>The stable id.</returns>
    public static Guid IdFor(Guid orderId, Guid warehouseId, Guid skuId) =>
        IdGenerator.FromName($"stock_hold:{orderId:N}:{warehouseId:N}:{skuId:N}");
}
