using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Platform.Web.Controllers;
using Platform.Web.Services.Api;

namespace Platform.Web.Areas.Shop.Controllers;

/// <summary>
/// Base of every store page: the Shop area, the visitor's delivery PIN code
/// (a cookie, and the cart's PIN code once signed in), and what to do when the
/// customer's session has ended.
/// </summary>
[Area(ShopAuth.Area)]
public abstract class ShopControllerBase : PlatformControllerBase
{
    /// <summary>How long the PIN code cookie is kept.</summary>
    private static readonly TimeSpan PincodeLifetime = TimeSpan.FromDays(180);

    /// <summary>The visitor's delivery PIN code, if they gave one.</summary>
    protected string? SavedPincode =>
        Request.Cookies.TryGetValue(ShopAuth.PincodeCookie, out string? pin) && IsPincode(pin) ? pin : null;

    /// <summary>
    /// Remembers the visitor's delivery PIN code.
    /// </summary>
    /// <param name="pincode">Six-digit PIN code.</param>
    protected void RememberPincode(string pincode) =>
        Response.Cookies.Append(ShopAuth.PincodeCookie, pincode, new CookieOptions
        {
            Path = ShopAuth.PathPrefix,
            MaxAge = PincodeLifetime,
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
        });

    /// <summary>
    /// Handles a failed call to a customer endpoint: an ended session signs the
    /// customer out and sends them to sign in again.
    /// </summary>
    /// <param name="result">API result.</param>
    /// <returns>A redirect when the session ended; otherwise null.</returns>
    protected async Task<IActionResult?> SessionEndedAsync(ApiResult result)
    {
        if (!result.IsUnauthorized)
        {
            return null;
        }

        await HttpContext.SignOutAsync(ShopAuth.Scheme);
        FlashError("Your session has ended. Please sign in again.");
        return RedirectToAction("Login", "Account", new { returnUrl = Request.Path + Request.QueryString });
    }

    /// <summary>
    /// Checks a PIN code's format.
    /// </summary>
    /// <param name="pincode">Candidate.</param>
    /// <returns>True for six digits not starting with 0.</returns>
    protected static bool IsPincode(string? pincode) =>
        pincode is { Length: 6 } && pincode[0] != '0' && pincode.All(char.IsAsciiDigit);
}
