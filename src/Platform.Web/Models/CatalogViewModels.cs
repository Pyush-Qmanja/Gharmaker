using Microsoft.AspNetCore.Mvc.Rendering;
using Platform.Shared.Dtos.Catalog;
using Platform.Shared.Dtos.Inventory;

namespace Platform.Web.Models;

/// <summary>
/// The catalogue browse page: category tree, filters and one page of products.
/// </summary>
public sealed class CatalogIndexViewModel
{
    /// <summary>Category tree for the side panel.</summary>
    public IReadOnlyList<CategoryNodeDto> Categories { get; init; } = Array.Empty<CategoryNodeDto>();

    /// <summary>Selected category (products include its sub-categories), if any.</summary>
    public Guid? CategoryId { get; init; }

    /// <summary>Selected brand, if any.</summary>
    public Guid? BrandId { get; init; }

    /// <summary>Brand drop-down options.</summary>
    public IReadOnlyList<SelectListItem> BrandOptions { get; init; } = Array.Empty<SelectListItem>();

    /// <summary>Products page, reusing the shared pager and search box.</summary>
    public IListViewModel Products { get; init; } = default!;

    /// <summary>True when the user may import (shows the Import button).</summary>
    public bool CanImport { get; init; }

    /// <summary>Name of the selected category, for the heading.</summary>
    public string? CategoryName { get; init; }
}

/// <summary>
/// The product page: product with SKUs, plus the result of a unit conversion
/// the user asked for, if any.
/// </summary>
public sealed class ProductPageViewModel
{
    /// <summary>The product.</summary>
    public ProductDetailDto Product { get; init; } = default!;

    /// <summary>SKU the converter was used on, if any.</summary>
    public Guid? ConvertSkuId { get; init; }

    /// <summary>Amount entered in the converter.</summary>
    public decimal? ConvertValue { get; init; }

    /// <summary>Unit converted from.</summary>
    public string? ConvertFrom { get; init; }

    /// <summary>Unit converted to.</summary>
    public string? ConvertTo { get; init; }

    /// <summary>Successful result, if any.</summary>
    public ConversionResultDto? Conversion { get; init; }

    /// <summary>Why the conversion failed, if it did.</summary>
    public string? ConversionError { get; init; }

    /// <summary>
    /// Stock of each variant in the warehouses the user can see, keyed by SKU id;
    /// null when the user has no stock access, so the page leaves stock out.
    /// </summary>
    public IReadOnlyDictionary<Guid, SkuStockDto>? Stock { get; init; }

    /// <summary>True when the user may post goods receipts somewhere.</summary>
    public bool CanReceive { get; init; }

    /// <summary>True when the user may load opening stock somewhere.</summary>
    public bool CanLoadOpening { get; init; }

    /// <summary>Every warehouse row of every variant, for the "where it is" table.</summary>
    public IEnumerable<StockBalanceDto> StockRows =>
        Stock?.Values.SelectMany(s => s.Balances).OrderBy(b => b.WarehouseCode).ThenBy(b => b.SkuCode)
        ?? Enumerable.Empty<StockBalanceDto>();
}

/// <summary>
/// One level of the category tree partial.
/// </summary>
/// <param name="Nodes">Categories at this level.</param>
/// <param name="SelectedId">Currently selected category, highlighted.</param>
/// <param name="BrandId">Brand filter to keep when switching category.</param>
/// <param name="IsRoot">True for the top level, which also shows "All products".</param>
public sealed record CategoryTreeModel(IReadOnlyList<CategoryNodeDto> Nodes, Guid? SelectedId, Guid? BrandId, bool IsRoot);
