using System.Globalization;
using Platform.Api.Repositories;
using Platform.Api.Services.Catalog;
using Platform.Api.Services.Identity;
using Platform.Api.Services.Inventory;
using Platform.Api.Services.Pricing;
using Platform.Shared.Commerce;
using Platform.Shared.Common;
using Platform.Shared.Constants;
using Platform.Shared.Entities.Catalog;
using Platform.Shared.Entities.Pricing;
using Platform.Shared.Entities.Sales;
using Platform.Shared.Units;

namespace Platform.Api.Services.Storefront;

/// <summary>
/// One cart line priced for a customer and a PIN code.
/// </summary>
public sealed class PricedLine
{
    /// <summary>The line as the customer entered it.</summary>
    public required CartLine Line { get; init; }

    /// <summary>The SKU, when it is still on sale.</summary>
    public Sku? Sku { get; set; }

    /// <summary>Its product, when it is still on sale.</summary>
    public Product? Product { get; set; }

    /// <summary>"Product · variant".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Units the line can be ordered in.</summary>
    public List<string> Units { get; set; } = new();

    /// <summary>The amount in the SKU's base unit.</summary>
    public decimal BaseQuantity { get; set; }

    /// <summary>The customer's price.</summary>
    public ResolvedPrice? Price { get; set; }

    /// <summary>GST rate that applies.</summary>
    public TaxRate? Tax { get; set; }

    /// <summary>Price of one unit of the line's unit at this quantity, excluding GST.</summary>
    public decimal? UnitPrice { get; set; }

    /// <summary>Taxable value, GST and total.</summary>
    public TaxAmounts? Amounts { get; set; }

    /// <summary>Why the line cannot be ordered as it is.</summary>
    public string? Problem { get; set; }
}

/// <summary>
/// A cart priced for a customer and a PIN code.
/// </summary>
public sealed class PricedCart
{
    /// <summary>Lines, in cart order.</summary>
    public List<PricedLine> Lines { get; } = new();

    /// <summary>Totals of the lines that have a price.</summary>
    public TaxAmounts Totals { get; set; } = new();

    /// <summary>True when IGST applies.</summary>
    public bool IsInterState { get; set; }

    /// <summary>Seller's registered state, when set.</summary>
    public string? SellerState { get; set; }

    /// <summary>Warehouses delivering to the PIN code (internal).</summary>
    public IReadOnlyList<ServingWarehouse> Serving { get; set; } = Array.Empty<ServingWarehouse>();

    /// <summary>
    /// The one warehouse that would ship the whole order (internal, P1: never
    /// leaves the API). Null until every line can be supplied from one place.
    /// </summary>
    public OrderSource? Source { get; set; }

    /// <summary>Delivery date of the whole order, when one warehouse can supply it all.</summary>
    public (DateOnly Earliest, DateOnly Latest)? Window { get; set; }

    /// <summary>Problems with the cart as a whole, in plain words.</summary>
    public List<string> Problems { get; } = new();

    /// <summary>True when the order can be placed now.</summary>
    public bool CanCheckout => Lines.Count > 0 && Problems.Count == 0 && Lines.All(l => l.Problem is null);
}

/// <summary>
/// Prices cart lines exactly as checkout will: the customer's price list and
/// quantity slab, GST by HSN code and place of supply, and whether the
/// serving warehouses can supply it. The cart page and checkout share it, so
/// what the customer is shown is what they are charged.
/// </summary>
public interface IShopPricer
{
    /// <summary>
    /// Prices lines.
    /// </summary>
    /// <param name="lines">Lines to price.</param>
    /// <param name="customer">Signed-in customer, or null for retail prices.</param>
    /// <param name="pincode">Delivery PIN code, if known.</param>
    /// <param name="deliveryState">Delivery state, if known; otherwise GST is shown as within the seller's state.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The priced cart.</returns>
    Task<PricedCart> PriceAsync(
        IReadOnlyList<CartLine> lines, Customer? customer, string? pincode, string? deliveryState, CancellationToken cancellationToken = default);
}

