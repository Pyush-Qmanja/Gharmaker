using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Mapping;
using Platform.Api.Repositories;
using Platform.Api.Services.Catalog;
using Platform.Shared.Commerce;
using Platform.Shared.Dtos.Catalog;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Pricing;
using Platform.Shared.Entities.Catalog;
using Platform.Shared.Entities.Pricing;
using Platform.Shared.Units;

namespace Platform.Api.Services.Pricing;

/// <summary>
/// Prices of SKUs in a price list: the grid staff edit, the history of one
/// SKU, and setting a new price. A new price is always a new dated row (P8).
/// </summary>
public interface IPriceService
{
    /// <summary>
    /// Returns one page of products with each SKU's current and upcoming price in a list.
    /// </summary>
    /// <param name="request">List, category, search and page.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The page.</returns>
    Task<PagedResult<PriceGridProductDto>> GetGridAsync(PriceGridRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every price a SKU has had in a list, newest first.
    /// </summary>
    /// <param name="priceListId">List.</param>
    /// <param name="skuId">SKU.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The rows.</returns>
    Task<List<SkuPriceDto>> GetHistoryAsync(Guid priceListId, Guid skuId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a SKU's price in a list from a date.
    /// </summary>
    /// <param name="request">Price.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The new price row.</returns>
    Task<SkuPriceDto> SetAsync(SetSkuPriceRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Firestore-backed <see cref="IPriceService"/>.
/// </summary>
public sealed class PriceService : IPriceService
{
    /// <summary>How far in the past a "now" start may be (clock drift between UI and API).</summary>
    private static readonly TimeSpan PastTolerance = TimeSpan.FromMinutes(5);

    /// <summary>Firestore's limit on values in one "in" filter.</summary>
    private const int MaxInValues = 30;

    private static readonly string ProductIdField = FirestoreNaming.Field(nameof(Sku.ProductId));

    private readonly IRepository<PriceList> _lists;
    private readonly IRepository<SkuPrice> _prices;
    private readonly IRepository<Sku> _skus;
    private readonly IRepository<Brand> _brands;
    private readonly ICatalogBrowseService _catalog;
    private readonly IPriceResolver _resolver;
    private readonly ITaxRateProvider _taxRates;
    private readonly IUomConversionProvider _conversions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="lists">Price list data access.</param>
    /// <param name="prices">Price data access.</param>
    /// <param name="skus">SKU data access.</param>
    /// <param name="brands">Brand data access, for names in the grid.</param>
    /// <param name="catalog">Finds products for the grid.</param>
    /// <param name="resolver">Reads price rows.</param>
    /// <param name="taxRates">GST rates, shown next to each product.</param>
    /// <param name="conversions">Unit conversion (P7).</param>
    /// <param name="unitOfWork">Commits a new price.</param>
    /// <param name="timeProvider">Clock.</param>
    public PriceService(
        IRepository<PriceList> lists,
        IRepository<SkuPrice> prices,
        IRepository<Sku> skus,
        IRepository<Brand> brands,
        ICatalogBrowseService catalog,
        IPriceResolver resolver,
        ITaxRateProvider taxRates,
        IUomConversionProvider conversions,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _lists = lists;
        _prices = prices;
        _skus = skus;
        _brands = brands;
        _catalog = catalog;
        _resolver = resolver;
        _taxRates = taxRates;
        _conversions = conversions;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<PagedResult<PriceGridProductDto>> GetGridAsync(PriceGridRequest request, CancellationToken cancellationToken = default)
    {
        PriceList list = await LoadListAsync(request.PriceListId, cancellationToken);
        PagedResult<Product> page = await _catalog.FindProductsAsync(
            new CatalogBrowseRequest { CategoryId = request.CategoryId, Search = request.Search, Page = request.Page, PageSize = request.PageSize },
            activeOnly: false,
            cancellationToken);

        var skus = new List<Sku>();
        foreach (Guid[] chunk in page.Items.Select(p => p.Id).Chunk(MaxInValues))
        {
            skus.AddRange(await _skus.ListAsync(
                _skus.Query().WhereIn(ProductIdField, chunk.Select(id => DocumentConverter.ToFirestoreValue(id))), cancellationToken));
        }

        ILookup<Guid, SkuPrice> rows = (await _resolver.ListRowsAsync(list.Id, skus.Select(s => s.Id), cancellationToken)).ToLookup(r => r.SkuId);
        var brands = (await _brands.GetByIdsAsync(page.Items.Select(p => p.BrandId), cancellationToken)).ToDictionary(b => b.Id, b => b.Name);
        TaxRateTable taxes = await _taxRates.GetAsync(cancellationToken);
        IUomConversionService converter = await _conversions.GetAsync(cancellationToken);
        DateTime now = Now();

        return new PagedResult<PriceGridProductDto>
        {
            Items = page.Items.Select(product => new PriceGridProductDto
            {
                ProductId = product.Id,
                Name = product.Name,
                BrandName = brands.GetValueOrDefault(product.BrandId, string.Empty),
                HsnCode = product.HsnCode,
                TaxRatePercent = taxes.Find(product.HsnCode, now)?.RatePercent,
                Skus = skus.Where(s => s.ProductId == product.Id)
                    .OrderBy(s => s.Code, StringComparer.Ordinal)
                    .Select(sku =>
                    {
                        SkuPrice? current = Dated.InForce(rows[sku.Id], r => r.ValidFrom, now);
                        SkuPrice? upcoming = Dated.Upcoming(rows[sku.Id], r => r.ValidFrom, now);
                        return new PriceGridSkuDto
                        {
                            SkuId = sku.Id,
                            Code = sku.Code,
                            VariantLabel = sku.VariantLabel,
                            BaseUom = sku.BaseUom,
                            Units = converter.ReachableUnits(new SkuUnits(sku.BaseUom, sku.Conversions)).Select(u => u.Uom).ToList(),
                            IsActive = sku.IsActive,
                            Current = current is null ? null : ToDto(current, isCurrent: true),
                            Upcoming = upcoming is null ? null : ToDto(upcoming, isCurrent: false),
                        };
                    })
                    .ToList(),
            }).ToList(),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
        };
    }

    /// <inheritdoc />
    public async Task<List<SkuPriceDto>> GetHistoryAsync(Guid priceListId, Guid skuId, CancellationToken cancellationToken = default)
    {
        await LoadListAsync(priceListId, cancellationToken);
        IReadOnlyList<SkuPrice> rows = await _resolver.ListRowsAsync(priceListId, new[] { skuId }, cancellationToken);
        SkuPrice? current = Dated.InForce(rows, r => r.ValidFrom, Now());
        return rows.OrderByDescending(r => r.ValidFrom).Select(r => ToDto(r, ReferenceEquals(r, current))).ToList();
    }

    /// <inheritdoc />
    public async Task<SkuPriceDto> SetAsync(SetSkuPriceRequest request, CancellationToken cancellationToken = default)
    {
        PriceList list = await LoadListAsync(request.PriceListId, cancellationToken);
        if (!list.IsActive)
        {
            throw new BusinessRuleException("This price list is deactivated. Restore it before setting prices.");
        }

        Sku sku = await _skus.GetByIdAsync(request.SkuId, cancellationToken) ?? throw new NotFoundException("SKU");
        IUomConversionService converter = await _conversions.GetAsync(cancellationToken);
        string? unit = converter.ReachableUnits(new SkuUnits(sku.BaseUom, sku.Conversions))
            .Select(u => u.Uom)
            .FirstOrDefault(u => string.Equals(u, request.Uom, StringComparison.OrdinalIgnoreCase));
        if (unit is null)
        {
            throw new FieldValidationException(nameof(SetSkuPriceRequest.Uom), $"{sku.Code} cannot be priced in {request.Uom}.");
        }

        DateTime now = Now();
        DateTime validFrom = request.ValidFrom?.ToUniversalTime() ?? now;
        if (validFrom < now - PastTolerance)
        {
            throw new FieldValidationException(nameof(SetSkuPriceRequest.ValidFrom),
                "A price cannot start in the past: orders already placed keep their price. Leave it empty to start now.");
        }

        var price = new SkuPrice
        {
            PriceListId = list.Id,
            SkuId = sku.Id,
            ProductId = sku.ProductId,
            Uom = unit,
            Currency = list.Currency,
            Slabs = SlabPricing.Sorted(request.Slabs.Select(s => new PriceSlab
            {
                MinQuantity = s.MinQuantity,
                UnitPrice = Shared.Common.Money.RoundUnitPrice(s.UnitPrice),
            })),
            ValidFrom = validFrom < now ? now : validFrom,
            Remarks = request.Remarks,
        };
        _prices.Add(price);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(price, isCurrent: price.ValidFrom <= Now());
    }

    /// <summary>
    /// Loads a price list or fails with 404.
    /// </summary>
    /// <param name="id">List id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The list.</returns>
    private async Task<PriceList> LoadListAsync(Guid id, CancellationToken cancellationToken) =>
        await _lists.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Price list");

    /// <summary>
    /// Maps a price row.
    /// </summary>
    /// <param name="price">Row.</param>
    /// <param name="isCurrent">True when in force now.</param>
    /// <returns>The DTO.</returns>
    private static SkuPriceDto ToDto(SkuPrice price, bool isCurrent) => new SkuPriceDto
    {
        PriceListId = price.PriceListId,
        SkuId = price.SkuId,
        Uom = price.Uom,
        Currency = price.Currency,
        Slabs = price.Slabs.Select(s => new PriceSlabDto { MinQuantity = s.MinQuantity, UnitPrice = s.UnitPrice }).ToList(),
        ValidFrom = price.ValidFrom,
        Remarks = price.Remarks,
        IsCurrent = isCurrent,
    }.WithAuditFrom(price);

    /// <summary>
    /// Current UTC instant.
    /// </summary>
    /// <returns>Now.</returns>
    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;
}
