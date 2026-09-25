using Platform.Shared.Dtos.Common;

namespace Platform.Shared.Dtos.Storefront;

/// <summary>
/// Taxable value, GST and total, as the customer sees them.
/// </summary>
public class ShopTaxDto
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
/// A delivery or billing address as the customer enters it.
/// </summary>
public class ShopAddressDto
{
    /// <summary>House, building, street.</summary>
    public string Line1 { get; set; } = string.Empty;

    /// <summary>Area, landmark.</summary>
    public string? Line2 { get; set; }

    /// <summary>City or town.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>State or union territory.</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>PIN code.</summary>
    public string Pincode { get; set; } = string.Empty;
}

/// <summary>
/// One line of the cart, priced now.
/// </summary>
public class ShopCartLineDto : ShopTaxDto
{
    /// <summary>SKU id.</summary>
    public Guid SkuId { get; set; }

    /// <summary>Product id (for the link back).</summary>
    public Guid ProductId { get; set; }

    /// <summary>SKU code.</summary>
    public string SkuCode { get; set; } = string.Empty;

    /// <summary>"Product · variant".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Amount wanted.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Unit wanted.</summary>
    public string Uom { get; set; } = string.Empty;

    /// <summary>Units the line can be changed to.</summary>
    public List<string> Units { get; set; } = new();

    /// <summary>Price of one unit at this quantity, excluding GST; null when not sellable.</summary>
    public decimal? UnitPrice { get; set; }

    /// <summary>GST rate in percent; null when not sellable.</summary>
    public decimal? TaxRatePercent { get; set; }

    /// <summary>Why the line cannot be ordered as it is, if it cannot.</summary>
    public string? Problem { get; set; }
}

/// <summary>
/// The customer's cart with prices, GST and whether it can be ordered.
/// </summary>
public class ShopCartDto
{
    /// <summary>PIN code availability is checked for.</summary>
    public string? Pincode { get; set; }

    /// <summary>Currency (ISO 4217).</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Lines.</summary>
    public List<ShopCartLineDto> Lines { get; set; } = new();

    /// <summary>Totals of the lines that can be ordered.</summary>
    public ShopTaxDto Totals { get; set; } = new();

    /// <summary>True when IGST applies (delivery to another state than the seller's).</summary>
    public bool IsInterState { get; set; }

    /// <summary>When everything would arrive, if it can be ordered.</summary>
    public ShopDeliveryDto? Delivery { get; set; }

    /// <summary>True when every line can be ordered now.</summary>
    public bool CanCheckout { get; set; }

    /// <summary>What stops checkout, in plain words.</summary>
    public List<string> Problems { get; set; } = new();
}

/// <summary>
/// Body of <c>POST /api/storefront/cart/lines</c>.
/// </summary>
public class AddCartLineRequest
{
    /// <summary>SKU.</summary>
    public Guid SkuId { get; set; }

    /// <summary>Amount to add.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Unit of the amount.</summary>
    public string Uom { get; set; } = string.Empty;
}

/// <summary>
/// Body of <c>PUT /api/storefront/cart/lines/{skuId}</c>.
/// </summary>
public class UpdateCartLineRequest
{
    /// <summary>New amount.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Unit of the amount.</summary>
    public string Uom { get; set; } = string.Empty;
}

/// <summary>
/// Body of <c>PUT /api/storefront/cart/pincode</c>.
/// </summary>
public class SetPincodeRequest
{
    /// <summary>PIN code to deliver to.</summary>
    public string Pincode { get; set; } = string.Empty;
}

/// <summary>
/// Body of <c>POST /api/storefront/checkout</c>: places the order for the cart.
/// </summary>
public class CheckoutRequest : INormalisable
{
    /// <summary>A key the checkout page creates once; sending it again returns the same order.</summary>
    public Guid ClientId { get; set; }

    /// <summary>Where to deliver.</summary>
    public ShopAddressDto Address { get; set; } = new();

    /// <summary>Contact number for the delivery.</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>Buyer's GSTIN for a B2B invoice, optional.</summary>
    public string? Gstin { get; set; }

    /// <summary>
    /// Trims the fields and upper-cases the GSTIN.
    /// </summary>
    public void Normalise()
    {
        Phone = (Phone ?? string.Empty).Trim();
        Gstin = string.IsNullOrWhiteSpace(Gstin) ? null : Gstin.Trim().ToUpperInvariant();
        Address.Pincode = (Address.Pincode ?? string.Empty).Trim();
    }
}
