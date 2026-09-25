using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.Security.Authorization;
using Platform.Shared.Constants;
using Platform.Shared.Entities.Identity;

namespace Platform.Api.Middleware;

/// <summary>
/// Ends a staff session the moment it should end, without waiting for the
/// token to expire: every request from a signed-in staff user is refused with
/// 401 once the user is deactivated, or when their sessions were ended ("sign
/// out everywhere") after the token was issued. Access changes need nothing
/// here — capabilities are read per request anyway. Customer tokens are
/// checked by the storefront's own customer context.
/// </summary>
public static class SessionGuard
{
    /// <summary>What a refused caller is told.</summary>
    public const string EndedMessage = "Your session has ended. Please sign in again.";

    /// <summary>JWT "issued at" claim, in seconds since 1970.</summary>
    private const string IssuedAtClaim = "iat";

    /// <summary>
    /// Adds the guard. Place it straight after authentication, so it runs
    /// before rate limiting and authorisation.
    /// </summary>
    /// <param name="app">Application pipeline.</param>
    /// <returns>The pipeline, for chaining.</returns>
    public static IApplicationBuilder UseSessionGuard(this IApplicationBuilder app) =>
        app.Use(CheckAsync);

    /// <summary>
    /// Whether a staff token still belongs to a live session.
    /// </summary>
    /// <param name="account">The user as stored now, or null when not found.</param>
    /// <param name="principal">The token's claims.</param>
    /// <returns>True when the user is active and the token was issued after their sessions were last ended.</returns>
    public static bool IsLive(User? account, ClaimsPrincipal principal)
    {
        if (account is not { IsActive: true })
        {
            return false;
        }

        if (account.SessionsEndedAt is not { } endedAt)
        {
            return true;
        }

        // A token issued in the same second as, or before, the end is refused.
        long ended = new DateTimeOffset(DateTime.SpecifyKind(endedAt, DateTimeKind.Utc)).ToUnixTimeSeconds();
        return long.TryParse(principal.FindFirst(IssuedAtClaim)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out long issuedAt)
            && issuedAt > ended;
    }

    /// <summary>
    /// Refuses the request with 401 when a signed-in staff user's session has ended.
    /// </summary>
    /// <param name="context">Current request.</param>
    /// <param name="next">Rest of the pipeline.</param>
    /// <returns>A task that completes when the request is handled.</returns>
    private static async Task CheckAsync(HttpContext context, RequestDelegate next)
    {
        ClaimsPrincipal principal = context.User;
        bool isStaff = principal.Identity?.IsAuthenticated == true
            && principal.FindFirst(ClaimNames.Actor)?.Value != Actors.Customer;
        if (isStaff)
        {
            User? account = await context.RequestServices.GetRequiredService<ICallerAccount>().GetAsync(context.RequestAborted);
            if (!IsLive(account, principal))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new ProblemDetails { Status = StatusCodes.Status401Unauthorized, Title = "Session ended", Detail = EndedMessage },
                    options: null,
                    contentType: "application/problem+json",
                    context.RequestAborted);
                return;
            }
        }

        await next(context);
    }
}
