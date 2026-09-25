using Platform.Shared.Common;
using Platform.Shared.Entities.Common;

namespace Platform.Shared.Entities.Pricing;

/// <summary>
/// The unit price from a quantity upward. Slabs apply to the whole quantity:
/// buying 150 bags when the 100-bag slab is cheaper prices all 150 at the
/// 100-bag rate (decision 3 in <c>docs/decisions-pending.md</c>).
/// </summary>
public class PriceSlab
{
    /// <summary>Smallest quantity (in the price's unit) this rate applies to; the first slab is 0.</summary>
    public decimal MinQuantity { get; set; }

    /// <summary>Price of one unit, excluding GST.</summary>
    public decimal UnitPrice { get; set; }
}

/// <summary>
/// The price of one SKU in one price list from <see cref="ValidFrom"/> onward
/// (P8). Rows are only ever inserted: a new price is a new row, so an order
/// can always show the price that was true when it was placed.
/// </summary>
public class SkuPrice : BaseEntity, IOrgScoped
{
    /// <inheritdoc />
    public Guid OrgId { get; set; }

    /// <summary>Price list the price belongs to.</summary>
    public Guid PriceListId { get; set; }

    /// <summary>SKU priced.</summary>
    public Guid SkuId { get; set; }

    /// <summary>Product of the SKU, so a list's prices can be read product by product.</summary>
    public Guid ProductId { get; set; }

    /// <summary>Unit the price is for (any unit the SKU can be expressed in, e.g. BAG or TONNE).</summary>
    public string Uom { get; set; } = string.Empty;

    /// <summary>Currency (ISO 4217), copied from the list.</summary>
    public string Currency { get; set; } = Money.Inr;

    /// <summary>Rates by quantity, ascending by <see cref="PriceSlab.MinQuantity"/>, the first at 0.</summary>
    public List<PriceSlab> Slabs { get; set; } = new();

    /// <summary>UTC instant from which this price applies; it applies until a later row starts.</summary>
    public DateTime ValidFrom { get; set; }

    /// <summary>Why the price was set or changed.</summary>
    public string? Remarks { get; set; }
}
