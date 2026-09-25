using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Platform.Shared.Dtos.Storefront;
using Platform.Web.Areas.Shop.Models;
using Platform.Web.Services.Api;

namespace Platform.Web.Areas.Shop.ViewComponents;

/// <summary>
/// The store's header: logo, search, delivery PIN code, account and cart
/// count, and the category menu. Categories are the same for every visitor,
/// so they are cached for a few minutes.
/// </summary>
public sealed class ShopHeaderViewComponent : ViewComponent
{
    /// <summary>Cache key of the category menu.</summary>
    private const string CategoriesKey = "shop:categories";

    /// <summary>How long the menu is cached.</summary>
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

    private readonly IShopApiClient _shop;
    private readonly IMemoryCache _cache;

    /// <summary>
    /// Creates the component.
    /// </summary>
    /// <param name="shop">Storefront API client.</param>
    /// <param name="cache">Holds the category menu.</param>
    public ShopHeaderViewComponent(IShopApiClient shop, IMemoryCache cache)
    {
        _shop = shop;
        _cache = cache;
    }

    /// <summary>
    /// Builds the header.
    /// </summary>
    /// <returns>The header view.</returns>
    public async Task<IViewComponentResult> InvokeAsync()
    {
        CancellationToken cancellationToken = HttpContext.RequestAborted;
        if (!_cache.TryGetValue(CategoriesKey, out List<ShopCategoryDto>? categories) || categories is null)
        {
            categories = (await _shop.GetCategoriesAsync(cancellationToken)).Value ?? new List<ShopCategoryDto>();
            _cache.Set(CategoriesKey, categories, CacheFor);
        }

        string? name = null;
        int cartCount = 0;
        if (UserClaimsPrincipal.IsCustomer())
        {
            name = UserClaimsPrincipal.Identity?.Name;
            cartCount = (await _shop.GetCartAsync(cancellationToken)).Value?.Lines.Count ?? 0;
        }

        string? pincode = Request.Cookies.TryGetValue(ShopAuth.PincodeCookie, out string? pin) ? pin : null;
        return View(new ShopHeaderViewModel(categories, name, cartCount, pincode, Request.Query["search"].FirstOrDefault()));
    }
}
