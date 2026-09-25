namespace Platform.Shared.Dtos.Storefront;

/// <summary>
/// Where an order is, in words a customer understands.
/// </summary>
public enum ShopOrderStatus
{
    /// <summary>Placed; the store will confirm it.</summary>
    Placed = 0,

    /// <summary>Cancelled.</summary>
    Cancelled = 1,

    /// <summary>Not confirmed in time and closed.</summary>
    Expired = 2,

    /// <summary>Confirmed by the store; delivery is on its way to being arranged.</summary>
    Confirmed = 3,
}

/// <summary>
/// An order in the customer's list.
/// </summary>
public class ShopOrderSummaryDto
{
    /// <summary>Order id.</summary>
    public Guid Id { get; set; }

    /// <summary>Order number.</summary>
    public string ReferenceNo { get; set; } = string.Empty;

    /// <summary>State.</summary>
    public ShopOrderStatus Status { get; set; }

    /// <summary>UTC instant it was placed.</summary>
    public DateTime PlacedAt { get; set; }

    /// <summary>Lines.</summary>
    public int LineCount { get; set; }

    /// <summary>Total payable.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Currency.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Promised delivery window.</summary>
    public ShopDeliveryDto Delivery { get; set; } = new();
}

/// <summary>
/// One line of the customer's order.
/// </summary>
public class ShopOrderLineDto : ShopTaxDto
{
    /// <summary>SKU code.</summary>
    public string SkuCode { get; set; } = string.Empty;

    /// <summary>"Product · variant".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>HSN code.</summary>
    public string HsnCode { get; set; } = string.Empty;

    /// <summary>Amount ordered.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Unit.</summary>
    public string Uom { get; set; } = string.Empty;

    /// <summary>Price of one unit, excluding GST.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>GST rate in percent.</summary>
    public decimal TaxRatePercent { get; set; }
}

/// <summary>
/// The customer's order in full.
/// </summary>
public class ShopOrderDto : ShopOrderSummaryDto
{
    /// <summary>Delivery address.</summary>
    public ShopAddressDto Address { get; set; } = new();

    /// <summary>Contact number.</summary>
    public string? Phone { get; set; }

    /// <summary>GSTIN the invoice will carry.</summary>
    public string? Gstin { get; set; }

    /// <summary>True when IGST applies.</summary>
    public bool IsInterState { get; set; }

    /// <summary>Lines.</summary>
    public List<ShopOrderLineDto> Lines { get; set; } = new();

    /// <summary>Totals.</summary>
    public ShopTaxDto Totals { get; set; } = new();

    /// <summary>True while the customer may still cancel it.</summary>
    public bool CanCancel { get; set; }

    /// <summary>When it was cancelled or closed.</summary>
    public DateTime? ClosedAt { get; set; }
}
