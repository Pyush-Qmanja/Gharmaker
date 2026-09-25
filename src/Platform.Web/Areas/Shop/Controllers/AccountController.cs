using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Platform.Shared.Common;
using Platform.Shared.Dtos.Auth;
using Platform.Shared.Dtos.Storefront;
using Platform.Web.Services.Api;

namespace Platform.Web.Areas.Shop.Controllers;

/// <summary>
/// Customer accounts on the store: sign in, create an account, sign out and
/// the profile. An item a visitor tried to add before signing in is added
/// straight after.
/// </summary>
public sealed class AccountController : ShopControllerBase
{
    private readonly IShopApiClient _shop;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<ShopRegisterRequest> _registerValidator;
    private readonly IValidator<UpdateShopProfileRequest> _profileValidator;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="shop">Storefront API client.</param>
    /// <param name="loginValidator">Shared sign-in validator.</param>
    /// <param name="registerValidator">Shared registration validator.</param>
    /// <param name="profileValidator">Shared profile validator.</param>
    public AccountController(
        IShopApiClient shop,
        IValidator<LoginRequest> loginValidator,
        IValidator<ShopRegisterRequest> registerValidator,
        IValidator<UpdateShopProfileRequest> profileValidator)
    {
        _shop = shop;
        _loginValidator = loginValidator;
        _registerValidator = registerValidator;
        _profileValidator = profileValidator;
    }

    /// <summary>
    /// Shows the sign-in form.
    /// </summary>
    /// <param name="returnUrl">Page to go to afterwards.</param>
    /// <returns>The form.</returns>
    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        TempData.Keep(CartController.PendingAddKey);
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginRequest());
    }

    /// <summary>
    /// Signs the customer in.
    /// </summary>
    /// <param name="form">Email and password.</param>
    /// <param name="returnUrl">Page to go to afterwards.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The next page, or the form with the reason.</returns>
    [HttpPost]
    public async Task<IActionResult> Login(LoginRequest form, string? returnUrl, CancellationToken cancellationToken)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (await ValidateAsync(_loginValidator, form, cancellationToken))
        {
            var session = await _shop.LoginAsync(form, cancellationToken);
            if (session.Value is { } signedIn)
            {
                return await AfterSignInAsync(signedIn, returnUrl, cancellationToken);
            }

            ModelState.AddModelError(string.Empty, session.IsUnauthorized
                ? "That email and password do not match a store account."
                : session.ErrorMessage ?? "Could not sign in.");
        }

        TempData.Keep(CartController.PendingAddKey);
        return View(form);
    }

    /// <summary>
    /// Shows the new-account form.
    /// </summary>
    /// <param name="returnUrl">Page to go to afterwards.</param>
    /// <returns>The form.</returns>
    [HttpGet]
    public IActionResult Register(string? returnUrl)
    {
        TempData.Keep(CartController.PendingAddKey);
        ViewData["ReturnUrl"] = returnUrl;
        return View(new ShopRegisterRequest());
    }

    /// <summary>
    /// Creates the account and signs in.
    /// </summary>
    /// <param name="form">Details.</param>
    /// <param name="returnUrl">Page to go to afterwards.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The next page, or the form with errors.</returns>
    [HttpPost]
    public async Task<IActionResult> Register(ShopRegisterRequest form, string? returnUrl, CancellationToken cancellationToken)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (await ValidateAsync(_registerValidator, form, cancellationToken))
        {
            var session = await _shop.RegisterAsync(form, cancellationToken);
            if (session.Value is { } signedIn)
            {
                FlashSuccess($"Welcome, {signedIn.Name}. Your account is ready.");
                return await AfterSignInAsync(signedIn, returnUrl, cancellationToken);
            }

            AddApiErrors(session);
        }

        TempData.Keep(CartController.PendingAddKey);
        return View(form);
    }

    /// <summary>
    /// Signs the customer out of the store.
    /// </summary>
    /// <returns>The store's front page.</returns>
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(ShopAuth.Scheme);
        FlashSuccess("You are signed out.");
        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// Shows the customer's profile.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The profile form.</returns>
    [HttpGet]
    [CustomerRequired]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken)
    {
        var profile = await _shop.GetProfileAsync(cancellationToken);
        if (await SessionEndedAsync(profile) is { } ended)
        {
            return ended;
        }

        var value = profile.Value!;
        ViewData["Email"] = value.Email;
        return View(new UpdateShopProfileRequest
        {
            Name = value.Name,
            Phone = value.Phone,
            CompanyName = value.CompanyName,
            Gstin = value.Gstin,
            Address = value.Address ?? new ShopAddressDto(),
        });
    }

    /// <summary>
    /// Saves the customer's profile.
    /// </summary>
    /// <param name="form">New values.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The profile, saved or with errors.</returns>
    [HttpPost]
    [CustomerRequired]
    public async Task<IActionResult> Profile(UpdateShopProfileRequest form, CancellationToken cancellationToken)
    {
        // An address left completely blank means "no saved address".
        if (form.Address is { } address && string.IsNullOrWhiteSpace(address.Line1) && string.IsNullOrWhiteSpace(address.City)
            && string.IsNullOrWhiteSpace(address.Pincode))
        {
            form.Address = null;
        }

        if (await ValidateAsync(_profileValidator, form, cancellationToken))
        {
            var result = await _shop.UpdateProfileAsync(form, cancellationToken);
            if (await SessionEndedAsync(result) is { } ended)
            {
                return ended;
            }

            if (result.IsSuccess)
            {
                FlashSuccess("Your details are saved.");
                return RedirectToAction(nameof(Profile));
            }

            AddApiErrors(result);
        }

        ViewData["Email"] = User.FindFirst(Platform.Shared.Constants.ClaimNames.Email)?.Value;
        form.Address ??= new ShopAddressDto();
        return View(form);
    }

    /// <summary>
    /// Sets the store cookie, adds any item waiting from before sign-in, and moves on.
    /// </summary>
    /// <param name="session">New session.</param>
    /// <param name="returnUrl">Page to go to.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The redirect.</returns>
    private async Task<IActionResult> AfterSignInAsync(ShopSessionDto session, string? returnUrl, CancellationToken cancellationToken)
    {
        await ShopAuth.SignInAsync(HttpContext, session);

        if (SavedPincode is { } pin)
        {
            await _shop.SetPincodeAsync(pin, cancellationToken);
        }

        if (TempData[CartController.PendingAddKey] is string pending
            && JsonSerializer.Deserialize<AddCartLineRequest>(pending, JsonDefaults.Options) is { } line)
        {
            var added = await _shop.AddLineAsync(line, cancellationToken);
            if (added.IsSuccess)
            {
                FlashSuccess("You are signed in, and the item is in your cart.");
            }
            else
            {
                FlashError(added.FieldErrors.Values.SelectMany(v => v).FirstOrDefault() ?? added.ErrorMessage ?? "Could not add the item.");
            }

            return RedirectToAction("Index", "Cart");
        }

        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction("Index", "Home");
    }
}
