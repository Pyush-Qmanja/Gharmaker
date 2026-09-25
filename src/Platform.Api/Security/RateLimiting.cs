using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Platform.Shared.Constants;

namespace Platform.Api.Security;

/// <summary>
/// Request limits (<c>RateLimiting</c> section). Reads and writes are limited
/// separately per caller — the signed-in user, or the IP address of a visitor —
/// so no single client can run up Firestore reads or writes; sign-in,
/// registration and checkout have tighter limits of their own.
/// </summary>
public sealed class RateLimitOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "RateLimiting";

    /// <summary>False switches every limit off (never in production).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>GET requests per caller per minute.</summary>
    [Range(1, 100_000)]
    public int ReadsPerMinute { get; set; } = 600;

    /// <summary>POST, PUT and DELETE requests per caller per minute.</summary>
    [Range(1, 100_000)]
    public int WritesPerMinute { get; set; } = 120;

    /// <summary>Sign-in attempts (staff and customers) per IP address per minute.</summary>
    [Range(1, 10_000)]
    public int SignInsPerMinute { get; set; } = 10;

    /// <summary>Store accounts opened per IP address per window.</summary>
    [Range(1, 10_000)]
    public int RegistrationsPerWindow { get; set; } = 5;

    /// <summary>Length of the registration window, in minutes.</summary>
    [Range(1, 1440)]
    public int RegistrationWindowMinutes { get; set; } = 15;

    /// <summary>Checkouts per customer per minute.</summary>
    [Range(1, 10_000)]
    public int CheckoutsPerMinute { get; set; } = 10;

    /// <summary>
    /// Addresses of proxies allowed to tell the API a caller's real IP
    /// (<c>X-Forwarded-For</c>) — the web app's servers and any load balancer.
    /// Loopback is always trusted.
    /// </summary>
    public List<string> TrustedProxies { get; set; } = new();
}

/// <summary>
/// Names of the endpoint limits, used with <c>[EnableRateLimiting]</c>.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>Sign-in endpoints.</summary>
    public const string SignIn = "sign-in";

    /// <summary>Store account registration.</summary>
    public const string Register = "register";

    /// <summary>Store checkout.</summary>
    public const string Checkout = "checkout";
}

/// <summary>
/// Registers and applies request limiting, and trusts the forwarded client IP
/// only from known proxies so it cannot be spoofed from outside.
/// </summary>
public static class RateLimitingSetup
{
    /// <summary>Message sent with every refused request.</summary>
    private const string TooManyMessage = "Too many requests. Please wait {0} and try again.";

    /// <summary>
    /// Registers the limiter, its endpoint policies and forwarded-header handling.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Reads the <c>RateLimiting</c> section.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddPlatformRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitOptions>()
            .Bind(configuration.GetSection(RateLimitOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        var options = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();

        services.Configure<ForwardedHeadersOptions>(forwarded =>
        {
            forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            forwarded.ForwardLimit = 1;
            foreach (string proxy in options.TrustedProxies)
            {
                forwarded.KnownProxies.Add(IPAddress.Parse(proxy));
            }
        });

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = WriteRejectionAsync;

            // Endpoint policies always exist (endpoints name them); switched off they never refuse.
            limiter.AddPolicy(RateLimitPolicies.SignIn, context => options.Enabled
                ? RateLimitPartition.GetFixedWindowLimiter($"sign-in:{IpKey(context)}", _ => FixedWindow(options.SignInsPerMinute, TimeSpan.FromMinutes(1)))
                : RateLimitPartition.GetNoLimiter(string.Empty));
            limiter.AddPolicy(RateLimitPolicies.Register, context => options.Enabled
                ? RateLimitPartition.GetFixedWindowLimiter($"register:{IpKey(context)}", _ => FixedWindow(options.RegistrationsPerWindow, TimeSpan.FromMinutes(options.RegistrationWindowMinutes)))
                : RateLimitPartition.GetNoLimiter(string.Empty));
            limiter.AddPolicy(RateLimitPolicies.Checkout, context => options.Enabled
                ? RateLimitPartition.GetSlidingWindowLimiter($"checkout:{CallerKey(context)}", _ => SlidingWindow(options.CheckoutsPerMinute, TimeSpan.FromMinutes(1)))
                : RateLimitPartition.GetNoLimiter(string.Empty));

            if (!options.Enabled)
            {
                return;
            }

            // Every request: reads and writes counted separately per caller.
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                bool isRead = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
                return RateLimitPartition.GetSlidingWindowLimiter(
                    $"{(isRead ? "read" : "write")}:{CallerKey(context)}",
                    _ => SlidingWindow(isRead ? options.ReadsPerMinute : options.WritesPerMinute, TimeSpan.FromMinutes(1)));
            });
        });

        return services;
    }

    /// <summary>
    /// Identifies the caller: the signed-in user, else the client IP.
    /// </summary>
    /// <param name="context">Request.</param>
    /// <returns>The partition key.</returns>
    private static string CallerKey(HttpContext context) =>
        context.User.FindFirst(ClaimNames.UserId)?.Value is { Length: > 0 } userId ? $"user:{userId}" : IpKey(context);

    /// <summary>
    /// Identifies the client IP (the real one when a trusted proxy forwarded it).
    /// </summary>
    /// <param name="context">Request.</param>
    /// <returns>The partition key.</returns>
    private static string IpKey(HttpContext context) =>
        $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

    /// <summary>
    /// A sliding window of six segments: smooth, with no burst at the window edge.
    /// </summary>
    /// <param name="permits">Requests allowed per window.</param>
    /// <param name="window">Window length.</param>
    /// <returns>The limiter options.</returns>
    private static SlidingWindowRateLimiterOptions SlidingWindow(int permits, TimeSpan window) => new()
    {
        PermitLimit = permits,
        Window = window,
        SegmentsPerWindow = 6,
        QueueLimit = 0,
        AutoReplenishment = true,
    };

    /// <summary>
    /// A fixed window, for attempts that should be counted strictly.
    /// </summary>
    /// <param name="permits">Attempts allowed per window.</param>
    /// <param name="window">Window length.</param>
    /// <returns>The limiter options.</returns>
    private static FixedWindowRateLimiterOptions FixedWindow(int permits, TimeSpan window) => new()
    {
        PermitLimit = permits,
        Window = window,
        QueueLimit = 0,
        AutoReplenishment = true,
    };

    /// <summary>
    /// Answers a refused request with 429, a <c>Retry-After</c> header and ProblemDetails.
    /// </summary>
    /// <param name="context">Rejection context.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>A task that completes when the answer is written.</returns>
    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        int seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
            : 60;
        HttpResponse response = context.HttpContext.Response;
        response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        string wait = seconds < 90 ? $"{seconds} seconds" : $"{(int)Math.Ceiling(seconds / 60.0)} minutes";
        await response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests",
                Detail = string.Format(CultureInfo.InvariantCulture, TooManyMessage, wait),
            },
            options: null,
            contentType: "application/problem+json",
            cancellationToken);
    }
}
