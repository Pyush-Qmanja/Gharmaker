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
/// Reads <see cref="ICurrentUser"/> from the JWT claims of the current HTTP request,
/// unless code is running inside <see cref="SystemIdentity.Use"/> (startup and background jobs).
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
    public Guid? UserId => SystemIdentity.Current is { } system ? system.UserId : ReadGuid(ClaimNames.UserId);

    /// <inheritdoc />
    public Guid? OrgId => SystemIdentity.Current?.OrgId ?? ReadGuid(ClaimNames.OrgId);

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

/// <summary>
/// Lets startup and background work act on behalf of an organisation, so
/// tenancy filtering and audit stamping behave exactly as in a web request.
/// </summary>
/// <example>
/// <code>
/// using (SystemIdentity.Use(orgId, adminUserId)) { await importService.CommitAsync(...); }
/// </code>
/// </example>
public static class SystemIdentity
{
    /// <summary>Identity for the current async flow, if any.</summary>
    private static readonly AsyncLocal<(Guid OrgId, Guid? UserId)?> Scope = new();

    /// <summary>The identity in effect, or null inside a normal request.</summary>
    internal static (Guid OrgId, Guid? UserId)? Current => Scope.Value;

    /// <summary>
    /// Acts as the given organisation (and optionally user) until disposed.
    /// </summary>
    /// <param name="orgId">Organisation to act for.</param>
    /// <param name="userId">User recorded in audit fields; null for "system".</param>
    /// <returns>A handle that restores the previous identity when disposed.</returns>
    public static IDisposable Use(Guid orgId, Guid? userId)
    {
        var previous = Scope.Value;
        Scope.Value = (orgId, userId);
        return new Restore(previous);
    }

    /// <summary>
    /// Restores the identity that was in effect before <see cref="Use"/>.
    /// </summary>
    /// <param name="Previous">Identity to restore.</param>
    private sealed record Restore((Guid OrgId, Guid? UserId)? Previous) : IDisposable
    {
        /// <summary>
        /// Puts the previous identity back.
        /// </summary>
        public void Dispose() => Scope.Value = Previous;
    }
}
