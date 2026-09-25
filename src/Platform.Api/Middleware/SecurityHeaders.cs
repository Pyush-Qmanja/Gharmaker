namespace Platform.Api.Middleware;

/// <summary>
/// Adds the browser security headers to every API response. The API only
/// serves JSON, so its content policy allows nothing to load and nothing to
/// frame it; Swagger's own pages (Development only) are left alone.
/// </summary>
public static class SecurityHeaders
{
    /// <summary>Path of the Swagger UI, which needs its own scripts and styles.</summary>
    private const string SwaggerPath = "/swagger";

    /// <summary>
    /// Adds the headers before each response is sent.
    /// </summary>
    /// <param name="app">App pipeline.</param>
    /// <returns>The pipeline, for chaining.</returns>
    public static IApplicationBuilder UseApiSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                IHeaderDictionary headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Cross-Origin-Resource-Policy"] = "same-origin";
                if (!context.Request.Path.StartsWithSegments(SwaggerPath))
                {
                    headers.XFrameOptions = "DENY";
                    headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
                    headers.CacheControl = "no-store";
                }

                return Task.CompletedTask;
            });
            await next();
        });
}
