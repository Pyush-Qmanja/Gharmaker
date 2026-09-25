using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Platform.Shared.Constants;
using Platform.Web.Areas.Shop;
using Platform.Web.Services.Auth;

namespace Platform.Web.Security;

/// <summary>
/// Keeps a signed-in staff browser in step with the API on every page, so an
/// administrator's change reaches people who are already signed in:
/// <list type="bullet">
/// <item>access changes show on the next click (menus and screens read access per request);</item>
/// <item>a deactivated user, or one signed out everywhere, is signed out at once and
/// sent to the sign-in page with "Your session has ended";</item>
/// <item>a renamed user sees their new name without signing in again.</item>
/// </list>
/// </summary>
public static class StaffSession
{
    /// <summary>Query value on the sign-in page telling why the user is there.</summary>
    public const string NoticeParameter = "notice";

    /// <summary>Notice: the session was ended by the system or an administrator.</summary>
    public const string EndedNotice = "ended";

    /// <summary>Notice: the user signed themselves out on every device.</summary>
    public const string EverywhereNotice = "everywhere";

    /// <summary>Pages that must work without a live session (sign-in and sign-out).</summary>
    private static readonly PathString AccountPath = "/Account";

    /// <summary>
    /// Adds the check. Place it after authentication and the store's identity switch.
    /// </summary>
    /// <param name="app">Application pipeline.</param>
    /// <returns>The pipeline, for chaining.</returns>
    public static IApplicationBuilder UseStaffSession(this IApplicationBuilder app) =>
        app.Use(CheckAsync);

    /// <summary>
    /// The message the sign-in page shows for a notice value.
    /// </summary>
    /// <param name="notice">Value of <see cref="NoticeParameter"/>.</param>
    /// <returns>The message, or null for none.</returns>
    public static string? MessageFor(string? notice) => notice switch
    {
        EndedNotice => "Your session has ended. Please sign in again.",
        EverywhereNotice => "You are signed out on every device.",
        _ => null,
    };

    /// <summary>
    /// Signs out a staff session the API no longer accepts, and refreshes a changed name.
    /// </summary>
    /// <param name="context">Current request.</param>
    /// <param name="next">Rest of the pipeline.</param>
    /// <returns>A task that completes when the request is handled.</returns>
    private static async Task CheckAsync(HttpContext context, RequestDelegate next)
    {
        bool isStaffPage = !context.Request.Path.StartsWithSegments(ShopAuth.PathPrefix)
            && !context.Request.Path.StartsWithSegments(AccountPath);
        if (!isStaffPage || context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var access = context.RequestServices.GetRequiredService<IUserAccess>();
        switch (await access.GetSessionStateAsync())
        {
            case SessionState.Ended:
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                string returnUrl = HttpMethods.IsGet(context.Request.Method)
                    ? context.Request.Path + context.Request.QueryString
                    : "/";
                context.Response.Redirect(LoginUrlFor(context, returnUrl));
                return;

            case SessionState.Live when await access.GetCurrentAsync() is { } current
                                        && current.Name != context.User.Identity.Name:
                await RenameAsync(context, current.Name);
                break;
        }

        await next(context);
    }

    /// <summary>
    /// Builds the sign-in address with the return page and the "session ended" notice.
    /// </summary>
    /// <param name="context">Current request.</param>
    /// <param name="returnUrl">Local page to return to after signing in.</param>
    /// <returns>The address.</returns>
    private static string LoginUrlFor(HttpContext context, string returnUrl) =>
        context.Request.PathBase + AccountPath + "/Login"
        + QueryString.Create(new Dictionary<string, string?> { ["returnUrl"] = returnUrl, [NoticeParameter] = EndedNotice });

    /// <summary>
    /// Re-issues the cookie with the user's new name, keeping its expiry.
    /// </summary>
    /// <param name="context">Current request.</param>
    /// <param name="name">Name as stored now.</param>
    /// <returns>A task that completes when the cookie is re-issued.</returns>
    private static async Task RenameAsync(HttpContext context, string name)
    {
        AuthenticateResult current = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (current.Principal?.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        var renamed = new ClaimsIdentity(
            identity.Claims.Where(c => c.Type != ClaimNames.Name).Append(new Claim(ClaimNames.Name, name)),
            identity.AuthenticationType,
            ClaimNames.Name,
            roleType: null);
        var principal = new ClaimsPrincipal(renamed);
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, current.Properties);
        context.User = principal;
    }
}