/// <summary>
/// Firestore-backed <see cref="IShopPricer"/>.
/// </summary>
public sealed class ShopPricer : IShopPricer
{
    private readonly IRepository<Sku> _skus;
    private readonly IRepository<Product> _products;
    private readonly IPriceResolver _prices;
    private readonly ITaxRateProvider _taxRates;
    private readonly IBusinessSettingsService _business;
    private readonly IAvailabilityService _availability;
    private readonly IUomConversionProvider _conversions;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates the pricer.
    /// </summary>
    /// <param name="skus">SKU data access.</param>
    /// <param name="products">Product data access.</param>
    /// <param name="prices">Resolves the customer's prices.</param>
    /// <param name="taxRates">GST rates.</param>
    /// <param name="business">Seller's registered state.</param>
    /// <param name="availability">Stock of serving warehouses.</param>
    /// <param name="conversions">Unit conversion (P7).</param>
    /// <param name="timeProvider">Clock.</param>
    public ShopPricer(
        IRepository<Sku> skus,
        IRepository<Product> products,
        IPriceResolver prices,
        ITaxRateProvider taxRates,
        IBusinessSettingsService business,
        IAvailabilityService availability,
        IUomConversionProvider conversions,
        TimeProvider timeProvider)
    {
        _skus = skus;
        _products = products;
        _prices = prices;
        _taxRates = taxRates;
        _business = business;
        _availability = availability;
        _conversions = conversions;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<PricedCart> PriceAsync(
        IReadOnlyList<CartLine> lines, Customer? customer, string? pincode, string? deliveryState, CancellationToken cancellationToken = default)
    {
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        var cart = new PricedCart { SellerState = await _business.GetSellerStateAsync(cancellationToken) };
        cart.IsInterState = cart.SellerState is not null && deliveryState is not null && !IndianStates.AreSame(cart.SellerState, deliveryState);

        List<Guid> skuIds = lines.Select(l => l.SkuId).Distinct().ToList();
        var skus = (await _skus.GetByIdsAsync(skuIds, cancellationToken)).ToDictionary(s => s.Id);
        var products = (await _products.GetByIdsAsync(skus.Values.Select(s => s.ProductId), cancellationToken)).ToDictionary(p => p.Id);
        IReadOnlyDictionary<Guid, ResolvedPrice> prices = await _prices.ResolveAsync(skuIds, customer, now, cancellationToken);
        TaxRateTable taxes = await _taxRates.GetAsync(cancellationToken);
        IUomConversionService converter = await _conversions.GetAsync(cancellationToken);

        IReadOnlyDictionary<Guid, List<WarehouseStock>>? stock = null;
        if (!string.IsNullOrEmpty(pincode))
        {
            cart.Serving = await _availability.ServingAsync(pincode, cancellationToken);
            stock = await _availability.GetStockAsync(skuIds, cart.Serving, cancellationToken);
        }

        foreach (CartLine line in lines)
        {
            cart.Lines.Add(PriceLine(line, skus, products, prices, taxes, converter, stock, pincode, cart.IsInterState, now));
        }

        cart.Totals = GstCalculator.Sum(cart.Lines.Where(l => l.Amounts is not null).Select(l => l.Amounts!));
        if (lines.Count == 0)
        {
            cart.Problems.Add("Your cart is empty.");
        }

        if (string.IsNullOrEmpty(pincode))
        {
            cart.Problems.Add("Enter your delivery PIN code to check delivery.");
        }

        if (cart.SellerState is null)
        {
            cart.Problems.Add("The store is not taking orders yet. Please try again later.");
        }

        // Decision #2: the whole order ships from one warehouse. Lines that fit on
        // their own may still not fit together in any single place.
        if (stock is not null && cart.Lines.Count > 0 && cart.Lines.All(l => l.Problem is null && l.Sku is not null))
        {
            cart.Source = AllocationPlanner.PlanOrder(NeedsOf(cart.Lines), stock);
            if (cart.Source is { } source)
            {
                cart.Window = AllocationPlanner.Window(FinancialYear.IstDate(now), source.LeadTimeDays);
            }
            else
            {
                cart.Problems.Add(NotTogetherMessage(pincode!));
            }
        }

        return cart;
    }

    /// <summary>
    /// What the priced lines need, in each SKU's base unit, for planning the order.
    /// </summary>
    /// <param name="lines">Priced lines, each with its SKU.</param>
    /// <returns>The needs.</returns>
    public static IReadOnlyList<LineNeed> NeedsOf(IEnumerable<PricedLine> lines) =>
        lines.Select(l => new LineNeed(l.Sku!.Id, l.BaseQuantity)).ToList();

    /// <summary>
    /// What the customer is told when every item is available, but not all from one place.
    /// Says nothing about warehouses (P1).
    /// </summary>
    /// <param name="pincode">Delivery PIN code.</param>
    /// <returns>The message.</returns>
    public static string NotTogetherMessage(string pincode) =>
        $"These items cannot all be delivered together to PIN code {pincode} right now. Lower a quantity or remove an item, and order it separately.";

    /// <summary>
    /// Prices one line and checks it can be supplied.
    /// </summary>
    /// <param name="line">Line.</param>
    /// <param name="skus">SKUs by id.</param>
    /// <param name="products">Products by id.</param>
    /// <param name="prices">Customer prices by SKU id.</param>
    /// <param name="taxes">GST rates.</param>
    /// <param name="converter">Unit conversion.</param>
    /// <param name="stock">Free stock of serving warehouses by SKU, or null without a PIN code.</param>
    /// <param name="pincode">Delivery PIN code.</param>
    /// <param name="isInterState">True when IGST applies.</param>
    /// <param name="now">Instant prices and rates must be in force.</param>
    /// <returns>The priced line.</returns>
    private static PricedLine PriceLine(
        CartLine line,
        IReadOnlyDictionary<Guid, Sku> skus,
        IReadOnlyDictionary<Guid, Product> products,
        IReadOnlyDictionary<Guid, ResolvedPrice> prices,
        TaxRateTable taxes,
        IUomConversionService converter,
        IReadOnlyDictionary<Guid, List<WarehouseStock>>? stock,
        string? pincode,
        bool isInterState,
        DateTime now)
    {
        var priced = new PricedLine { Line = line };
        if (!skus.TryGetValue(line.SkuId, out Sku? sku) || !sku.IsActive
            || !products.TryGetValue(sku.ProductId, out Product? product) || !product.IsActive)
        {
            priced.Problem = "This item is no longer sold. Remove it to continue.";
            return priced;
        }

        var units = new SkuUnits(sku.BaseUom, sku.Conversions);
        priced.Sku = sku;
        priced.Product = product;
        priced.Name = $"{product.Name} · {sku.VariantLabel}";
        priced.Units = converter.ReachableUnits(units).Select(u => u.Uom).ToList();

        if (!converter.TryConvert(new Quantity(line.Quantity, line.Uom), sku.BaseUom, units, out Quantity baseQuantity))
        {
            priced.Problem = $"This item cannot be ordered in {line.Uom}.";
            return priced;
        }

        priced.BaseQuantity = Quantity.Normalise(baseQuantity.Value);
        priced.Price = prices.GetValueOrDefault(sku.Id);
        priced.Tax = taxes.Find(product.HsnCode, now);
        if (priced.Price is null || priced.Tax is null
            || !converter.TryConvert(new Quantity(line.Quantity, line.Uom), priced.Price.Uom, units, out Quantity inPriceUnit)
            || !converter.TryConvert(new Quantity(1, line.Uom), priced.Price.Uom, units, out Quantity onePerLineUnit))
        {
            priced.Price = null;
            priced.Problem = "This item cannot be ordered online yet. Please contact us for a quote.";
            return priced;
        }

        decimal slabPrice = SlabPricing.UnitPriceFor(priced.Price.Slabs, inPriceUnit.Value);
        priced.UnitPrice = Money.RoundUnitPrice(slabPrice * onePerLineUnit.Value);
        priced.Amounts = GstCalculator.Compute(inPriceUnit.Value * slabPrice, priced.Tax.RatePercent, priced.Tax.CessPercent, isInterState);

        if (stock is null)
        {
            return priced;
        }

        List<WarehouseStock> sources = stock.GetValueOrDefault(sku.Id) ?? new List<WarehouseStock>();
        if (sources.Count == 0)
        {
            priced.Problem = $"Not delivered to PIN code {pincode}.";
            return priced;
        }

        // One order ships from one warehouse, so a line can have at most what the best-stocked single place holds.
        decimal available = AllocationPlanner.MostFromOnePlace(sources);
        if (available < priced.BaseQuantity)
        {
            priced.Problem = available <= 0
                ? $"Out of stock for PIN code {pincode}."
                : $"Only {InUnit(available, sku, line.Uom, units, converter)} can be delivered to PIN code {pincode} right now.";
        }

        return priced;
    }

    /// <summary>
    /// Shows a base-unit amount in the line's unit, rounded down, e.g. "12.5 TONNE".
    /// </summary>
    /// <param name="baseAmount">Amount in the base unit.</param>
    /// <param name="sku">SKU.</param>
    /// <param name="uom">Unit to show.</param>
    /// <param name="units">SKU's units.</param>
    /// <param name="converter">Unit conversion.</param>
    /// <returns>The text.</returns>
    private static string InUnit(decimal baseAmount, Sku sku, string uom, SkuUnits units, IUomConversionService converter)
    {
        Quantity shown = converter.TryConvert(new Quantity(baseAmount, sku.BaseUom), uom, units, out Quantity converted)
            ? new Quantity(Math.Floor(converted.Value * 10_000m) / 10_000m, converted.Uom)
            : new Quantity(baseAmount, sku.BaseUom);
        return $"{Quantity.Normalise(shown.Value).ToString("#,0.####", CultureInfo.InvariantCulture)} {shown.Uom}";
    }
}
