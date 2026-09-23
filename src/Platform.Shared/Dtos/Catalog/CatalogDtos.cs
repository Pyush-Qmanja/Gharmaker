using Platform.Shared.Dtos.Common;

namespace Platform.Shared.Dtos.Catalog;

/// <summary>
/// One node of the category tree, with its children.
/// </summary>
public class CategoryNodeDto
{
    /// <summary>Category id.</summary>
    public Guid Id { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Parent id; null at the top level.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Sort position among siblings.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>False once deactivated.</summary>
    public bool IsActive { get; set; }

    /// <summary>Sub-categories, sorted.</summary>
    public List<CategoryNodeDto> Children { get; set; } = new();
}

/// <summary>
/// Query for <c>GET /api/catalog/products</c>: paging and search plus optional
/// category (includes everything beneath it) and brand filters.
/// </summary>
public class CatalogBrowseRequest : PagedRequest
{
    /// <summary>Show products in this category or any of its sub-categories.</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Show only this brand's products.</summary>
    public Guid? BrandId { get; set; }
}

/// <summary>
/// One product in the browse list.
/// </summary>
public class ProductListItemDto
{
    /// <summary>Product id.</summary>
    public Guid Id { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Brand id.</summary>
    public Guid BrandId { get; set; }

    /// <summary>Brand name.</summary>
    public string BrandName { get; set; } = string.Empty;

    /// <summary>Category id.</summary>
    public Guid CategoryId { get; set; }

    /// <summary>Category name.</summary>
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>GST HSN code.</summary>
    public string HsnCode { get; set; } = string.Empty;

    /// <summary>Variant labels of the product's active SKUs.</summary>
    public List<string> Variants { get; set; } = new();

    /// <summary>False once deactivated.</summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// A category in a product's breadcrumb.
/// </summary>
public class CategoryCrumbDto
{
    /// <summary>Category id.</summary>
    public Guid Id { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// A unit a SKU can be expressed in and how many base units one of it holds.
/// </summary>
public class UnitEquivalentDto
{
    /// <summary>Unit code.</summary>
    public string Uom { get; set; } = string.Empty;

    /// <summary>Base units in one of <see cref="Uom"/>.</summary>
    public decimal BaseUnitsPerOne { get; set; }
}

/// <summary>
/// "1 <see cref="Uom"/> = <see cref="Factor"/> base units" for one SKU.
/// </summary>
public class SkuConversionDto
{
    /// <summary>Unit code.</summary>
    public string Uom { get; set; } = string.Empty;

    /// <summary>Base units per one <see cref="Uom"/>.</summary>
    public decimal Factor { get; set; }
}

/// <summary>
/// Read model of a SKU, including every unit it can be expressed in.
/// </summary>
public class SkuDto : EntityDto
{
    /// <summary>Unique SKU code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>What distinguishes this variant.</summary>
    public string VariantLabel { get; set; } = string.Empty;

    /// <summary>Unit stock is counted in.</summary>
    public string BaseUom { get; set; } = string.Empty;

    /// <summary>The SKU's own conversions, as entered.</summary>
    public List<SkuConversionDto> Conversions { get; set; } = new();

    /// <summary>Every reachable unit (own conversions plus standard ones), base unit first.</summary>
    public List<UnitEquivalentDto> Units { get; set; } = new();

    /// <summary>False once deactivated.</summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// A product with its breadcrumb and all its SKUs.
/// </summary>
public class ProductDetailDto : EntityDto
{
    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>GST HSN code.</summary>
    public string HsnCode { get; set; } = string.Empty;

    /// <summary>Brand id.</summary>
    public Guid BrandId { get; set; }

    /// <summary>Brand name.</summary>
    public string BrandName { get; set; } = string.Empty;

    /// <summary>Categories from the top down to the product's own.</summary>
    public List<CategoryCrumbDto> Breadcrumb { get; set; } = new();

    /// <summary>The product's SKUs, sorted by code.</summary>
    public List<SkuDto> Skus { get; set; } = new();

    /// <summary>False once deactivated.</summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// An amount with its unit (P7), as sent over the API.
/// </summary>
public class QuantityDto
{
    /// <summary>Amount.</summary>
    public decimal Value { get; set; }

    /// <summary>Unit code.</summary>
    public string Uom { get; set; } = string.Empty;
}

/// <summary>
/// Result of converting a SKU quantity from one unit to another.
/// </summary>
public class ConversionResultDto
{
    /// <summary>What was asked for.</summary>
    public QuantityDto From { get; set; } = new();

    /// <summary>The converted amount.</summary>
    public QuantityDto To { get; set; } = new();
}

/// <summary>
/// Query for <c>GET /api/catalog/skus/{id}/convert</c>: convert an amount of a SKU between units.
/// </summary>
public class SkuConversionRequest
{
    /// <summary>Amount to convert.</summary>
    public decimal Value { get; set; }

    /// <summary>Unit of <see cref="Value"/>.</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>Unit to convert to.</summary>
    public string To { get; set; } = string.Empty;
}
