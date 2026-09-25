using Microsoft.AspNetCore.Mvc;
using Platform.Shared.Dtos.Storefront;
using Platform.Web.Areas.Shop.Models;
using Platform.Web.Services.Api;

namespace Platform.Web.Areas.Shop.Controllers;

/// <summary>
/// The store's front page and the delivery PIN code box shown on every page.
/// </summary>
public sealed class HomeController : ShopControllerBase
{
    /// <summary>Products on the front page.</summary>
    private const int FeaturedCount = 8;

    private readonly IShopApiClient _shop;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="shop">Storefront API client.</param>
    public HomeController(IShopApiClient shop)
    {
        _shop = shop;
    }

    /// <summary>
    /// Shows categories, brands and a first set of products.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The front page.</returns>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var categories = await _shop.GetCategoriesAsync(cancellationToken);
        var products = await _shop.GetProductsAsync(new ShopProductRequest { PageSize = FeaturedCount, Pincode = SavedPincode }, cancellationToken);
        var brands = await _shop.GetBrandsAsync(cancellationToken);
        return View(new ShopHomeViewModel(
            categories.Value ?? new List<ShopCategoryDto>(),
            products.Value?.Items ?? new List<ShopProductCardDto>(),
            brands.Value ?? new List<ShopBrandDto>(),
            SavedPincode));
    }

    /// <summary>
    /// Remembers the delivery PIN code (and puts it on the cart when signed in), then returns.
    /// </summary>
    /// <param name="pincode">PIN code typed.</param>
    /// <param name="returnUrl">Page to go back to.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>Back to the page.</returns>
    [HttpPost]
    public async Task<IActionResult> Pincode(string? pincode, string? returnUrl, CancellationToken cancellationToken)
    {
        pincode = pincode?.Trim();
        if (IsPincode(pincode))
        {
            RememberPincode(pincode!);
            if (User.IsCustomer())
            {
                await _shop.SetPincodeAsync(pincode!, cancellationToken);
            }

            FlashSuccess($"Showing availability and delivery for PIN code {pincode}.");
        }
        else
        {
            FlashError("Enter a six-digit PIN code, e.g. 411001.");
        }

        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index));
    }
}
