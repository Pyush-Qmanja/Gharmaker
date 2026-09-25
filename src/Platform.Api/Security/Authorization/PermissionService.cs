using Platform.Api.Repositories;
using Platform.Shared.Constants;
using Platform.Shared.Entities.Identity;

namespace Platform.Api.Security.Authorization;

/// <summary>
/// Answers the two P6 questions for the current caller: do they hold this
/// capability, and is this object inside the scope where they hold it. Always
/// read from the database for the current request — never from the token — so
/// revoking a role, access row, scope or the user takes effect on the very next call.
/// </summary>
/// <remarks>
/// A capability can come from two places, each with its own scopes:
/// a role (applies in the user's <see cref="User.Scopes"/>) and a direct
/// feature access row (applies in that row's own scopes). The caller's scope
/// for a capability is the union of both.
/// </remarks>
public interface IPermissionService
{
    /// <summary>
    /// Returns every capability the caller holds right now, from roles and direct access.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Capability codes; empty for an unknown or inactive user.</returns>
    Task<IReadOnlySet<string>> GetCapabilitiesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks one capability, anywhere.
    /// </summary>
    /// <param name="capability">Code from <c>Capabilities</c>.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>True when the caller holds it.</returns>
    Task<bool> HasCapabilityAsync(string capability, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the caller holds a capability for one object. A global
    /// grant covers everything. For reads, callers must answer 404 — never 403 —
    /// when this is false.
    /// </summary>
    /// <param name="capability">Code from <c>Capabilities</c>.</param>
    /// <param name="scopeType">Kind of object.</param>
    /// <param name="scopeId">The object's id.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>True when covered.</returns>
    Task<bool> CoversAsync(string capability, ScopeType scopeType, Guid scopeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the caller holds a capability everywhere in the organisation.
    /// </summary>
    /// <param name="capability">Code from <c>Capabilities</c>.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>True when a global grant carries it.</returns>
    Task<bool> HasGlobalAsync(string capability, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the objects of one type where the caller holds a capability.
    /// </summary>
    /// <param name="capability">Code from <c>Capabilities</c>.</param>
    /// <param name="scopeType">Kind of object.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Null when a global grant carries it (no restriction); otherwise the ids (possibly empty).</returns>
    Task<IReadOnlySet<Guid>?> GetScopeIdsAsync(string capability, ScopeType scopeType, CancellationToken cancellationToken = default);
}

/// <summary>
/// Firestore-backed <see cref="IPermissionService"/>. Scoped per request; loads
/// the user and roles once and reuses them for every check in that request.
/// </summary>
public sealed class PermissionService : IPermissionService
{
    /// <summary>Nothing granted: unknown or inactive caller.</summary>
    private static readonly IReadOnlyDictionary<string, List<ScopeGrant>> NoGrants = new Dictionary<string, List<ScopeGrant>>();

    /// <summary>The scope list given to organisation-wide features.</summary>
    private static readonly ScopeGrant[] Everywhere = { new() { ScopeType = ScopeType.Global } };

    private readonly ICallerAccount _caller;
    private readonly IRepository<Role> _roles;
    private IReadOnlyDictionary<string, List<ScopeGrant>>? _grants;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="caller">The calling staff user, loaded once per request.</param>
    /// <param name="roles">Role data access (org-scoped).</param>
    public PermissionService(ICallerAccount caller, IRepository<Role> roles)
    {
        _caller = caller;
        _roles = roles;
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> GetCapabilitiesAsync(CancellationToken cancellationToken = default) =>
        (await LoadAsync(cancellationToken)).Keys.ToHashSet(StringComparer.Ordinal);

    /// <inheritdoc />
    public async Task<bool> HasCapabilityAsync(string capability, CancellationToken cancellationToken = default) =>
        (await LoadAsync(cancellationToken)).ContainsKey(capability);

    /// <inheritdoc />
    public async Task<bool> CoversAsync(string capability, ScopeType scopeType, Guid scopeId, CancellationToken cancellationToken = default) =>
        (await LoadAsync(cancellationToken)).TryGetValue(capability, out List<ScopeGrant>? scopes)
        && scopes.Any(s => s.ScopeType == ScopeType.Global || (s.ScopeType == scopeType && s.ScopeId == scopeId));

    /// <inheritdoc />
    public async Task<bool> HasGlobalAsync(string capability, CancellationToken cancellationToken = default) =>
        (await LoadAsync(cancellationToken)).TryGetValue(capability, out List<ScopeGrant>? scopes)
        && scopes.Any(s => s.ScopeType == ScopeType.Global);

    /// <inheritdoc />
    public async Task<IReadOnlySet<Guid>?> GetScopeIdsAsync(string capability, ScopeType scopeType, CancellationToken cancellationToken = default)
    {
        if (!(await LoadAsync(cancellationToken)).TryGetValue(capability, out List<ScopeGrant>? scopes))
        {
            return new HashSet<Guid>();
        }

        if (scopes.Any(s => s.ScopeType == ScopeType.Global))
        {
            return null;
        }

        return scopes
            .Where(s => s.ScopeType == scopeType && s.ScopeId.HasValue)
            .Select(s => s.ScopeId!.Value)
            .ToHashSet();
    }

    /// <summary>
    /// Builds, once per request, the map from each capability the caller holds
    /// to the scopes where they hold it: active roles contribute their
    /// capabilities in the user's role scopes; each direct access row
    /// contributes its level's capabilities in its own scopes (everywhere, for
    /// an organisation-wide feature).
    /// </summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Capability to scopes; empty for an unknown or inactive user.</returns>
    private async Task<IReadOnlyDictionary<string, List<ScopeGrant>>> LoadAsync(CancellationToken cancellationToken)
    {
        if (_grants is not null)
        {
            return _grants;
        }

        User? user = await _caller.GetAsync(cancellationToken);

        if (user is null || !user.IsActive)
        {
            return _grants = NoGrants;
        }

        var grants = new Dictionary<string, List<ScopeGrant>>(StringComparer.Ordinal);

        IReadOnlyList<Role> roles = await _roles.GetByIdsAsync(user.RoleIds, cancellationToken);
        // The built-in top role always carries every capability, including ones added after it was saved.
        IEnumerable<string> roleCapabilities = roles
            .Where(r => r.IsActive)
            .SelectMany(r => r.IsSystem ? Capabilities.All.Select(c => c.Code) : r.Capabilities)
            .Distinct(StringComparer.Ordinal);
        foreach (string capability in roleCapabilities)
        {
            Grant(grants, capability, user.Scopes);
        }

        foreach (FeatureAccess row in user.Access)
        {
            if (Features.Find(row.Feature) is not { } feature)
            {
                continue;
            }

            foreach (string capability in feature.CapabilitiesFor(row.Level))
            {
                Grant(grants, capability, feature.IsScoped ? row.Scopes : Everywhere);
            }
        }

        return _grants = grants;
    }

    /// <summary>
    /// Adds scopes to one capability's entry in the map.
    /// </summary>
    /// <param name="grants">Map being built.</param>
    /// <param name="capability">Capability code.</param>
    /// <param name="scopes">Where it is held.</param>
    private static void Grant(Dictionary<string, List<ScopeGrant>> grants, string capability, IEnumerable<ScopeGrant> scopes)
    {
        if (!grants.TryGetValue(capability, out List<ScopeGrant>? list))
        {
            grants[capability] = list = new List<ScopeGrant>();
        }

        list.AddRange(scopes);
    }
}
