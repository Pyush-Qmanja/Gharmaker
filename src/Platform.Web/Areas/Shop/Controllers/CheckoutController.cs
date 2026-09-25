using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Platform.Shared.Dtos.Storefront;
using Platform.Web.Areas.Shop.Models;
using Platform.Web.Services.Api;

namespace Platform.Web.Areas.Shop.Controllers;

/// <summary>
/// Checkout: delivery address and contact, the order summary with GST, and
/// placing the order. The page carries a one-time key, so a double click or a
/// refresh never places a second order.
/// </summary>
[CustomerRequired]
public sealed class CheckoutController : ShopControllerBase
{
    private readonly IShopApiClient _shop;
    private readonly IValidator<CheckoutRequest> _validator;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="shop">Storefront API client.</param>
    /// <param name="validator">Shared checkout validator.</param>
    public CheckoutController(IShopApiClient shop, IValidator<CheckoutRequest> validator)
    {
        _shop = shop;
        _validator = validator;
    }

    /// <summary>
    /// Shows the checkout form, filled from the customer's profile.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The checkout page, or back to the cart when it cannot be ordered.</returns>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var cart = await _shop.GetCartAsync(cancellationToken);
        if (await SessionEndedAsync(cart) is { } ended)
        {
            return ended;
        }

        if (cart.Value is not { CanCheckout: true })
        {
            return RedirectToAction("Index", "Cart");
        }

        var profile = (await _shop.GetProfileAsync(cancellationToken)).Value;
        var form = new CheckoutForm
        {
            ClientId = Guid.NewGuid(),
            Phone = profile?.Phone ?? string.Empty,
            Gstin = profile?.Gstin,
            Address = profile?.Address is { } saved && saved.Pincode == cart.Value.Pincode
                ? saved
                : new ShopAddressDto { Pincode = cart.Value.Pincode ?? string.Empty },
        };
        return View(new CheckoutViewModel(form, cart.Value));
    }

    /// <summary>
    /// Places the order.
    /// </summary>
    /// <param name="form">Posted form.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The order page, or the form with the reason.</returns>
    [HttpPost]
    public async Task<IActionResult> Index(CheckoutForm form, CancellationToken cancellationToken)
    {
        var request = new CheckoutRequest { ClientId = form.ClientId, Address = form.Address, Phone = form.Phone ?? string.Empty, Gstin = form.Gstin };
        request.Normalise();
        if (await ValidateAsync(_validator, request, cancellationToken, prefix: "Form"))
        {
            var placed = await _shop.CheckoutAsync(request, cancellationToken);
            if (await SessionEndedAsync(placed) is { } ended)
            {
                return ended;
            }

            if (placed.IsSuccess)
            {
                if (form.SaveAddress)
                {
                    await SaveAddressAsync(request, cancellationToken);
                }

                return RedirectToAction("Details", "Orders", new { id = placed.Value!.Id, placed = true });
            }

            AddApiErrors(placed, prefix: "Form");
        }

        var cart = await _shop.GetCartAsync(cancellationToken);
        return View(new CheckoutViewModel(form, cart.Value ?? new ShopCartDto()));
    }

    /// <summary>
    /// Keeps the delivery address and contact as the customer's defaults.
    /// </summary>
    /// <param name="request">Placed checkout.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>A task that completes when saved (failures are ignored: the order is placed).</returns>
    private async Task SaveAddressAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        var profile = (await _shop.GetProfileAsync(cancellationToken)).Value;
        if (profile is null)
        {
            return;
        }

        await _shop.UpdateProfileAsync(new UpdateShopProfileRequest
        {
            Name = profile.Name,
            Phone = request.Phone,
            CompanyName = profile.CompanyName,
            Gstin = request.Gstin ?? profile.Gstin,
            Address = request.Address,
        }, cancellationToken);
    }
}
