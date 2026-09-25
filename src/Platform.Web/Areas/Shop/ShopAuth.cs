using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Storefront;

namespace Platform.Web.Areas.Shop;

/// <summary>
/// How the online store keeps customers apart from staff in the one web app:
/// its own cookie scheme, limited to <c>/shop</c>, and a request identity that
/// on store pages is always the customer (or nobody) — never a staff user.
/// </summary>
public static class ShopAuth
{
    /// <summary>Area name of the store.</summary>
    public const string Area = "Shop";

    /// <summary>URL prefix of the store.</summary>
    public const string PathPrefix = "/shop";

    /// <summary>Cookie authentication scheme for customers.</summary>
    public const string Scheme = "ShopCustomer";

    /// <summary>Cookie holding a visitor's delivery PIN code.</summary>
    public const string PincodeCookie = "shop_pin";

    /// <summary>
    /// On store pages, replaces the request identity with the customer's
    /// cookie (or an anonymous one), so the token forwarded to the API is the
    /// customer's and staff sign-ins never leak into the store.
    /// </summary>
    /// <param name="app">App pipeline, after authentication.</param>
    /// <returns>The pipeline, for chaining.</returns>
    public static IApplicationBuilder UseShopIdentity(this IApplicationBuilder app) =>
        app.UseWhen(
            context => context.Request.Path.StartsWithSegments(PathPrefix),
            shop => shop.Use(async (context, next) =>
            {
                AuthenticateResult result = await context.AuthenticateAsync(Scheme);
                context.User = result.Succeeded && result.Principal is not null ? result.Principal : new ClaimsPrincipal(new ClaimsIdentity());
                await next();
            }));

    /// <summary>
    /// Signs a customer in to the store, and makes them the identity of the rest
    /// of this request so API calls made before the redirect carry their token.
    /// </summary>
    /// <param name="context">Current request.</param>
    /// <param name="session">Session returned by the API.</param>
    /// <returns>A task that completes when the cookie is set.</returns>
    public static Task SignInAsync(HttpContext context, ShopSessionDto session)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimNames.UserId, session.CustomerId.ToString()),
                new Claim(ClaimNames.Name, session.Name),
                new Claim(ClaimNames.Email, session.Email),
                new Claim(ClaimNames.Actor, Actors.Customer),
                new Claim(ClaimNames.AccessToken, session.AccessToken),
            },
            Scheme,
            ClaimNames.Name,
            roleType: null);
        var principal = new ClaimsPrincipal(identity);
        context.User = principal;
        return context.SignInAsync(Scheme, principal, new AuthenticationProperties
        {
            ExpiresUtc = session.ExpiresAt,
            IsPersistent = true,
        });
    }

    /// <summary>
    /// Checks whether the current request is from a signed-in customer.
    /// </summary>
    /// <param name="user">Request identity.</param>
    /// <returns>True for a customer.</returns>
    public static bool IsCustomer(this ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && user.FindFirst(ClaimNames.Actor)?.Value == Actors.Customer;
}

/// <summary>
/// Adds the staff sign-in requirement to every controller outside the store,
/// so the store's pages stay open to visitors while the admin app stays closed.
/// </summary>
public sealed class StaffAuthorizationConvention : IControllerModelConvention
{
    /// <summary>
    /// Adds <see cref="AuthorizeFilter"/> unless the controller is in the store area.
    /// </summary>
    /// <param name="controller">Controller being configured.</param>
    public void Apply(ControllerModel controller)
    {
        if (!controller.RouteValues.TryGetValue("area", out string? area) || area != ShopAuth.Area)
        {
            controller.Filters.Add(new AuthorizeFilter());
        }
    }
}

/// <summary>
/// Sends visitors to the store's sign-in page before an action that needs a customer.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class CustomerRequiredAttribute : Attribute, IAuthorizationFilter
{
    /// <summary>
    /// Redirects when nobody is signed in to the store.
    /// </summary>
    /// <param name="context">Authorization context.</param>
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!context.HttpContext.User.IsCustomer())
        {
            HttpRequest request = context.HttpContext.Request;
            context.Result = new RedirectToActionResult("Login", "Account", new
            {
                area = ShopAuth.Area,
                returnUrl = HttpMethods.IsGet(request.Method) ? request.Path + request.QueryString : null,
            });
        }
    }
}
