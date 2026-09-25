using Platform.Shared.Dtos.Common;
using Platform.Shared.Entities.Pricing;

namespace Platform.Shared.Dtos.Pricing;

/// <summary>
/// Fields shared by the create and update requests of a price list.
/// </summary>
public interface IPriceListFields
{
    /// <summary>Display name.</summary>
    string Name { get; }

    /// <summary>Short unique code.</summary>
    string Code { get; }

    /// <summary>Who the list applies to.</summary>
    PriceListType Type { get; }

    /// <summary>The customer of a contract list.</summary>
    Guid? CustomerId { get; }

    /// <summary>Internal notes.</summary>
    string? Remarks { get; }
}

/// <summary>
/// A price list as shown to staff.
/// </summary>
public class PriceListDto : EntityDto
{
    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Short unique code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Who the list applies to.</summary>
    public PriceListType Type { get; set; }

    /// <summary>The customer of a contract list.</summary>
    public Guid? CustomerId { get; set; }

    /// <summary>That customer's name, for display.</summary>
    public string? CustomerName { get; set; }

    /// <summary>Currency of the list (ISO 4217).</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Internal notes.</summary>
    public string? Remarks { get; set; }

    /// <summary>False when deactivated.</summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// Body of <c>POST /api/price-lists</c>.
/// </summary>
public class CreatePriceListRequest : IPriceListFields
{
    /// <inheritdoc />
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    public string Code { get; set; } = string.Empty;

    /// <inheritdoc />
    public PriceListType Type { get; set; } = PriceListType.Tier;

    /// <inheritdoc />
    public Guid? CustomerId { get; set; }

    /// <inheritdoc />
    public string? Remarks { get; set; }
}

/// <summary>
/// Body of <c>PUT /api/price-lists/{id}</c>.
/// </summary>
public class UpdatePriceListRequest : IPriceListFields, IActivatableRequest
{
    /// <inheritdoc />
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    public string Code { get; set; } = string.Empty;

    /// <inheritdoc />
    public PriceListType Type { get; set; }

    /// <inheritdoc />
    public Guid? CustomerId { get; set; }

    /// <inheritdoc />
    public string? Remarks { get; set; }

    /// <inheritdoc />
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// One quantity slab of a price.
/// </summary>
public class PriceSlabDto
{
    /// <summary>Smallest quantity the rate applies to, in the price's unit; 0 for the first slab.</summary>
    public decimal MinQuantity { get; set; }

    /// <summary>Price of one unit, excluding GST.</summary>
    public decimal UnitPrice { get; set; }
}

/// <summary>
/// One dated price of a SKU in a price list.
/// </summary>
public class SkuPriceDto : EntityDto
{
    /// <summary>Price list.</summary>
    public Guid PriceListId { get; set; }

    /// <summary>SKU.</summary>
    public Guid SkuId { get; set; }

    /// <summary>Unit the price is for.</summary>
    public string Uom { get; set; } = string.Empty;

    /// <summary>Currency (ISO 4217).</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Rates by quantity, ascending.</summary>
    public List<PriceSlabDto> Slabs { get; set; } = new();

    /// <summary>UTC instant from which it applies.</summary>
    public DateTime ValidFrom { get; set; }

    /// <summary>Why it was set.</summary>
    public string? Remarks { get; set; }

    /// <summary>True for the price in force now.</summary>
    public bool IsCurrent { get; set; }
}

/// <summary>
/// Body of <c>POST /api/prices</c>: sets a new price from a date (P8 — never overwrites).
/// </summary>
public class SetSkuPriceRequest : INormalisable
{
    /// <summary>Price list.</summary>
    public Guid PriceListId { get; set; }

    /// <summary>SKU.</summary>
    public Guid SkuId { get; set; }

    /// <summary>Unit the price is for; any unit the SKU can be expressed in.</summary>
    public string Uom { get; set; } = string.Empty;

    /// <summary>Rates by quantity; the first starts at 0.</summary>
    public List<PriceSlabDto> Slabs { get; set; } = new();

    /// <summary>UTC instant from which it applies; now when empty. Cannot be in the past.</summary>
    public DateTime? ValidFrom { get; set; }

    /// <summary>Why it is set.</summary>
    public string? Remarks { get; set; }

