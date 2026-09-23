using Platform.Api.Common;
using Platform.Api.Repositories;
using Platform.Shared.Entities.Identity;

namespace Platform.Api.Security.Authorization;

/// <summary>
/// Answers the two P6 questions for the current caller: does their role hold
/// this capability, and is this object inside their scope. Always read from
/// the database for the current request — never from the token — so revoking
/// a role, scope or the user takes effect on the very next call.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Returns every capability the caller holds right now through their active roles.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Capability codes; empty for an unknown or inactive user.</returns>
    Task<IReadOnlySet<string>> GetCapabilitiesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks one capability.
    /// </summary>
    /// <param name="capability">Code from <c>Capabilities</c>.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>True when the caller holds it.</returns>
    Task<bool> HasCapabilityAsync(string capability, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether an object is inside the caller's scope. A global grant
    /// covers everything. Callers must answer 404 — never 403 — when this is false.
    /// </summary>
    /// <param name="scopeType">Kind of object.</param>
    /// <param name="scopeId">The object's id.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>True when covered.</returns>
    Task<bool> CoversAsync(ScopeType scopeType, Guid scopeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the objects of one type the caller is scoped to.
    /// </summary>
    /// <param name="scopeType">Kind of object.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Null when the caller has global scope (no restriction); otherwise the ids they may see (possibly empty).</returns>
    Task<IReadOnlySet<Guid>?> GetScopeIdsAsync(ScopeType scopeType, CancellationToken cancellationToken = default);
}

/// <summary>
/// Firestore-backed <see cref="IPermissionService"/>. Scoped per request; loads
/// the user and roles once and reuses them for every check in that request.
/// </summary>
public sealed class PermissionService : IPermissionService
{
    private readonly ICurrentUser _currentUser;
    private readonly IRepository<User> _users;
    private readonly IRepository<Role> _roles;
    private (User? User, IReadOnlySet<string> Capabilities)? _loaded;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="currentUser">Caller identity from the token.</param>
    /// <param name="users">User data access (org-scoped).</param>
    /// <param name="roles">Role data access (org-scoped).</param>
    public PermissionService(ICurrentUser currentUser, IRepository<User> users, IRepository<Role> roles)
    {
        _currentUser = currentUser;
        _users = users;
        _roles = roles;
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> GetCapabilitiesAsync(CancellationToken cancellationToken = default) =>
        (await LoadAsync(cancellationToken)).Capabilities;

    /// <inheritdoc />
    public async Task<bool> HasCapabilityAsync(string capability, CancellationToken cancellationToken = default) =>
        (await GetCapabilitiesAsync(cancellationToken)).Contains(capability);

    /// <inheritdoc />
    public async Task<bool> CoversAsync(ScopeType scopeType, Guid scopeId, CancellationToken cancellationToken = default)
    {
        User? user = (await LoadAsync(cancellationToken)).User;
        return user is not null && user.Scopes.Any(s =>
            s.ScopeType == ScopeType.Global || (s.ScopeType == scopeType && s.ScopeId == scopeId));
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<Guid>?> GetScopeIdsAsync(ScopeType scopeType, CancellationToken cancellationToken = default)
    {
        User? user = (await LoadAsync(cancellationToken)).User;
        if (user is null)
        {
            return new HashSet<Guid>();
        }

        if (user.Scopes.Any(s => s.ScopeType == ScopeType.Global))
        {
            return null;
        }

        return user.Scopes
            .Where(s => s.ScopeType == scopeType && s.ScopeId.HasValue)
            .Select(s => s.ScopeId!.Value)
            .ToHashSet();
    }

    /// <summary>
    /// Loads the caller and the union of their active roles' capabilities,
    /// once per request.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The user (null when unknown or inactive) and their capabilities.</returns>
    private async Task<(User? User, IReadOnlySet<string> Capabilities)> LoadAsync(CancellationToken cancellationToken)
    {
        if (_loaded is { } cached)
        {
            return cached;
        }

        User? user = _currentUser.UserId is { } userId
            ? await _users.GetByIdAsync(userId, cancellationToken)
            : null;

        if (user is null || !user.IsActive)
        {
            _loaded = (null, new HashSet<string>());
            return _loaded.Value;
        }

        IReadOnlyList<Role> roles = await _roles.GetByIdsAsync(user.RoleIds, cancellationToken);
        var capabilities = roles
            .Where(r => r.IsActive)
            .SelectMany(r => r.Capabilities)
            .ToHashSet(StringComparer.Ordinal);

        _loaded = (user, capabilities);
        return _loaded.Value;
    }
}
