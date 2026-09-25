using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Platform.Shared.Constants;
using Platform.Shared.Entities.Identity;
using Platform.Shared.Entities.Sales;

namespace Platform.Api.Security;

/// <summary>
/// Issues access tokens.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Creates a signed JWT for a user.
    /// </summary>
    /// <param name="user">Authenticated user.</param>
    /// <returns>The token and its UTC expiry.</returns>
    (string Token, DateTime ExpiresAt) CreateAccessToken(User user);

    /// <summary>
    /// Creates a storefront token for a customer. It carries the customer
    /// actor claim, so staff endpoints refuse it and customer endpoints accept it.
    /// </summary>
    /// <param name="customer">Signed-in customer.</param>
    /// <returns>The encoded token and its UTC expiry.</returns>
    (string Token, DateTime ExpiresAt) CreateCustomerToken(Customer customer);
}

/// <summary>
/// HMAC-SHA256 JWT implementation of <see cref="ITokenService"/>.
/// </summary>
/// <remarks>
/// The token carries identity only (user, organisation). Capabilities and
/// scopes are deliberately NOT placed in it: they are checked against the
/// database on each request so a revoked scope takes effect immediately (P6).
/// </remarks>
public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="options">JWT settings.</param>
    /// <param name="timeProvider">Clock used for issue and expiry times.</param>
    public JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Builds the key used to sign and validate tokens. Shared with the
    /// JwtBearer setup so both sides always use the same key.
    /// </summary>
    /// <param name="options">JWT settings.</param>
    /// <returns>The symmetric signing key.</returns>
    public static SymmetricSecurityKey CreateSigningKey(JwtOptions options) =>
        new(Encoding.UTF8.GetBytes(options.SigningKey));

    /// <inheritdoc />
    public (string Token, DateTime ExpiresAt) CreateAccessToken(User user) =>
        Create(new[]
        {
            new Claim(ClaimNames.UserId, user.Id.ToString()),
            new Claim(ClaimNames.OrgId, user.OrgId.ToString()),
            new Claim(ClaimNames.Name, user.Name),
            new Claim(ClaimNames.Email, user.Email),
            new Claim(ClaimNames.Actor, Actors.Staff),
        }, _options.AccessTokenMinutes);

    /// <inheritdoc />
    public (string Token, DateTime ExpiresAt) CreateCustomerToken(Customer customer) =>
        Create(new[]
        {
            new Claim(ClaimNames.UserId, customer.Id.ToString()),
            new Claim(ClaimNames.OrgId, customer.OrgId.ToString()),
            new Claim(ClaimNames.Name, customer.Name),
            new Claim(ClaimNames.Email, customer.Email),
            new Claim(ClaimNames.Actor, Actors.Customer),
        }, _options.CustomerTokenMinutes);

    /// <summary>
    /// Signs a token with the given claims and lifetime.
    /// </summary>
    /// <param name="claims">Claims to carry.</param>
    /// <param name="minutes">Lifetime in minutes.</param>
    /// <returns>The encoded token and its UTC expiry.</returns>
    private (string Token, DateTime ExpiresAt) Create(Claim[] claims, int minutes)
    {
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        DateTime expiresAt = now.AddMinutes(minutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(CreateSigningKey(_options), SecurityAlgorithms.HmacSha256),
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }
}
