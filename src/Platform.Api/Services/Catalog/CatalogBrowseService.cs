using Google.Cloud.Firestore;
using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Mapping;
using Platform.Api.Repositories;
using Platform.Shared.Common;
using Platform.Shared.Dtos.Catalog;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Entities.Catalog;
using Platform.Shared.Units;

namespace Platform.Api.Services.Catalog;

/// <summary>
/// Read side of the catalogue: the category tree, the product list with
/// filters, product detail with SKUs, and unit conversion for a SKU.
/// </summary>
public interface ICatalogBrowseService
{
    /// <summary>
    /// Returns the whole category tree, sorted.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Top-level categories with their descendants.</returns>
    Task<IReadOnlyList<CategoryNodeDto>> GetCategoryTreeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists products, optionally under a category (including sub-categories),
    /// of one brand, and/or matching search words.
    /// </summary>
    /// <param name="request">Filters and paging.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One page of products with brand, category and variant labels.</returns>
    Task<PagedResult<ProductListItemDto>> GetProductsAsync(CatalogBrowseRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds one page of products (entities) for a category, brand and search,
    /// ordered by name. The storefront and the price grid build their own rows from it.
    /// </summary>
    /// <param name="request">Category, brand, search words and page.</param>
    /// <param name="activeOnly">True to leave out deactivated products (the storefront).</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The page of products.</returns>
    Task<PagedResult<Product>> FindProductsAsync(CatalogBrowseRequest request, bool activeOnly, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a product with its breadcrumb and SKUs, each SKU with every unit it can be expressed in.
    /// </summary>
    /// <param name="productId">Product id.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The product.</returns>
    /// <exception cref="NotFoundException">No such product.</exception>
    Task<ProductDetailDto> GetProductAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a quantity of one SKU between units (P7).
    /// </summary>
    /// <param name="skuId">SKU id.</param>
    /// <param name="quantity">Amount and unit to convert.</param>
    /// <param name="toUom">Target unit.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Both quantities.</returns>
    /// <exception cref="NotFoundException">No such SKU.</exception>
    /// <exception cref="FieldValidationException">The units cannot be converted for this SKU.</exception>
    Task<ConversionResultDto> ConvertAsync(Guid skuId, QuantityDto quantity, string toUom, CancellationToken cancellationToken = default);
}

/// <summary>
/// Firestore implementation of <see cref="ICatalogBrowseService"/>.
/// </summary>
/// <remarks>
/// Without search, filters and paging run in Firestore (indexed on
/// <c>category_path</c>, <c>brand_id</c>, <c>name</c>). With search, Firestore
/// finds products holding the longest search word (<c>search_terms</c>), and the
/// category filter and other words are applied to at most
/// <see cref="MaxSearchCandidates"/> results in memory — Firestore allows only
/// one array filter per query.
/// </remarks>
public sealed class CatalogBrowseService : ICatalogBrowseService
{
    /// <summary>Most products examined for one search.</summary>
    private const int MaxSearchCandidates = 300;

    private static readonly string NameField = FirestoreNaming.Field(nameof(Product.Name));
    private static readonly string BrandIdField = FirestoreNaming.Field(nameof(Product.BrandId));
    private static readonly string CategoryPathField = FirestoreNaming.Field(nameof(Product.CategoryPath));
    private static readonly string SearchTermsField = FirestoreNaming.Field(nameof(Product.SearchTerms));
    private static readonly string ProductIdField = FirestoreNaming.Field(nameof(Sku.ProductId));
    private static readonly string IsActiveField = FirestoreNaming.Field(nameof(Product.IsActive));

    private readonly IRepository<Category> _categories;
    private readonly IRepository<Product> _products;
    private readonly IRepository<Sku> _skus;
    private readonly IRepository<Brand> _brands;
    private readonly IUomConversionProvider _conversions;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="categories">Category data access.</param>
    /// <param name="products">Product data access.</param>
    /// <param name="skus">SKU data access.</param>
    /// <param name="brands">Brand data access.</param>
    /// <param name="conversions">Unit conversion for the organisation.</param>
    public CatalogBrowseService(
        IRepository<Category> categories,
        IRepository<Product> products,
        IRepository<Sku> skus,
        IRepository<Brand> brands,
        IUomConversionProvider conversions)
    {
        _categories = categories;
        _products = products;
        _skus = skus;
        _brands = brands;
        _conversions = conversions;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CategoryNodeDto>> GetCategoryTreeAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Category> all = await _categories.ListAsync(_categories.Query(), cancellationToken);
        var nodes = all.ToDictionary(c => c.Id, c => new CategoryNodeDto
        {
            Id = c.Id,
            Name = c.Name,
            ParentId = c.ParentId,
            DisplayOrder = c.DisplayOrder,
            IsActive = c.IsActive,
        });

        var roots = new List<CategoryNodeDto>();
        foreach (CategoryNodeDto node in nodes.Values)
        {
            if (node.ParentId is { } parentId && nodes.TryGetValue(parentId, out var parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        SortTree(roots);
        return roots;
    }

    /// <inheritdoc />
    public async Task<PagedResult<ProductListItemDto>> GetProductsAsync(CatalogBrowseRequest request, CancellationToken cancellationToken = default) =>
        await ToListItemsAsync(await FindProductsAsync(request, activeOnly: false, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public async Task<PagedResult<Product>> FindProductsAsync(CatalogBrowseRequest request, bool activeOnly, CancellationToken cancellationToken = default)
    {
        Query query = _products.Query();
        if (request.BrandId is { } brandId)
        {
            query = query.WhereEqualTo(BrandIdField, DocumentConverter.ToFirestoreValue(brandId));
        }

        List<string> words = string.IsNullOrWhiteSpace(request.Search) ? new() : SearchTerms.Query(request.Search);
        PagedResult<Product> page;
        if (words.Count == 0)
        {
            if (request.CategoryId is { } categoryId)
            {
                query = query.WhereArrayContains(CategoryPathField, DocumentConverter.ToFirestoreValue(categoryId));
            }

            if (activeOnly)
            {
                query = query.WhereEqualTo(IsActiveField, true);
            }

            page = await _products.GetPagedAsync(query.OrderBy(NameField), request, cancellationToken);
        }
        else
        {
            IReadOnlyList<Product> candidates = await _products.ListAsync(
                query.WhereArrayContains(SearchTermsField, words[0]).OrderBy(NameField).Limit(MaxSearchCandidates),
                cancellationToken);

            List<Product> matches = candidates
                .Where(p => words.Skip(1).All(p.SearchTerms.Contains))
                .Where(p => request.CategoryId is not { } cat || p.CategoryPath.Contains(cat))
                .Where(p => !activeOnly || p.IsActive)
                .ToList();

            page = new PagedResult<Product>
            {
                Items = matches.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList(),
                Page = request.Page,
                PageSize = request.PageSize,
                TotalCount = matches.Count,
            };
        }

        return page;
    }

    /// <inheritdoc />
    public async Task<ProductDetailDto> GetProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        Product product = await _products.GetByIdAsync(productId, cancellationToken) ?? throw new NotFoundException("Product");
        Brand? brand = await _brands.GetByIdAsync(product.BrandId, cancellationToken);
        var pathCategories = (await _categories.GetByIdsAsync(product.CategoryPath, cancellationToken)).ToDictionary(c => c.Id);
        IReadOnlyList<Sku> skus = await _skus.ListAsync(
            _skus.Query().WhereEqualTo(ProductIdField, DocumentConverter.ToFirestoreValue(product.Id)), cancellationToken);
        IUomConversionService converter = await _conversions.GetAsync(cancellationToken);

        return new ProductDetailDto
        {
            Name = product.Name,
            HsnCode = product.HsnCode,
            BrandId = product.BrandId,
            BrandName = brand?.Name ?? string.Empty,
            Breadcrumb = product.CategoryPath
                .Where(pathCategories.ContainsKey)
                .Select(id => new CategoryCrumbDto { Id = id, Name = pathCategories[id].Name })
                .ToList(),
            Skus = skus.OrderBy(s => s.Code, StringComparer.Ordinal).Select(s => ToSkuDto(s, converter)).ToList(),
            IsActive = product.IsActive,
        }.WithAuditFrom(product);
    }

    /// <inheritdoc />
    public async Task<ConversionResultDto> ConvertAsync(Guid skuId, QuantityDto quantity, string toUom, CancellationToken cancellationToken = default)
    {
        Sku sku = await _skus.GetByIdAsync(skuId, cancellationToken) ?? throw new NotFoundException("SKU");
        IUomConversionService converter = await _conversions.GetAsync(cancellationToken);

        if (!converter.TryConvert(new Quantity(quantity.Value, quantity.Uom), toUom, UnitsOf(sku), out Quantity result))
        {
            throw new FieldValidationException("To", $"{quantity.Uom.ToUpperInvariant()} cannot be converted to {toUom.ToUpperInvariant()} for {sku.Code}.");
        }

        return new ConversionResultDto
        {
            From = new QuantityDto { Value = quantity.Value, Uom = quantity.Uom.ToUpperInvariant() },
            To = new QuantityDto { Value = result.Value, Uom = result.Uom },
        };
    }

    /// <summary>
    /// The SKU's units in the form the conversion service expects.
    /// </summary>
    /// <param name="sku">SKU.</param>
    /// <returns>Base unit and conversions.</returns>
    private static SkuUnits UnitsOf(Sku sku) => new(sku.BaseUom, sku.Conversions);

    /// <summary>
    /// Builds a SKU's read model, including every unit it can be expressed in.
    /// </summary>
    /// <param name="sku">SKU.</param>
    /// <param name="converter">Conversion service.</param>
    /// <returns>The DTO.</returns>
    private static SkuDto ToSkuDto(Sku sku, IUomConversionService converter) => new SkuDto
    {
        Code = sku.Code,
        VariantLabel = sku.VariantLabel,
        BaseUom = sku.BaseUom,
        Conversions = sku.Conversions.Select(c => new SkuConversionDto { Uom = c.Uom, Factor = c.Factor }).ToList(),
        Units = converter.ReachableUnits(UnitsOf(sku))
            .Select(u => new UnitEquivalentDto { Uom = u.Uom, BaseUnitsPerOne = u.BaseUnitsPerOne })
            .ToList(),
        IsActive = sku.IsActive,
    }.WithAuditFrom(sku);

    /// <summary>
    /// Adds brand names, category names and variant labels to a page of products,
    /// reading only what that page needs.
    /// </summary>
    /// <param name="page">Page of products.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The page as list items.</returns>
    private async Task<PagedResult<ProductListItemDto>> ToListItemsAsync(PagedResult<Product> page, CancellationToken cancellationToken)
    {
        var brands = (await _brands.GetByIdsAsync(page.Items.Select(p => p.BrandId), cancellationToken)).ToDictionary(b => b.Id);
        var categories = (await _categories.GetByIdsAsync(page.Items.Select(p => p.CategoryId), cancellationToken)).ToDictionary(c => c.Id);

        var variants = new Dictionary<Guid, List<string>>();
        foreach (Guid[] chunk in page.Items.Select(p => p.Id).Chunk(30))
        {
            IReadOnlyList<Sku> skus = await _skus.ListAsync(
                _skus.Query().WhereIn(ProductIdField, chunk.Select(id => DocumentConverter.ToFirestoreValue(id))), cancellationToken);
            foreach (Sku sku in skus.Where(s => s.IsActive).OrderBy(s => s.Code, StringComparer.Ordinal))
            {
                (variants.TryGetValue(sku.ProductId, out var list) ? list : variants[sku.ProductId] = new()).Add(sku.VariantLabel);
            }
        }

        return new PagedResult<ProductListItemDto>
        {
            Items = page.Items.Select(p => new ProductListItemDto
            {
                Id = p.Id,
                Name = p.Name,
                BrandId = p.BrandId,
                BrandName = brands.TryGetValue(p.BrandId, out var b) ? b.Name : string.Empty,
                CategoryId = p.CategoryId,
                CategoryName = categories.TryGetValue(p.CategoryId, out var c) ? c.Name : string.Empty,
                HsnCode = p.HsnCode,
                Variants = variants.TryGetValue(p.Id, out var v) ? v : new(),
                IsActive = p.IsActive,
            }).ToList(),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
        };
    }

    /// <summary>
    /// Sorts every level of the tree by display order, then name.
    /// </summary>
    /// <param name="nodes">Nodes to sort in place, recursively.</param>
    private static void SortTree(List<CategoryNodeDto> nodes)
    {
        nodes.Sort((a, b) => a.DisplayOrder != b.DisplayOrder
            ? a.DisplayOrder.CompareTo(b.DisplayOrder)
            : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        foreach (CategoryNodeDto node in nodes)
        {
            SortTree(node.Children);
        }
    }
}
