using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Platform.Web.Common;
using Platform.Web.Services.Api;

namespace Platform.Web.Controllers;

/// <summary>
/// Base of every UI controller. Holds the helpers for validation, API errors
/// and flash messages so no controller implements them twice.
/// </summary>
public abstract class PlatformControllerBase : Controller
{
    /// <summary>
    /// Runs a shared FluentValidation validator and copies failures into ModelState.
    /// </summary>
    /// <typeparam name="T">Model type.</typeparam>
    /// <param name="validator">Validator from <c>Platform.Shared</c>.</param>
    /// <param name="model">Posted model.</param>
    /// <param name="cancellationToken">Cancels validation.</param>
    /// <returns>True when ModelState is valid afterwards.</returns>
    protected async Task<bool> ValidateAsync<T>(IValidator<T> validator, T model, CancellationToken cancellationToken)
    {
        ValidationResult result = await validator.ValidateAsync(model, cancellationToken);
        foreach (ValidationFailure failure in result.Errors)
        {
            ModelState.AddModelError(failure.PropertyName, failure.ErrorMessage);
        }

        return ModelState.IsValid;
    }

    /// <summary>
    /// Copies an API error into ModelState: field errors onto their fields,
    /// anything else into the form summary.
    /// </summary>
    /// <param name="result">Failed API result.</param>
    protected void AddApiErrors(ApiResult result)
    {
        foreach (var (field, messages) in result.FieldErrors)
        {
            foreach (string message in messages)
            {
                ModelState.AddModelError(field, message);
            }
        }

        if (result.FieldErrors.Count == 0)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "The request failed.");
        }
    }

    /// <summary>
    /// Sends the user to sign in again, returning here afterwards. Used when
    /// the API rejects an expired token.
    /// </summary>
    /// <returns>A redirect to the login page.</returns>
    protected IActionResult RedirectToLogin() =>
        RedirectToAction(nameof(AccountController.Login), "Account", new { returnUrl = Request.Path + Request.QueryString });

    /// <summary>
    /// Turns an API "not signed in" or "not allowed" answer into the right page.
    /// </summary>
    /// <param name="result">Any API result.</param>
    /// <returns>A redirect to sign-in (401), the "not allowed" page (403), or null to carry on.</returns>
    protected IActionResult? HandleAccess(ApiResult result)
    {
        if (result.IsUnauthorized)
        {
            return RedirectToLogin();
        }

        if (result.IsForbidden)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View("Forbidden");
        }

        return null;
    }

    /// <summary>
    /// Queues a success message for the next page.
    /// </summary>
    /// <param name="message">Message to show.</param>
    protected void FlashSuccess(string message) => TempData[TempDataKeys.Success] = message;

    /// <summary>
    /// Queues an error message for the next page.
    /// </summary>
    /// <param name="message">Message to show.</param>
    protected void FlashError(string message) => TempData[TempDataKeys.Error] = message;
}
