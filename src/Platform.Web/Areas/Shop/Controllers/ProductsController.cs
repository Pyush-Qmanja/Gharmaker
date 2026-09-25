using Microsoft.AspNetCore.Mvc;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Storefront;
using Platform.Web.Areas.Shop.Models;
using Platform.Web.Models;
using Platform.Web.Services.Api;

namespace Platform.Web.Areas.Shop.Controllers;

/// <summary>
/// Product listings (by category, brand or search) and product pages.
/// </summary>
public sealed class ProductsController : ShopControllerBase
{
    /// <summary>Products per listing page.</summary>
    private const int PageSize = 12;

    private readonly IShopApiClient _shop;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="shop">Storefront API client.</param>
    public ProductsController(IShopApiClient shop)
    {
        _shop = shop;
    }

    /// <summary>
    /// Lists products.
    /// </summary>
    /// <param name="categoryId">Category.</param>
    /// <param name="brandId">Brand.</param>
    /// <param name="search">Search words.</param>
    /// <param name="page">Page.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The listing.</returns>
    [HttpGet]
    public async Task<IActionResult> Index(Guid? categoryId, Guid? brandId, string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        var request = new ShopProductRequest
        {
            CategoryId = categoryId,
            BrandId = brandId,
            Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            Page = Math.Max(page, 1),
            PageSize = PageSize,
            Pincode = SavedPincode,
        };
        var products = await _shop.GetProductsAsync(request, cancellationToken);
        var categories = (await _shop.GetCategoriesAsync(cancellationToken)).Value ?? new List<ShopCategoryDto>();
        var brands = (await _shop.GetBrandsAsync(cancellationToken)).Value ?? new List<ShopBrandDto>();

        string heading = request.Search is { } words ? $"Results for “{words}”"
            : categoryId is { } id ? FindCategory(categories, id)?.Name ?? "Products"
            : brandId is { } brand ? brands.FirstOrDefault(b => b.Id == brand)?.Name ?? "Products"
            : "All products";

        return View(new ShopListingViewModel(
            new ListViewModel<ShopProductCardDto>(
                heading,
                products.Value ?? new PagedResult<ShopProductCardDto> { Page = request.Page, PageSize = request.PageSize },
                request.Search,
                new Dictionary<string, string?> { ["categoryId"] = categoryId?.ToString(), ["brandId"] = brandId?.ToString() },
                itemName: "product"),
            categories,
            brands,
            categoryId,
            brandId,
            heading,
            SavedPincode));
    }

    /// <summary>
    /// Shows a product with its variants, prices, availability and delivery.
    /// </summary>
    /// <param name="id">Product id.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The product page, or 404.</returns>
    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var product = await _shop.GetProductAsync(id, SavedPincode, cancellationToken);
        return product.Value is null ? NotFound() : View(new ShopProductViewModel(product.Value, SavedPincode, User.IsCustomer()));
    }

    /// <summary>
    /// Finds a category anywhere in the tree.
    /// </summary>
    /// <param name="nodes">Categories at one level.</param>
    /// <param name="id">Category id.</param>
    /// <returns>The category, or null.</returns>
    private static ShopCategoryDto? FindCategory(IEnumerable<ShopCategoryDto> nodes, Guid id)
    {
        foreach (ShopCategoryDto node in nodes)
        {
            if (node.Id == id)
            {
                return node;
            }

            if (FindCategory(node.Children, id) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
