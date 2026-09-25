using System.ComponentModel.DataAnnotations;

namespace Platform.Api.Security;

/// <summary>
/// JWT settings, bound from the <c>Jwt</c> configuration section and
/// validated at startup so a missing key fails fast instead of at first login.
/// </summary>
public sealed class JwtOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Jwt";

    /// <summary>Token issuer (<c>iss</c>).</summary>
    [Required]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Token audience (<c>aud</c>).</summary>
    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>HMAC-SHA256 signing key; at least 32 characters. Never commit a real one.</summary>
    [Required, MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Lifetime of an access token in minutes.</summary>
    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 60;

    /// <summary>Lifetime of a storefront customer's token, in minutes (default 12 hours).</summary>
    [Range(1, 10080)]
    public int CustomerTokenMinutes { get; set; } = 720;
}