    /// <summary>
    /// Drops blank slab rows a form posts (0 from, 0 price, after the first),
    /// sorts the rest by quantity and upper-cases the unit.
    /// </summary>
    public void Normalise()
    {
        Uom = (Uom ?? string.Empty).Trim().ToUpperInvariant();
        Slabs = Slabs
            .Where((slab, index) => index == 0 || slab.MinQuantity != 0 || slab.UnitPrice != 0)
            .OrderBy(s => s.MinQuantity)
            .ToList();
        Remarks = string.IsNullOrWhiteSpace(Remarks) ? null : Remarks.Trim();
    }
}

/// <summary>
/// Query of <c>GET /api/prices</c>: one page of products with each SKU's current price in a list.
/// </summary>
public class PriceGridRequest : PagedRequest
{
    /// <summary>Price list to show.</summary>
    public Guid PriceListId { get; set; }

    /// <summary>Only products in this category (or below it).</summary>
    public Guid? CategoryId { get; set; }
}

/// <summary>
/// A product in the price grid with its SKUs.
/// </summary>
public class PriceGridProductDto
{
    /// <summary>Product id.</summary>
    public Guid ProductId { get; set; }

    /// <summary>Product name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Brand name.</summary>
    public string BrandName { get; set; } = string.Empty;

    /// <summary>HSN code.</summary>
    public string HsnCode { get; set; } = string.Empty;

    /// <summary>GST rate in force for the HSN code; null when none is set (the product cannot be sold).</summary>
    public decimal? TaxRatePercent { get; set; }

    /// <summary>SKUs of the product.</summary>
    public List<PriceGridSkuDto> Skus { get; set; } = new();
}

/// <summary>
/// A SKU in the price grid with its current price.
/// </summary>
public class PriceGridSkuDto
{
    /// <summary>SKU id.</summary>
    public Guid SkuId { get; set; }

    /// <summary>SKU code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Variant label.</summary>
    public string VariantLabel { get; set; } = string.Empty;

    /// <summary>Base unit.</summary>
    public string BaseUom { get; set; } = string.Empty;

    /// <summary>Every unit a price can be set in.</summary>
    public List<string> Units { get; set; } = new();

    /// <summary>False when the SKU is deactivated.</summary>
    public bool IsActive { get; set; }

    /// <summary>Price in force now; null when the SKU has no price in this list.</summary>
    public SkuPriceDto? Current { get; set; }

    /// <summary>A price set to start later, if any.</summary>
    public SkuPriceDto? Upcoming { get; set; }
}

/// <summary>
/// A GST rate as shown to staff.
/// </summary>
public class TaxRateDto : EntityDto
{
    /// <summary>HSN code.</summary>
    public string HsnCode { get; set; } = string.Empty;

    /// <summary>GST rate in percent.</summary>
    public decimal RatePercent { get; set; }

    /// <summary>Cess in percent.</summary>
    public decimal CessPercent { get; set; }

    /// <summary>UTC instant from which it applies.</summary>
    public DateTime ValidFrom { get; set; }

    /// <summary>Source of the rate.</summary>
    public string? Remarks { get; set; }

    /// <summary>True for the rate in force now for its HSN code.</summary>
    public bool IsCurrent { get; set; }

    /// <summary>Active products whose HSN code takes this rate.</summary>
    public int ProductCount { get; set; }
}

/// <summary>
/// Body of <c>POST /api/tax-rates</c>: a new rate from a date (P8).
/// </summary>
public class CreateTaxRateRequest
{
    /// <summary>HSN code (4, 6 or 8 digits).</summary>
    public string HsnCode { get; set; } = string.Empty;

    /// <summary>GST rate in percent.</summary>
    public decimal RatePercent { get; set; }

    /// <summary>Cess in percent.</summary>
    public decimal CessPercent { get; set; }

    /// <summary>UTC instant from which it applies; now when empty. Cannot be in the past.</summary>
    public DateTime? ValidFrom { get; set; }

    /// <summary>Source of the rate, e.g. a notification number.</summary>
    public string? Remarks { get; set; }
}

/// <summary>
/// Query of <c>GET /api/tax-rates</c>: rates in force now, or the history of one HSN code.
/// </summary>
public class TaxRateListRequest : PagedRequest
{
    /// <summary>When set, every rate of this HSN code, newest first.</summary>
    public string? HsnCode { get; set; }
}

/// <summary>
/// An HSN code used by active products that has no GST rate, so those products cannot be sold.
/// </summary>
public class MissingTaxRateDto
{
    /// <summary>HSN code.</summary>
    public string HsnCode { get; set; } = string.Empty;

    /// <summary>Active products using it.</summary>
    public int ProductCount { get; set; }

    /// <summary>A few of their names.</summary>
    public List<string> ExampleProducts { get; set; } = new();
}
