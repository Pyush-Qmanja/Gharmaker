using Platform.Shared.Dtos.Storefront;
using Platform.Web.Models;

namespace Platform.Web.Areas.Shop.Models;

/// <summary>
/// The store's home page.
/// </summary>
/// <param name="Categories">Top-level categories.</param>
/// <param name="Featured">A first page of products.</param>
/// <param name="Brands">Brands on sale.</param>
/// <param name="Pincode">Visitor's PIN code, if known.</param>
public sealed record ShopHomeViewModel(
    IReadOnlyList<ShopCategoryDto> Categories, IReadOnlyList<ShopProductCardDto> Featured, IReadOnlyList<ShopBrandDto> Brands, string? Pincode);

/// <summary>
/// A product listing: category menu, brand filter and a page of products.
/// </summary>
/// <param name="Products">One page of products.</param>
/// <param name="Categories">Category tree.</param>
/// <param name="Brands">Brands on sale.</param>
/// <param name="CategoryId">Chosen category.</param>
/// <param name="BrandId">Chosen brand.</param>
/// <param name="Heading">Page heading (category name, search or "All products").</param>
/// <param name="Pincode">Visitor's PIN code, if known.</param>
public sealed record ShopListingViewModel(
    ListViewModel<ShopProductCardDto> Products,
    IReadOnlyList<ShopCategoryDto> Categories,
    IReadOnlyList<ShopBrandDto> Brands,
    Guid? CategoryId,
    Guid? BrandId,
    string Heading,
    string? Pincode)
{
    /// <summary>
    /// True when a PIN code is set and no warehouse delivers there (every product says so).
    /// </summary>
    public bool IsPincodeUnserved =>
        Pincode is not null && ((IReadOnlyList<ShopProductCardDto>)Products.Items).Any(p => p.Status == ShopStockStatus.NotDeliverable);

    /// <summary>
    /// Whether a category is the chosen one or contains it, so its sub-categories are shown.
    /// </summary>
    /// <param name="category">Top-level category.</param>
    /// <returns>True when it is open in the menu.</returns>
    public bool IsOpen(ShopCategoryDto category) =>
        CategoryId is { } id && (category.Id == id || category.Children.Any(c => c.Id == id));
}

/// <summary>
/// A product page.
/// </summary>
/// <param name="Product">The product.</param>
/// <param name="Pincode">Visitor's PIN code, if known.</param>
/// <param name="IsSignedIn">True when a customer is signed in.</param>
public sealed record ShopProductViewModel(ShopProductDto Product, string? Pincode, bool IsSignedIn)
{
    /// <summary>True when a PIN code is set and no warehouse delivers there.</summary>
    public bool IsPincodeUnserved =>
        Pincode is not null && Product.Variants.Count > 0 && Product.Variants.All(v => v.Availability.Status == ShopStockStatus.NotDeliverable);
}

/// <summary>
/// Checkout form: where to deliver, a contact number, an optional GSTIN, and
/// the key that makes placing the order safe to repeat.
/// </summary>
public sealed class CheckoutForm
{
    /// <summary>Key created when the page is shown; sending it twice places one order.</summary>
    public Guid ClientId { get; set; }

    /// <summary>Delivery address.</summary>
    public ShopAddressDto Address { get; set; } = new();

    /// <summary>Mobile number for the delivery.</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>GSTIN for a B2B invoice.</summary>
    public string? Gstin { get; set; }

    /// <summary>True to save the address as the default.</summary>
    public bool SaveAddress { get; set; } = true;
}

/// <summary>
/// The checkout page.
/// </summary>
/// <param name="Form">Address and contact.</param>
/// <param name="Cart">The cart being ordered.</param>
public sealed record CheckoutViewModel(CheckoutForm Form, ShopCartDto Cart);

/// <summary>
/// One of the customer's orders.
/// </summary>
/// <param name="Order">The order.</param>
/// <param name="JustPlaced">True right after checkout, to thank the customer.</param>
public sealed record ShopOrderViewModel(ShopOrderDto Order, bool JustPlaced);

/// <summary>
/// What the store's header shows.
/// </summary>
/// <param name="Categories">Top-level categories for the menu.</param>
/// <param name="CustomerName">Signed-in customer's name, if any.</param>
/// <param name="CartCount">Lines in the cart.</param>
/// <param name="Pincode">Delivery PIN code, if known.</param>
/// <param name="Search">Current search text.</param>
public sealed record ShopHeaderViewModel(IReadOnlyList<ShopCategoryDto> Categories, string? CustomerName, int CartCount, string? Pincode, string? Search);

/// <summary>
/// A totals box: taxable value, GST split the way the invoice will show it, and the amount payable.
/// </summary>
/// <param name="Totals">Amounts.</param>
/// <param name="IsInterState">True when IGST applies.</param>
/// <param name="TotalLabel">Words for the last line, e.g. "Total payable".</param>
public sealed record TotalsViewModel(ShopTaxDto Totals, bool IsInterState, string TotalLabel);
