using Platform.Shared.Common;
using Platform.Shared.Entities.Common;

namespace Platform.Shared.Entities.Sales;

/// <summary>
/// Where an order is in its life. Phase 4 adds confirmation, allocation and
/// dispatch; until then an order is placed with its stock held, and ends
/// cancelled or expired if nobody confirms it.
/// </summary>
public enum OrderStatus
{
    /// <summary>Placed by the customer; stock is held until the hold expires.</summary>
    Placed = 0,

    /// <summary>Cancelled by the customer or staff; held stock was given back.</summary>
    Cancelled = 1,

    /// <summary>Not confirmed in time; held stock was given back.</summary>
    Expired = 2,

    /// <summary>Confirmed with the customer by phone; its stock stays held (no expiry) until dispatch.</summary>
    Confirmed = 3,
}

/// <summary>
/// The GST on an amount, split the way a tax invoice shows it. Within the
/// seller's state it is CGST + SGST (half each); to another state it is IGST.
/// </summary>
public class TaxAmounts
{
    /// <summary>Value before tax.</summary>
    public decimal TaxableAmount { get; set; }

    /// <summary>Central GST.</summary>
    public decimal CgstAmount { get; set; }

    /// <summary>State (or union territory) GST.</summary>
    public decimal SgstAmount { get; set; }

    /// <summary>Integrated GST (sale to another state).</summary>
    public decimal IgstAmount { get; set; }

    /// <summary>Compensation cess.</summary>
    public decimal CessAmount { get; set; }

    /// <summary>Taxable value plus every tax.</summary>
    public decimal TotalAmount { get; set; }
}

/// <summary>
/// One line of an order, with the price and tax that were true when it was
/// placed (P8), so the order never changes when a price does.
/// </summary>
public class OrderLine : TaxAmounts
{
    /// <summary>SKU ordered.</summary>
    public Guid SkuId { get; set; }

    /// <summary>Product of the SKU.</summary>
    public Guid ProductId { get; set; }

    /// <summary>SKU code at the time.</summary>
    public string SkuCode { get; set; } = string.Empty;

    /// <summary>"Product · variant" at the time.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>HSN code at the time.</summary>
    public string HsnCode { get; set; } = string.Empty;

    /// <summary>Amount ordered.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Unit ordered in.</summary>
    public string Uom { get; set; } = string.Empty;

    /// <summary>The same amount in the SKU's base unit (what stock is held in).</summary>
    public decimal BaseQuantity { get; set; }

    /// <summary>The SKU's base unit.</summary>
    public string BaseUom { get; set; } = string.Empty;

    /// <summary>Price list the price came from.</summary>
    public Guid PriceListId { get; set; }

    /// <summary>Price of one <see cref="Uom"/>, excluding GST, after the quantity slab.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>GST rate in percent.</summary>
    public decimal TaxRatePercent { get; set; }

    /// <summary>Cess rate in percent.</summary>
    public decimal CessPercent { get; set; }
}

/// <summary>
/// An order placed on the online store. Issued once and never edited (P9):
/// cancelling changes only its status, and Phase 4 fulfils it through
/// separate documents.
/// </summary>
public class Order : BaseEntity, IOrgScoped
{
    /// <inheritdoc />
    public Guid OrgId { get; set; }

    /// <summary>Human reference, e.g. ORD-2627-000042.</summary>
    public string ReferenceNo { get; set; } = string.Empty;

    /// <summary>Idempotency key sent by the checkout page: placing the same order twice returns the first.</summary>
    public Guid ClientId { get; set; }

    /// <summary>Customer who placed it.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Customer's name at the time.</summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>Customer's GSTIN at the time, if any.</summary>
    public string? CustomerGstin { get; set; }

    /// <summary>Contact number for the delivery.</summary>
    public string? Phone { get; set; }

    /// <summary>Where to deliver; its state is the place of supply.</summary>
    public Address Address { get; set; } = new();

    /// <summary>Seller's registered state at the time (one registered address, decision 1).</summary>
    public string SellerState { get; set; } = string.Empty;

    /// <summary>True when the delivery state differs from the seller's (IGST instead of CGST + SGST).</summary>
    public bool IsInterState { get; set; }

    /// <summary>Currency of every amount (ISO 4217).</summary>
    public string Currency { get; set; } = Money.Inr;

    /// <summary>Lines, in the order the customer added them.</summary>
    public List<OrderLine> Lines { get; set; } = new();

    /// <summary>Sums of the lines: taxable value, each tax and the amount payable.</summary>
    public TaxAmounts Totals { get; set; } = new();

    /// <summary>Where the order is in its life.</summary>
    public OrderStatus Status { get; set; }

    /// <summary>Earliest promised delivery date (IST).</summary>
    public DateOnly EarliestDeliveryOn { get; set; }

    /// <summary>Latest promised delivery date (IST).</summary>
    public DateOnly LatestDeliveryOn { get; set; }

    /// <summary>UTC instant the held stock is given back if the order is still only placed.</summary>
    public DateTime HoldExpiresAt { get; set; }

    /// <summary>When staff confirmed the order with the customer; null until then.</summary>
    public DateTime? ConfirmedAt { get; set; }

    /// <summary>UTC instant it was cancelled or expired.</summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>Why it was cancelled.</summary>
    public string? Remarks { get; set; }

    /// <summary>
    /// Derives the id of the order a customer places with a checkout key, so a
    /// double-clicked or retried checkout finds the first order instead of placing another.
    /// </summary>
    /// <param name="customerId">Customer.</param>
    /// <param name="clientId">Key from the checkout page.</param>
    /// <returns>The stable id.</returns>
    public static Guid IdFor(Guid customerId, Guid clientId) => IdGenerator.FromName($"order:{customerId:N}:{clientId:N}");
}
