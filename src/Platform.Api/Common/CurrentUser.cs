using System.Security.Claims;
using Platform.Shared.Constants;

namespace Platform.Api.Common;

/// <summary>
/// Who is making the current request. Injected wherever user identity is
/// needed so nothing reads claims from <c>HttpContext</c> directly.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Signed-in user's id, or null for anonymous requests.</summary>
    Guid? UserId { get; }

    /// <summary>Signed-in user's organisation, or null for anonymous requests.</summary>
    Guid? OrgId { get; }
}

/// <summary>
/// Reads <see cref="ICurrentUser"/> from the JWT claims of the current HTTP request.
/// </summary>
public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Creates the accessor.
    /// </summary>
    /// <param name="httpContextAccessor">Gives access to the current request.</param>
    public HttpCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public Guid? UserId => ReadGuid(ClaimNames.UserId);

    /// <inheritdoc />
    public Guid? OrgId => ReadGuid(ClaimNames.OrgId);

    /// <summary>
    /// Reads a claim of the current principal and parses it as a <see cref="Guid"/>.
    /// </summary>
    /// <param name="claimType">Claim to read.</param>
    /// <returns>The parsed value, or null when absent or malformed.</returns>
    private Guid? ReadGuid(string claimType)
    {
        ClaimsPrincipal? principal = _httpContextAccessor.HttpContext?.User;
        string? value = principal?.FindFirst(claimType)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
