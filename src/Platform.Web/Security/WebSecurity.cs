using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Platform.Web.Security;

/// <summary>
/// Browser security for the web app (admin and store): response headers, and
/// the visitor's real IP address passed on to the API so its rate limits count
/// each visitor separately instead of the web server as one caller.
/// </summary>
public static class WebSecurity
{
    /// <summary>
    /// Content policy. Everything is served from this site (the project allows no
    /// CDN, inline script or inline style), so nothing else may load, frame the
    /// site or receive a form post. Bootstrap's form icons are data: images.
    /// </summary>
    private const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; " +
        "connect-src 'self'; form-action 'self'; frame-ancestors 'none'; base-uri 'self'; object-src 'none'";

    /// <summary>Configuration key listing proxies (load balancers) allowed to tell us a visitor's IP.</summary>
    private const string TrustedProxiesKey = "Security:TrustedProxies";

    /// <summary>
    /// Trusts <c>X-Forwarded-For</c> / <c>X-Forwarded-Proto</c> only from the
    /// configured proxies (loopback always), so the visitor IP cannot be spoofed.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Reads <c>Security:TrustedProxies</c>.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddPlatformForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            foreach (string proxy in configuration.GetSection(TrustedProxiesKey).Get<string[]>() ?? Array.Empty<string>())
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }
        });
        return services;
    }

    /// <summary>
    /// Adds the security headers to every page and file.
    /// </summary>
    /// <param name="app">App pipeline.</param>
    /// <returns>The pipeline, for chaining.</returns>
    public static IApplicationBuilder UseWebSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                IHeaderDictionary headers = context.Response.Headers;
                headers.ContentSecurityPolicy = ContentSecurityPolicy;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
                headers["Cross-Origin-Opener-Policy"] = "same-origin";
                return Task.CompletedTask;
            });
            await next();
        });
}

/// <summary>
/// Tells the API the visitor's IP on every call (<c>X-Forwarded-For</c>),
/// replacing anything the browser sent, so API rate limits and the audit log
/// see the real visitor. The API trusts this header only from known proxies.
/// </summary>
public sealed class ForwardedForHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="httpContextAccessor">Gives the current visitor's connection.</param>
    public ForwardedForHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Adds the header, then sends the request.
    /// </summary>
    /// <param name="request">Outgoing API request.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The API response.</returns>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove("X-Forwarded-For");
        if (_httpContextAccessor.HttpContext?.Connection.RemoteIpAddress is { } visitor)
        {
            request.Headers.Add("X-Forwarded-For", visitor.ToString());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
