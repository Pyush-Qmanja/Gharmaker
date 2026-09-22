using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Shared.Dtos.Auth;
using Platform.Web.Services.Auth;

namespace Platform.Web.Controllers;

/// <summary>
/// Sign-in and sign-out screens.
/// </summary>
public sealed class AccountController : PlatformControllerBase
{
    private readonly IAccountService _accountService;
    private readonly IValidator<LoginRequest> _loginValidator;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="accountService">Signs users in and out.</param>
    /// <param name="loginValidator">Shared login validator.</param>
    public AccountController(IAccountService accountService, IValidator<LoginRequest> loginValidator)
    {
        _accountService = accountService;
        _loginValidator = loginValidator;
    }

    /// <summary>
    /// Shows the sign-in form.
    /// </summary>
    /// <param name="returnUrl">Local page to return to after sign-in.</param>
    /// <returns>The sign-in page.</returns>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginRequest());
    }

    /// <summary>
    /// Signs the user in and returns them to where they were going.
    /// </summary>
    /// <param name="form">Posted credentials.</param>
    /// <param name="returnUrl">Local page to return to after sign-in.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>Redirect on success, or the form with errors.</returns>
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest form, string? returnUrl, CancellationToken cancellationToken)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!await ValidateAsync(_loginValidator, form, cancellationToken))
        {
            return View(form);
        }

        var result = await _accountService.SignInAsync(form, cancellationToken);
        if (!result.IsSuccess)
        {
            AddApiErrors(result);
            return View(form);
        }

        // Only local URLs, so the login page cannot be used as an open redirect.
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// Signs the user out.
    /// </summary>
    /// <returns>Redirect to the sign-in page.</returns>
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await _accountService.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }
}
