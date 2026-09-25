using Platform.Shared.Dtos.Common;

namespace Platform.Shared.Dtos.Storefront;

// Everything in this namespace is customer-facing (P1). Types here carry the
// quantity a customer can buy, the price, the tax and a delivery window —
// never a warehouse, bin, batch, supplier, cost or per-location split. The
// opacity tests (tests/Platform.Tests.Opacity) check every property name.

/// <summary>
/// Whether a customer can buy a SKU, as one status.
/// </summary>
public enum ShopStockStatus
{
    /// <summary>No PIN code given yet, so availability is not known.</summary>
    CheckPincode = 0,

    /// <summary>Can be delivered to the customer's PIN code.</summary>
    InStock = 1,

    /// <summary>Delivered to the PIN code, but none is available now.</summary>
    OutOfStock = 2,

    /// <summary>Not delivered to the customer's PIN code.</summary>
    NotDeliverable = 3,
}

/// <summary>
/// A category in the store's menu.
/// </summary>
public class ShopCategoryDto
{
    /// <summary>Category id.</summary>
    public Guid Id { get; set; }

    /// <summary>Name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Sub-categories.</summary>
    public List<ShopCategoryDto> Children { get; set; } = new();
}

/// <summary>
/// A brand in the store's filter.
/// </summary>
public class ShopBrandDto
{
    /// <summary>Brand id.</summary>
    public Guid Id { get; set; }

    /// <summary>Name.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Query of <c>GET /api/storefront/products</c>.
/// </summary>
public class ShopProductRequest : PagedRequest
{
    /// <summary>Only products in this category (or below it).</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Only this brand.</summary>
    public Guid? BrandId { get; set; }

    /// <summary>Customer's PIN code, for availability.</summary>
    public string? Pincode { get; set; }
}

/// <summary>
/// The lowest price a product starts at.
/// </summary>
public class ShopFromPriceDto
{
    /// <summary>Price of one unit, excluding GST.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Unit.</summary>
    public string Uom { get; set; } = string.Empty;

    /// <summary>Currency (ISO 4217).</summary>
    public string Currency { get; set; } = string.Empty;
}

/// <summary>
/// A product tile in a store listing.
/// </summary>
public class ShopProductCardDto
{
    /// <summary>Product id.</summary>
    public Guid Id { get; set; }

    /// <summary>Name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Brand name.</summary>
    public string BrandName { get; set; } = string.Empty;

    /// <summary>Category name.</summary>
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>Variant labels (sizes, grades).</summary>
    public List<string> Variants { get; set; } = new();

    /// <summary>Lowest price among its variants; null when none is priced.</summary>
    public ShopFromPriceDto? FromPrice { get; set; }

    /// <summary>Best status among its variants for the customer's PIN code.</summary>
    public ShopStockStatus Status { get; set; }
}

/// <summary>
/// A category on the path to a product.
/// </summary>
public class ShopCrumbDto
{
    /// <summary>Category id.</summary>
    public Guid Id { get; set; }

    /// <summary>Name.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// One quantity slab of a price.
/// </summary>
public class ShopSlabDto
{
    /// <summary>Smallest quantity the rate applies to.</summary>
    public decimal MinQuantity { get; set; }

    /// <summary>Price of one unit, excluding GST.</summary>
    public decimal UnitPrice { get; set; }
}

/// <summary>
/// The customer's price for a SKU.
/// </summary>
public class ShopPriceDto
{
    /// <summary>Unit the price is for.</summary>
    public string Uom { get; set; } = string.Empty;

    /// <summary>Currency (ISO 4217).</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Rates by quantity; the whole quantity takes the rate of the highest slab reached.</summary>
    public List<ShopSlabDto> Slabs { get; set; } = new();

    /// <summary>GST rate in percent.</summary>
    public decimal TaxRatePercent { get; set; }

    /// <summary>Cess in percent.</summary>
    public decimal CessPercent { get; set; }
}

/// <summary>
/// How much of a SKU the customer can have delivered: one number (P1).
/// </summary>
public class ShopAvailabilityDto
{
    /// <summary>Status for the customer's PIN code.</summary>
    public ShopStockStatus Status { get; set; }

    /// <summary>Amount that can be ordered now, when the PIN code is known.</summary>
    public decimal? Quantity { get; set; }

    /// <summary>Unit of <see cref="Quantity"/>.</summary>
    public string Uom { get; set; } = string.Empty;
}

/// <summary>
/// When a delivery can arrive: a date range, never a per-location breakdown (P1).
/// </summary>
public class ShopDeliveryDto
{
    /// <summary>Earliest date (IST).</summary>
    public DateOnly EarliestOn { get; set; }

    /// <summary>Latest date (IST).</summary>
    public DateOnly LatestOn { get; set; }
}

/// <summary>
/// A variant (SKU) on a product page.
/// </summary>
public class ShopVariantDto
{
    /// <summary>SKU id.</summary>
    public Guid SkuId { get; set; }

    /// <summary>SKU code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Variant label.</summary>
    public string VariantLabel { get; set; } = string.Empty;

    /// <summary>Unit stock is counted in.</summary>
    public string BaseUom { get; set; } = string.Empty;

    /// <summary>Units it can be ordered in.</summary>
    public List<string> Units { get; set; } = new();

    /// <summary>The customer's price; null when it cannot be sold to them.</summary>
    public ShopPriceDto? Price { get; set; }

    /// <summary>Availability for the customer's PIN code.</summary>
    public ShopAvailabilityDto Availability { get; set; } = new();

    /// <summary>When a small order would arrive, when it can be delivered.</summary>
    public ShopDeliveryDto? Delivery { get; set; }
}

/// <summary>
/// A product page.
/// </summary>
public class ShopProductDto
{
    /// <summary>Product id.</summary>
    public Guid Id { get; set; }

    /// <summary>Name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Brand name.</summary>
    public string BrandName { get; set; } = string.Empty;

    /// <summary>HSN code (printed on the invoice).</summary>
    public string HsnCode { get; set; } = string.Empty;

    /// <summary>Category path, top first.</summary>
    public List<ShopCrumbDto> Breadcrumb { get; set; } = new();

    /// <summary>PIN code the availability is for, if any.</summary>
    public string? Pincode { get; set; }

    /// <summary>Variants that are on sale.</summary>
    public List<ShopVariantDto> Variants { get; set; } = new();
}
