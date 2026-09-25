using Platform.Shared.Common;
using Platform.Shared.Entities.Common;

namespace Platform.Shared.Entities.Pricing;

/// <summary>
/// Which customers a price list applies to. The price of a SKU for a customer
/// is taken from the first list that has one, in this order: the customer's
/// contract list, their tier list, then the retail list. With none, the SKU is
/// not sellable to them — the price never defaults to zero.
/// </summary>
public enum PriceListType
{
    /// <summary>The default list for everyone. Exactly one may be active.</summary>
    Retail = 0,

    /// <summary>A list shared by a group of customers (e.g. contractors), set on each customer.</summary>
    Tier = 1,

    /// <summary>Agreed prices for one customer.</summary>
    Contract = 2,
}

/// <summary>
/// A named set of prices. The prices themselves are <see cref="SkuPrice"/> rows,
/// each dated, so the list can change without rewriting history (P8).
/// </summary>
public class PriceList : BaseEntity, IOrgScoped, ISoftDeletable
{
    /// <inheritdoc />
    public Guid OrgId { get; set; }

    /// <summary>Display name, e.g. "Retail" or "Contractors".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Short unique code, e.g. RETAIL.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Who the list applies to.</summary>
    public PriceListType Type { get; set; }

    /// <summary>The one customer of a <see cref="PriceListType.Contract"/> list; null otherwise.</summary>
    public Guid? CustomerId { get; set; }

    /// <summary>Currency of every price in the list (ISO 4217).</summary>
    public string Currency { get; set; } = Money.Inr;

    /// <summary>Internal notes.</summary>
    public string? Remarks { get; set; }

    /// <inheritdoc />
    public bool IsActive { get; set; } = true;
}
