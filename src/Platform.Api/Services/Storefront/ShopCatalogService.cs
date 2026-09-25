using Microsoft.Extensions.Caching.Memory;
using Platform.Api.Common;
using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Repositories;
using Platform.Api.Services.Catalog;
using Platform.Api.Services.Inventory;
using Platform.Api.Services.Pricing;
using Platform.Shared.Commerce;
using Platform.Shared.Common;
using Platform.Shared.Dtos.Catalog;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Storefront;
using Platform.Shared.Entities.Catalog;
using Platform.Shared.Entities.Pricing;
using Platform.Shared.Entities.Sales;
using Platform.Shared.Units;

namespace Platform.Api.Services.Storefront;

/// <summary>
/// The store's catalogue: menu, brands, product listings and product pages,
/// each with the visitor's or customer's price and, when a PIN code is given,
/// availability as one number and delivery as a date range (P1). Products
/// with no stock stay listed as out of stock (decision 4).
/// </summary>
public interface IShopCatalogService
{
    /// <summary>
    /// Returns the active category tree.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Top-level categories with their children.</returns>
    Task<List<ShopCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the active brands.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Brands in display order.</returns>
    Task<List<ShopBrandDto>> GetBrandsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one page of active products.
    /// </summary>
    /// <param name="request">Category, brand, search, PIN code and page.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Product cards.</returns>
    Task<PagedResult<ShopProductCardDto>> GetProductsAsync(ShopProductRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns an active product with its variants on sale.
    /// </summary>
    /// <param name="id">Product id.</param>
    /// <param name="pincode">Customer's PIN code, if known.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The product page.</returns>
    Task<ShopProductDto> GetProductAsync(Guid id, string? pincode, CancellationToken cancellationToken = default);
}

/// <summary>
/// Firestore-backed <see cref="IShopCatalogService"/>.
/// </summary>
public sealed class ShopCatalogService : IShopCatalogService
{
    /// <summary>Firestore's limit on values in one "in" filter.</summary>
    private const int MaxInValues = 30;

    /// <summary>How long the category menu and brand list are served from memory.</summary>
    private static readonly TimeSpan MenuCacheFor = TimeSpan.FromMinutes(2);

    private static readonly string ProductIdField = FirestoreNaming.Field(nameof(Sku.ProductId));

