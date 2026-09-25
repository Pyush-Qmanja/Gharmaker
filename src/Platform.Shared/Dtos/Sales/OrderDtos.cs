using Platform.Shared.Dtos.Common;
using Platform.Shared.Entities.Inventory;
using Platform.Shared.Entities.Sales;

namespace Platform.Shared.Dtos.Sales;

/// <summary>
/// Query of <c>GET /api/orders</c>. <see cref="PagedRequest.Search"/> is a reference prefix.
/// </summary>
public class OrderListRequest : PagedRequest
{
    /// <summary>Only orders in this state.</summary>
    public OrderStatus? Status { get; set; }

    /// <summary>Only this customer's orders.</summary>
    public Guid? CustomerId { get; set; }
}

/// <summary>
/// Taxable value, taxes and total of a line or an order.
/// </summary>
public class TaxAmountsDto
{
    /// <summary>Value before tax.</summary>
    public decimal TaxableAmount { get; set; }

    /// <summary>Central GST.</summary>
    public decimal CgstAmount { get; set; }

    /// <summary>State or union territory GST.</summary>
    public decimal SgstAmount { get; set; }

    /// <summary>Integrated GST.</summary>
    public decimal IgstAmount { get; set; }

    /// <summary>Cess.</summary>
    public decimal CessAmount { get; set; }

    /// <summary>Total payable.</summary>
    public decimal TotalAmount { get; set; }
}

/// <summary>
/// An order in a staff list.
/// </summary>
public class OrderSummaryDto : EntityDto
{
    /// <summary>Human reference.</summary>
    public string ReferenceNo { get; set; } = string.Empty;

    /// <summary>Customer.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Customer's name.</summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>State of the order.</summary>
    public OrderStatus Status { get; set; }

    /// <summary>Delivery PIN code.</summary>
    public string Pincode { get; set; } = string.Empty;

    /// <summary>Delivery city.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>Lines in the order.</summary>
    public int LineCount { get; set; }

    /// <summary>Total payable.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Currency.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Earliest promised delivery date.</summary>
    public DateOnly EarliestDeliveryOn { get; set; }

    /// <summary>Latest promised delivery date.</summary>
    public DateOnly LatestDeliveryOn { get; set; }

    /// <summary>When held stock is given back if the order is still only placed.</summary>
    public DateTime HoldExpiresAt { get; set; }
}

/// <summary>
/// One line of an order (staff view).
/// </summary>
public class OrderLineDto : TaxAmountsDto
{
    /// <summary>SKU.</summary>
    public Guid SkuId { get; set; }

    /// <summary>SKU code.</summary>
    public string SkuCode { get; set; } = string.Empty;

    /// <summary>"Product · variant".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>HSN code.</summary>
    public string HsnCode { get; set; } = string.Empty;

    /// <summary>Amount ordered.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Unit ordered in.</summary>
    public string Uom { get; set; } = string.Empty;

    /// <summary>Amount in the base unit.</summary>
    public decimal BaseQuantity { get; set; }

    /// <summary>Base unit.</summary>
    public string BaseUom { get; set; } = string.Empty;

    /// <summary>Price of one unit, excluding GST.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>GST rate in percent.</summary>
    public decimal TaxRatePercent { get; set; }

    /// <summary>Cess rate in percent.</summary>
    public decimal CessPercent { get; set; }
}

/// <summary>
/// Stock held for an order in one warehouse (staff only — P1).
/// </summary>
public class StockHoldDto
{
    /// <summary>Warehouse.</summary>
    public Guid WarehouseId { get; set; }

    /// <summary>Warehouse code.</summary>
    public string WarehouseCode { get; set; } = string.Empty;

    /// <summary>SKU code.</summary>
    public string SkuCode { get; set; } = string.Empty;

    /// <summary>Amount held.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Unit (base unit).</summary>
    public string Uom { get; set; } = string.Empty;

    /// <summary>State of the hold.</summary>
    public StockHoldStatus Status { get; set; }

    /// <summary>When it is given back if unconfirmed.</summary>
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// An order in full (staff view), including where its stock is held.
/// </summary>
public class OrderDto : OrderSummaryDto
{
    /// <summary>Customer's GSTIN at the time.</summary>
    public string? CustomerGstin { get; set; }

    /// <summary>Contact number.</summary>
    public string? Phone { get; set; }

    /// <summary>Delivery address.</summary>
    public AddressDto Address { get; set; } = new();

    /// <summary>Seller's registered state at the time.</summary>
    public string SellerState { get; set; } = string.Empty;

    /// <summary>True when IGST applies.</summary>
    public bool IsInterState { get; set; }

    /// <summary>Lines.</summary>
    public List<OrderLineDto> Lines { get; set; } = new();

    /// <summary>Order totals.</summary>
    public TaxAmountsDto Totals { get; set; } = new();

    /// <summary>When staff confirmed it with the customer.</summary>
    public DateTime? ConfirmedAt { get; set; }

    /// <summary>When it was cancelled or expired.</summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>Why it was cancelled.</summary>
    public string? Remarks { get; set; }

    /// <summary>Stock held for it, by warehouse.</summary>
    public List<StockHoldDto> Holds { get; set; } = new();
}

/// <summary>
/// Body of <c>POST /api/orders/{id}/cancel</c> and the storefront's cancel.
/// </summary>
public class CancelOrderRequest
{
    /// <summary>Why the order is cancelled.</summary>
    public string? Remarks { get; set; }
}
