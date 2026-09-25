using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Platform.Shared.Common;
using Platform.Shared.Dtos.Storefront;
using Platform.Web.Services.Api;

namespace Platform.Web.Areas.Shop.Controllers;

/// <summary>
/// The customer's cart: add from a product page (a visitor is asked to sign
/// in first and the item is added straight after), change amounts, remove.
/// </summary>
public sealed class CartController : ShopControllerBase
{
    /// <summary>TempData key of an item a visitor tried to add before signing in.</summary>
    public const string PendingAddKey = "shop:pending-add";

    private readonly IShopApiClient _shop;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="shop">Storefront API client.</param>
    public CartController(IShopApiClient shop)
    {
        _shop = shop;
    }

    /// <summary>
    /// Shows the cart with prices, GST and delivery.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The cart page.</returns>
    [HttpGet]
    [CustomerRequired]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var cart = await _shop.GetCartAsync(cancellationToken);
        if (await SessionEndedAsync(cart) is { } ended)
        {
            return ended;
        }

        // A PIN code chosen while browsing becomes the cart's, so availability matches what was shown.
        if (cart.Value is { Pincode: null } && SavedPincode is { } pin)
        {
            cart = await _shop.SetPincodeAsync(pin, cancellationToken);
        }

        return View(cart.Value ?? new ShopCartDto());
    }

    /// <summary>
    /// Adds an amount of a variant to the cart.
    /// </summary>
    /// <param name="request">SKU, amount, unit.</param>
    /// <param name="returnUrl">Product page, to go back to on an error.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The cart, or back to the product with the reason.</returns>
    [HttpPost]
    public async Task<IActionResult> Add(AddCartLineRequest request, string? returnUrl, CancellationToken cancellationToken)
    {
        if (!User.IsCustomer())
        {
            TempData[PendingAddKey] = JsonSerializer.Serialize(request, JsonDefaults.Options);
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Index)) });
        }

        if (request.Quantity <= 0)
        {
            FlashError("Enter a quantity greater than zero.");
            return BackTo(returnUrl);
        }

        var result = await _shop.AddLineAsync(request, cancellationToken);
        if (await SessionEndedAsync(result) is { } ended)
        {
            return ended;
        }

        if (!result.IsSuccess)
        {
            FlashError(result.FieldErrors.Values.SelectMany(v => v).FirstOrDefault() ?? result.ErrorMessage ?? "Could not add to the cart.");
            return BackTo(returnUrl);
        }

        FlashSuccess("Added to your cart.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Changes the amount or unit of a line.
    /// </summary>
    /// <param name="skuId">SKU of the line.</param>
    /// <param name="request">New amount and unit.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The cart.</returns>
    [HttpPost]
    [CustomerRequired]
    public async Task<IActionResult> Update(Guid skuId, UpdateCartLineRequest request, CancellationToken cancellationToken)
    {
        var result = await _shop.UpdateLineAsync(skuId, request, cancellationToken);
        if (await SessionEndedAsync(result) is { } ended)
        {
            return ended;
        }

        if (!result.IsSuccess)
        {
            FlashError(result.FieldErrors.Values.SelectMany(v => v).FirstOrDefault() ?? result.ErrorMessage ?? "Could not change the cart.");
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Removes a line.
    /// </summary>
    /// <param name="skuId">SKU of the line.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The cart.</returns>
    [HttpPost]
    [CustomerRequired]
    public async Task<IActionResult> Remove(Guid skuId, CancellationToken cancellationToken)
    {
        var result = await _shop.RemoveLineAsync(skuId, cancellationToken);
        if (await SessionEndedAsync(result) is { } ended)
        {
            return ended;
        }

        FlashSuccess("Removed from your cart.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Returns to a local page, or the store's front page.
    /// </summary>
    /// <param name="returnUrl">Page to go back to.</param>
    /// <returns>The redirect.</returns>
    private IActionResult BackTo(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction("Index", "Home");
}