    private readonly ICatalogBrowseService _catalog;
    private readonly IRepository<Product> _products;
    private readonly IRepository<Sku> _skus;
    private readonly IRepository<Brand> _brands;
    private readonly IRepository<Category> _categories;
    private readonly IPriceResolver _prices;
    private readonly ITaxRateProvider _taxRates;
    private readonly IAvailabilityService _availability;
    private readonly IUomConversionProvider _conversions;
    private readonly ICustomerContext _customer;
    private readonly TimeProvider _timeProvider;
    private readonly IMemoryCache _cache;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="catalog">Finds products.</param>
    /// <param name="products">Product data access.</param>
    /// <param name="skus">SKU data access.</param>
    /// <param name="brands">Brand data access.</param>
    /// <param name="categories">Category data access.</param>
    /// <param name="prices">Resolves prices.</param>
    /// <param name="taxRates">GST rates.</param>
    /// <param name="availability">Stock of serving warehouses.</param>
    /// <param name="conversions">Unit conversion (P7).</param>
    /// <param name="customer">Signed-in customer, if any.</param>
    /// <param name="timeProvider">Clock.</param>
    /// <param name="cache">Holds the category menu and brand list briefly.</param>
    /// <param name="currentUser">The store's organisation, part of the cache key.</param>
    public ShopCatalogService(
        ICatalogBrowseService catalog,
        IRepository<Product> products,
        IRepository<Sku> skus,
        IRepository<Brand> brands,
        IRepository<Category> categories,
        IPriceResolver prices,
        ITaxRateProvider taxRates,
        IAvailabilityService availability,
        IUomConversionProvider conversions,
        ICustomerContext customer,
        TimeProvider timeProvider,
        IMemoryCache cache,
        ICurrentUser currentUser)
    {
        _catalog = catalog;
        _products = products;
        _skus = skus;
        _brands = brands;
        _categories = categories;
        _prices = prices;
        _taxRates = taxRates;
        _availability = availability;
        _conversions = conversions;
        _customer = customer;
        _timeProvider = timeProvider;
        _cache = cache;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public Task<List<ShopCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        CachedAsync("categories", async () => ToShopTree(await _catalog.GetCategoryTreeAsync(cancellationToken)));

    /// <inheritdoc />
    public Task<List<ShopBrandDto>> GetBrandsAsync(CancellationToken cancellationToken = default) =>
        CachedAsync("brands", async () => (await _brands.ListAsync(_brands.Query(), cancellationToken))
            .Where(b => b.IsActive)
            .OrderBy(b => b.DisplayOrder)
            .ThenBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
            .Select(b => new ShopBrandDto { Id = b.Id, Name = b.Name })
            .ToList());

    /// <summary>
    /// Serves a list that is the same for every shopper from a short cache, so
    /// every page view does not re-read the whole category or brand collection.
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    /// <param name="name">What is cached.</param>
    /// <param name="load">Reads it from Firestore.</param>
    /// <returns>The list.</returns>
    private async Task<List<T>> CachedAsync<T>(string name, Func<Task<List<T>>> load)
    {
        string key = $"storefront:{name}:{_currentUser.OrgId}";
        if (_cache.TryGetValue(key, out List<T>? cached) && cached is not null)
        {
            return cached;
        }

        List<T> fresh = await load();
        return _cache.Set(key, fresh, MenuCacheFor);
    }

    /// <inheritdoc />
    public async Task<PagedResult<ShopProductCardDto>> GetProductsAsync(ShopProductRequest request, CancellationToken cancellationToken = default)
    {
        PagedResult<Product> page = await _catalog.FindProductsAsync(
            new CatalogBrowseRequest
            {
                CategoryId = request.CategoryId,
                BrandId = request.BrandId,
                Search = request.Search,
                Page = request.Page,
                PageSize = request.PageSize,
            },
            activeOnly: true,
            cancellationToken);

        List<Sku> skus = await ActiveSkusAsync(page.Items.Select(p => p.Id), cancellationToken);
        Offer offer = await OfferAsync(skus, request.Pincode, cancellationToken);
        var brands = (await _brands.GetByIdsAsync(page.Items.Select(p => p.BrandId), cancellationToken)).ToDictionary(b => b.Id, b => b.Name);
        var categories = (await _categories.GetByIdsAsync(page.Items.Select(p => p.CategoryId), cancellationToken)).ToDictionary(c => c.Id, c => c.Name);

        return new PagedResult<ShopProductCardDto>
        {
            Items = page.Items.Select(product =>
            {
                List<Sku> variants = skus.Where(s => s.ProductId == product.Id).OrderBy(s => s.Code, StringComparer.Ordinal).ToList();
                return new ShopProductCardDto
                {
                    Id = product.Id,
                    Name = product.Name,
                    BrandName = brands.GetValueOrDefault(product.BrandId, string.Empty),
                    CategoryName = categories.GetValueOrDefault(product.CategoryId, string.Empty),
                    Variants = variants.Select(s => s.VariantLabel).ToList(),
                    FromPrice = FromPrice(product, variants, offer),
                    Status = BestStatus(product, variants, offer),
                };
            }).ToList(),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
        };
    }

    /// <inheritdoc />
    public async Task<ShopProductDto> GetProductAsync(Guid id, string? pincode, CancellationToken cancellationToken = default)
    {
        Product product = await _products.GetByIdAsync(id, cancellationToken) is { IsActive: true } found
            ? found
            : throw new NotFoundException("Product");
        List<Sku> skus = await ActiveSkusAsync(new[] { product.Id }, cancellationToken);
        Offer offer = await OfferAsync(skus, pincode, cancellationToken);
        Brand? brand = await _brands.GetByIdAsync(product.BrandId, cancellationToken);
        var path = (await _categories.GetByIdsAsync(product.CategoryPath, cancellationToken)).ToDictionary(c => c.Id);

        return new ShopProductDto
        {
            Id = product.Id,
            Name = product.Name,
            BrandName = brand?.Name ?? string.Empty,
            HsnCode = product.HsnCode,
            Breadcrumb = product.CategoryPath
                .Where(path.ContainsKey)
                .Select(c => new ShopCrumbDto { Id = c, Name = path[c].Name })
                .ToList(),
            Pincode = offer.Pincode,
            Variants = skus.OrderBy(s => s.Code, StringComparer.Ordinal).Select(sku => ToVariant(product, sku, offer)).ToList(),
        };
    }

    /// <summary>
    /// Maps one variant with its price, availability and delivery.
    /// </summary>
    /// <param name="product">Product.</param>
    /// <param name="sku">Variant.</param>
    /// <param name="offer">Prices, rates and stock.</param>
    /// <returns>The DTO.</returns>
    private static ShopVariantDto ToVariant(Product product, Sku sku, Offer offer)
    {
        ResolvedPrice? price = offer.PriceOf(product, sku);
        TaxRate? tax = offer.Taxes.Find(product.HsnCode, offer.Now);
        var variant = new ShopVariantDto
        {
            SkuId = sku.Id,
            Code = sku.Code,
            VariantLabel = sku.VariantLabel,
            BaseUom = sku.BaseUom,
            Units = offer.Converter.ReachableUnits(new SkuUnits(sku.BaseUom, sku.Conversions)).Select(u => u.Uom).ToList(),
            Price = price is null || tax is null ? null : new ShopPriceDto
            {
                Uom = price.Uom,
                Currency = price.Currency,
                Slabs = price.Slabs.Select(s => new ShopSlabDto { MinQuantity = s.MinQuantity, UnitPrice = s.UnitPrice }).ToList(),
                TaxRatePercent = tax.RatePercent,
                CessPercent = tax.CessPercent,
            },
            Availability = new ShopAvailabilityDto { Status = offer.StatusOf(sku), Uom = sku.BaseUom },
        };

        if (offer.StockOf(sku) is { } stock)
        {
            // An order ships from one warehouse (decision #2): show what one order can get, and its date.
            variant.Availability.Quantity = Quantity.Normalise(AllocationPlanner.MostFromOnePlace(stock));
            if (AllocationPlanner.FastestLeadTime(stock) is { } leadTimeDays)
            {
                var (earliest, latest) = AllocationPlanner.Window(offer.Today, leadTimeDays);
                variant.Delivery = new ShopDeliveryDto { EarliestOn = earliest, LatestOn = latest };
            }
        }

        return variant;
    }

    /// <summary>
    /// Works out the lowest starting price of a product's variants, in the unit of its first priced variant.
    /// </summary>
    /// <param name="product">Product.</param>
    /// <param name="variants">Its active SKUs.</param>
    /// <param name="offer">Prices and rates.</param>
    /// <returns>The "from" price, or null when nothing is sellable.</returns>
    private static ShopFromPriceDto? FromPrice(Product product, IReadOnlyList<Sku> variants, Offer offer)
    {
        if (offer.Taxes.Find(product.HsnCode, offer.Now) is null)
        {
            return null;
        }

        var priced = variants.Select(s => offer.PriceOf(product, s)).Where(p => p is not null).Select(p => p!).ToList();
        if (priced.Count == 0)
        {
            return null;
        }

        string uom = priced[0].Uom;
        ResolvedPrice cheapest = priced.Where(p => p.Uom == uom).MinBy(p => p.Slabs.Min(s => s.UnitPrice))!;
        return new ShopFromPriceDto { UnitPrice = cheapest.Slabs.Min(s => s.UnitPrice), Uom = uom, Currency = cheapest.Currency };
    }

    /// <summary>
    /// Works out the best availability among a product's variants.
    /// </summary>
    /// <param name="product">Product.</param>
    /// <param name="variants">Its active SKUs.</param>
    /// <param name="offer">Stock.</param>
    /// <returns>In stock when any variant is; otherwise the most hopeful status.</returns>
    private static ShopStockStatus BestStatus(Product product, IReadOnlyList<Sku> variants, Offer offer)
    {
        if (offer.Pincode is null)
        {
            return ShopStockStatus.CheckPincode;
        }

        var statuses = variants.Select(offer.StatusOf).ToList();
        return statuses.Contains(ShopStockStatus.InStock) ? ShopStockStatus.InStock
            : offer.Serving.Count > 0 ? ShopStockStatus.OutOfStock
            : ShopStockStatus.NotDeliverable;
    }

    /// <summary>
    /// Loads the active SKUs of some products.
    /// </summary>
    /// <param name="productIds">Products.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Active SKUs.</returns>
    private async Task<List<Sku>> ActiveSkusAsync(IEnumerable<Guid> productIds, CancellationToken cancellationToken)
    {
        var skus = new List<Sku>();
        foreach (Guid[] chunk in productIds.Distinct().Chunk(MaxInValues))
        {
            skus.AddRange(await _skus.ListAsync(
                _skus.Query().WhereIn(ProductIdField, chunk.Select(id => DocumentConverter.ToFirestoreValue(id))), cancellationToken));
        }

        return skus.Where(s => s.IsActive).ToList();
    }

    /// <summary>
    /// Gathers everything needed to show SKUs: prices for the caller, GST rates, unit conversion and stock for a PIN code.
    /// </summary>
    /// <param name="skus">SKUs shown.</param>
    /// <param name="pincode">PIN code, if known.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The offer.</returns>
    private async Task<Offer> OfferAsync(IReadOnlyList<Sku> skus, string? pincode, CancellationToken cancellationToken)
    {
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        Customer? customer = await _customer.FindAsync(cancellationToken);
        List<Guid> ids = skus.Select(s => s.Id).ToList();
        var offer = new Offer(
            now,
            FinancialYear.IstDate(now),
            await _prices.ResolveAsync(ids, customer, now, cancellationToken),
            await _taxRates.GetAsync(cancellationToken),
            await _conversions.GetAsync(cancellationToken),
            string.IsNullOrWhiteSpace(pincode) ? null : pincode.Trim());

        if (offer.Pincode is not null)
        {
            offer.Serving = await _availability.ServingAsync(offer.Pincode, cancellationToken);
            offer.Stock = await _availability.GetStockAsync(ids, offer.Serving, cancellationToken);
        }

        return offer;
    }

    /// <summary>
    /// Keeps active categories only.
    /// </summary>
    /// <param name="nodes">Category nodes.</param>
    /// <returns>Storefront nodes.</returns>
    private static List<ShopCategoryDto> ToShopTree(IEnumerable<CategoryNodeDto> nodes) =>
        nodes.Where(n => n.IsActive)
            .Select(n => new ShopCategoryDto { Id = n.Id, Name = n.Name, Children = ToShopTree(n.Children) })
            .ToList();

    /// <summary>
    /// Prices, rates, conversion and stock gathered for one request.
    /// </summary>
    /// <param name="Now">Instant prices must be in force.</param>
    /// <param name="Today">Today in IST.</param>
    /// <param name="Prices">Resolved prices by SKU id.</param>
    /// <param name="Taxes">GST rates.</param>
    /// <param name="Converter">Unit conversion.</param>
    /// <param name="Pincode">PIN code, if known.</param>
    private sealed record Offer(
        DateTime Now,
        DateOnly Today,
        IReadOnlyDictionary<Guid, ResolvedPrice> Prices,
        TaxRateTable Taxes,
        IUomConversionService Converter,
        string? Pincode)
    {
        /// <summary>Warehouses delivering to the PIN code (internal).</summary>
        public IReadOnlyList<ServingWarehouse> Serving { get; set; } = Array.Empty<ServingWarehouse>();

        /// <summary>Free stock by SKU (internal).</summary>
        public IReadOnlyDictionary<Guid, List<WarehouseStock>>? Stock { get; set; }

        /// <summary>
        /// Returns a SKU's price when it can be sold (it also needs a GST rate, checked by the caller).
        /// </summary>
        /// <param name="product">Product.</param>
        /// <param name="sku">SKU.</param>
        /// <returns>The price, or null.</returns>
        public ResolvedPrice? PriceOf(Product product, Sku sku) => Prices.GetValueOrDefault(sku.Id);

        /// <summary>
        /// Returns a SKU's stock in serving warehouses.
        /// </summary>
        /// <param name="sku">SKU.</param>
        /// <returns>The stock, or null without a PIN code.</returns>
        public List<WarehouseStock>? StockOf(Sku sku) => Stock?.GetValueOrDefault(sku.Id);

        /// <summary>
        /// Works out one SKU's status for the PIN code.
        /// </summary>
        /// <param name="sku">SKU.</param>
        /// <returns>The status.</returns>
        public ShopStockStatus StatusOf(Sku sku) =>
            Pincode is null ? ShopStockStatus.CheckPincode
            : Serving.Count == 0 ? ShopStockStatus.NotDeliverable
            : StockOf(sku)?.Sum(s => s.Available) > 0 ? ShopStockStatus.InStock
            : ShopStockStatus.OutOfStock;
    }
}
