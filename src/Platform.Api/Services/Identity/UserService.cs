using Google.Cloud.Firestore;
using Platform.Api.Common;
using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Mapping;
using Platform.Api.Repositories;
using Platform.Api.Security;
using Platform.Api.Security.Authorization;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Identity;
using Platform.Shared.Entities.Identity;
using Platform.Shared.Entities.Inventory;

namespace Platform.Api.Services.Identity;

/// <summary>
/// Ends a user's open sessions (sign out everywhere).
/// </summary>
public interface IUserSessionService
{
    /// <summary>
    /// Ends every session the user has open now; they can sign in again at once.
    /// </summary>
    /// <param name="id">User id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes once saved.</returns>
    Task EndSessionsAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// CRUD for users. Keeps two records in step: the Firebase Authentication
/// account (credentials, sign-in enabled) and the platform user in Firestore
/// (profile, organisation, roles, scopes).
/// </summary>
public sealed class UserService : CrudService<User, UserDto, CreateUserRequest, UpdateUserRequest>, IUserSessionService
{
    private readonly IRepository<Role> _roles;
    private readonly IIdentityProvider _identityProvider;
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionService _permissions;
    private readonly IRepository<Warehouse> _warehouses;
    private readonly IAccessHierarchy _hierarchy;
    private readonly TimeProvider _clock;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="repository">User data access.</param>
    /// <param name="unitOfWork">Commits staged changes.</param>
    /// <param name="mapper">User mapping.</param>
    /// <param name="roles">Role data access, to validate assigned roles.</param>
    /// <param name="identityProvider">Firebase Authentication account management.</param>
    /// <param name="currentUser">Caller, to stop self-deactivation.</param>
    /// <param name="permissions">Caller's access, to stop granting access they do not hold.</param>
    /// <param name="warehouses">Warehouse data access, to validate warehouse scopes.</param>
    /// <param name="hierarchy">Role hierarchy rules: who may manage whom.</param>
    /// <param name="clock">Current time, for ending sessions.</param>
    public UserService(
        IRepository<User> repository,
        IUnitOfWork unitOfWork,
        IEntityMapper<User, UserDto, CreateUserRequest, UpdateUserRequest> mapper,
        IRepository<Role> roles,
        IIdentityProvider identityProvider,
        ICurrentUser currentUser,
        IPermissionService permissions,
        IRepository<Warehouse> warehouses,
        IAccessHierarchy hierarchy,
        TimeProvider clock)
        : base(repository, unitOfWork, mapper)
    {
        _roles = roles;
        _identityProvider = identityProvider;
        _currentUser = currentUser;
        _permissions = permissions;
        _warehouses = warehouses;
        _hierarchy = hierarchy;
        _clock = clock;
    }

    /// <summary>
    /// Creates the Firebase account first, then the platform user. If saving
    /// the user fails, the account is deleted again so no orphan can sign in.
    /// </summary>
    /// <param name="request">Validated create request.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The created user.</returns>
    public override async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureRolesUsableAsync(request.RoleIds, cancellationToken);
        await _hierarchy.EnsureCanGiveRolesAsync(request.RoleIds, cancellationToken);
        await EnsureAccessChangesAllowedAsync(request, new User(), cancellationToken);

        User entity = Mapper.ToEntity(request);
        entity.AuthUid = await _identityProvider.CreateAccountAsync(entity.Email, request.Password, entity.Name, cancellationToken);

        try
        {
            Repository.Add(entity);
            await UnitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _identityProvider.DeleteAccountAsync(entity.AuthUid, CancellationToken.None);
            throw;
        }

        return Mapper.ToDto(entity);
    }

    /// <summary>
    /// Updates profile, roles, scopes and status; a status change is mirrored
    /// to the Firebase account so a deactivated user cannot sign in, and ends
    /// their open sessions at once. Someone other than an administrator may
    /// change only people below them in the hierarchy, and not their own access.
    /// </summary>
    /// <param name="id">User id.</param>
    /// <param name="request">Validated update request.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The updated user.</returns>
    public override async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        User entity = await LoadAsync(id, cancellationToken);
        bool wasActive = entity.IsActive;

        if (!request.IsActive)
        {
            EnsureNotSelf(id);
        }

        await EnsureCanChangeAsync(entity, request, cancellationToken);
        await EnsureRolesUsableAsync(request.RoleIds.Except(entity.RoleIds), cancellationToken);
        await _hierarchy.EnsureCanGiveRolesAsync(RoleChanges(entity.RoleIds, request.RoleIds), cancellationToken);
        await EnsureAccessChangesAllowedAsync(request, entity, cancellationToken);
        await _hierarchy.EnsureAdministratorRemainsAsync(
            entity,
            request.IsActive && await _hierarchy.IncludesAdministratorAsync(request.RoleIds, cancellationToken),
            cancellationToken);

        Mapper.Apply(request, entity);
        if (wasActive && !entity.IsActive)
        {
            entity.SessionsEndedAt = Now;
        }

        Repository.Update(entity);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        if (wasActive != entity.IsActive)
        {
            await _identityProvider.SetDisabledAsync(entity.AuthUid, !entity.IsActive, cancellationToken);
        }

        return Mapper.ToDto(entity);
    }

    /// <summary>
    /// Deactivates the user, ends their open sessions and disables their
    /// Firebase account. Only for someone below the caller in the hierarchy,
    /// and never the last active administrator.
    /// </summary>
    /// <param name="id">User id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes once both are updated.</returns>
    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureNotSelf(id);
        User entity = await LoadAsync(id, cancellationToken);
        await EnsureCanManageAsync(entity, cancellationToken);
        await _hierarchy.EnsureAdministratorRemainsAsync(entity, keepsAdministrator: false, cancellationToken);

        entity.IsActive = false;
        entity.SessionsEndedAt = Now;
        Repository.Update(entity);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
        await _identityProvider.SetDisabledAsync(entity.AuthUid, disabled: true, cancellationToken);
    }

    /// <summary>
    /// Signs a user out on every device now. They can sign in again at once;
    /// use it after a lost phone, a shared computer or a suspicious sign-in.
    /// Allowed for oneself, or for someone below the caller in the hierarchy.
    /// </summary>
    /// <param name="id">User id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes once saved.</returns>
    public async Task EndSessionsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        User entity = await LoadAsync(id, cancellationToken);
        if (!IsSelf(id))
        {
            await EnsureCanManageAsync(entity, cancellationToken);
        }

        entity.SessionsEndedAt = Now;
        Repository.Update(entity);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Prefix match on email (always stored lower-case).
    /// </summary>
    /// <param name="query">Org-scoped query.</param>
    /// <param name="search">Trimmed search text.</param>
    /// <returns>The filtered query, ordered by email.</returns>
    protected override Query ApplySearch(Query query, string search)
    {
        string field = FirestoreNaming.Field(nameof(User.Email));
        string prefix = search.ToLowerInvariant();
        return query.WhereStartsWith(field, prefix);
    }

    /// <summary>
    /// Sorts users by name.
    /// </summary>
    /// <param name="query">Org-scoped query.</param>
    /// <returns>The ordered query.</returns>
    protected override Query ApplyOrder(Query query) =>
        query.OrderBy(FirestoreNaming.Field(nameof(User.Name)));

    /// <summary>
    /// Checks that every role exists in the caller's organisation and is active.
    /// </summary>
    /// <param name="roleIds">Roles being newly assigned.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when all roles are usable.</returns>
    /// <exception cref="FieldValidationException">A role is unknown or inactive.</exception>
    private async Task EnsureRolesUsableAsync(IEnumerable<Guid> roleIds, CancellationToken cancellationToken)
    {
        List<Guid> requested = roleIds.Distinct().ToList();
        if (requested.Count == 0)
        {
            return;
        }

        IReadOnlyList<Role> found = await _roles.GetByIdsAsync(requested, cancellationToken);
        if (found.Count != requested.Count || found.Any(r => !r.IsActive))
        {
            throw new FieldValidationException(nameof(IUserFields.RoleIds), "One or more roles do not exist or are inactive.");
        }
    }

    /// <summary>
    /// Checks every change to a user's access before it is saved, so nobody can
    /// hand out (or take away) more than they hold themselves:
    /// <list type="bullet">
    /// <item>a role scope being added or removed must be one where the caller manages users;</item>
    /// <item>anything a newly given role or role scope would grant (each capability in each
    /// place) must be held by the caller there — so nobody can hand out the Administrator role
    /// without being an administrator;</item>
    /// <item>a feature access grant (capability in a place) being added or removed must be
    /// one the caller holds in that same place;</item>
    /// <item>every warehouse named must exist in the organisation.</item>
    /// </list>
    /// Unchanged grants are not re-checked, so an editor with less access can
    /// still edit a user's profile without losing that user's other access.
    /// </summary>
    /// <param name="request">Requested roles scopes and feature access.</param>
    /// <param name="current">The user as stored (a blank user when creating).</param>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <returns>A task that completes when every change is allowed.</returns>
    /// <exception cref="ForbiddenException">The caller does not hold something being changed.</exception>
    /// <exception cref="FieldValidationException">A scope names an unknown warehouse or an unsupported type.</exception>
    private async Task EnsureAccessChangesAllowedAsync(IUserFields request, User current, CancellationToken cancellationToken)
    {
        var wantedScopes = request.Scopes.Select(s => (s.ScopeType, s.ScopeId)).ToHashSet();
        var heldScopes = current.Scopes.Select(s => (s.ScopeType, s.ScopeId)).ToHashSet();
        foreach (var (scopeType, scopeId) in wantedScopes.Except(heldScopes).Concat(heldScopes.Except(wantedScopes)))
        {
            if (!await CallerHoldsAsync(Capabilities.UsersManage, scopeType, scopeId, cancellationToken))
            {
                throw new ForbiddenException("You can only give or remove role scopes where you manage users yourself.");
            }
        }

        HashSet<AccessGrant> wantedRoleGrants = ExpandRoles(await ActiveRolesAsync(request.RoleIds, cancellationToken), wantedScopes);
        HashSet<AccessGrant> heldRoleGrants = ExpandRoles(await ActiveRolesAsync(current.RoleIds, cancellationToken), heldScopes);
        foreach (AccessGrant grant in wantedRoleGrants.Except(heldRoleGrants))
        {
            if (!await CallerHoldsAsync(grant, cancellationToken))
            {
                throw new ForbiddenException("You can only give roles whose access you hold yourself, in the places they would apply.");
            }
        }

        List<FeatureAccessDto> wantedAccess = UserFieldsNormaliser.Normalise(request.Access);
        HashSet<AccessGrant> wantedGrants = ExpandAccess(wantedAccess.Select(a => (a.Feature, a.Level, a.Scopes.Select(s => (s.ScopeType, s.ScopeId)))));
        HashSet<AccessGrant> heldGrants = ExpandAccess(current.Access.Select(a => (a.Feature, a.Level, a.Scopes.Select(s => (s.ScopeType, s.ScopeId)))));
        foreach (AccessGrant grant in wantedGrants.Except(heldGrants).Concat(heldGrants.Except(wantedGrants)))
        {
            if (!await CallerHoldsAsync(grant, cancellationToken))
            {
                throw new ForbiddenException("You can only give or remove access you hold yourself, in the same places.");
            }
        }

        var newScopes = wantedScopes.Except(heldScopes)
            .Concat(wantedGrants.Except(heldGrants).Where(g => g.ScopeType.HasValue).Select(g => (g.ScopeType!.Value, g.ScopeId)))
            .ToList();

        if (newScopes.Any(s => s.Item1 is not (ScopeType.Global or ScopeType.Warehouse)))
        {
            throw new FieldValidationException(nameof(IUserFields.Scopes), "Only global and warehouse scopes are available so far.");
        }

        List<Guid> newWarehouseIds = newScopes
            .Where(s => s.Item1 == ScopeType.Warehouse)
            .Select(s => s.Item2!.Value)
            .Distinct()
            .ToList();
        if (newWarehouseIds.Count > 0
            && (await _warehouses.GetByIdsAsync(newWarehouseIds, cancellationToken)).Count != newWarehouseIds.Count)
        {
            throw new FieldValidationException(nameof(IUserFields.Scopes), "One or more warehouses do not exist.");
        }
    }

    /// <summary>
    /// Checks whether the caller holds a capability in one place: everywhere
    /// for a global scope, or covering that object otherwise.
    /// </summary>
    /// <param name="capability">Capability code.</param>
    /// <param name="scopeType">Kind of place.</param>
    /// <param name="scopeId">The object, or null for global.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>True when the caller holds it there.</returns>
    private async Task<bool> CallerHoldsAsync(string capability, ScopeType scopeType, Guid? scopeId, CancellationToken cancellationToken) =>
        scopeType == ScopeType.Global
            ? await _permissions.HasGlobalAsync(capability, cancellationToken)
            : await _permissions.CoversAsync(capability, scopeType, scopeId!.Value, cancellationToken);

    /// <summary>
    /// Checks whether the caller holds one grant: the capability anywhere for an
    /// organisation-wide feature, or in the grant's place otherwise.
    /// </summary>
    /// <param name="grant">Grant being given or removed.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>True when the caller holds it.</returns>
    private async Task<bool> CallerHoldsAsync(AccessGrant grant, CancellationToken cancellationToken) =>
        grant.ScopeType is { } type
            ? await CallerHoldsAsync(grant.Capability, type, grant.ScopeId, cancellationToken)
            : await _permissions.HasCapabilityAsync(grant.Capability, cancellationToken);

    /// <summary>
    /// Loads the active roles among some ids.
    /// </summary>
    /// <param name="roleIds">Role ids.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The active roles found.</returns>
    private async Task<IReadOnlyList<Role>> ActiveRolesAsync(IEnumerable<Guid> roleIds, CancellationToken cancellationToken)
    {
        List<Guid> ids = roleIds.Distinct().ToList();
        return ids.Count == 0
            ? Array.Empty<Role>()
            : (await _roles.GetByIdsAsync(ids, cancellationToken)).Where(r => r.IsActive).ToList();
    }

    /// <summary>
    /// Flattens roles into individual grants: each capability of each role, in
    /// each of the user's role scopes (once, for organisation-wide features).
    /// </summary>
    /// <param name="roles">Active roles.</param>
    /// <param name="scopes">Where the roles apply.</param>
    /// <returns>The grants.</returns>
    private static HashSet<AccessGrant> ExpandRoles(IEnumerable<Role> roles, IReadOnlyCollection<(ScopeType, Guid?)> scopes)
    {
        var grants = new HashSet<AccessGrant>();
        foreach (string capability in roles.SelectMany(r => r.Capabilities))
        {
            AddGrants(grants, capability, Features.OfCapability(capability), scopes);
        }

        return grants;
    }

    /// <summary>
    /// Flattens feature access rows into individual grants — one per capability
    /// per place — so two versions can be compared grant by grant.
    /// </summary>
    /// <param name="rows">Feature, level and scopes of each row.</param>
    /// <returns>The grants; organisation-wide features give grants with no scope.</returns>
    private static HashSet<AccessGrant> ExpandAccess(IEnumerable<(string Feature, AccessLevel Level, IEnumerable<(ScopeType, Guid?)> Scopes)> rows)
    {
        var grants = new HashSet<AccessGrant>();
        foreach (var (code, level, scopes) in rows)
        {
            if (Features.Find(code) is not { } feature)
            {
                continue;
            }

            List<(ScopeType, Guid?)> places = scopes.ToList();
            foreach (string capability in feature.CapabilitiesFor(level))
            {
                AddGrants(grants, capability, feature, places);
            }
        }

        return grants;
    }

    /// <summary>
    /// Adds one capability's grants: a single place-less grant for an
    /// organisation-wide feature, otherwise one per place.
    /// </summary>
    /// <param name="grants">Set being built.</param>
    /// <param name="capability">Capability code.</param>
    /// <param name="feature">Its feature, or null when unknown (treated as organisation-wide).</param>
    /// <param name="places">Where it applies.</param>
    private static void AddGrants(HashSet<AccessGrant> grants, string capability, FeatureInfo? feature, IEnumerable<(ScopeType, Guid?)> places)
    {
        if (feature is not { IsScoped: true })
        {
            grants.Add(new AccessGrant(capability, null, null));
            return;
        }

        foreach (var (scopeType, scopeId) in places)
        {
            grants.Add(new AccessGrant(capability, scopeType, scopeId));
        }
    }

    /// <summary>
    /// Checks who may change this user: an administrator may change anyone; others
    /// may change only people below them, and on their own record only the profile
    /// (never their own roles, places or feature access).
    /// </summary>
    /// <param name="current">The user as stored.</param>
    /// <param name="request">Requested fields.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when allowed.</returns>
    /// <exception cref="ForbiddenException">The change is not the caller's to make.</exception>
    private async Task EnsureCanChangeAsync(User current, IUserFields request, CancellationToken cancellationToken)
    {
        if (!IsSelf(current.Id))
        {
            await EnsureCanManageAsync(current, cancellationToken);
            return;
        }

        if (await _hierarchy.IsAdministratorAsync(cancellationToken))
        {
            return;
        }

        UserDto before = Mapper.ToDto(current);
        bool accessChanged =
            !request.RoleIds.ToHashSet().SetEquals(before.RoleIds)
            || !request.Scopes.Select(s => s.ToString()).ToHashSet().SetEquals(before.Scopes.Select(s => s.ToString()))
            || !Describe(UserFieldsNormaliser.Normalise(request.Access)).SetEquals(Describe(UserFieldsNormaliser.Normalise(before.Access)));
        if (accessChanged)
        {
            throw new ForbiddenException("You cannot change your own roles or access. Ask someone above you.");
        }
    }

    /// <summary>
    /// Checks the caller may manage an existing user: every role they hold is
    /// below the caller's, and the caller holds every feature given to them
    /// directly, so nobody can edit or deactivate a peer or a superior.
    /// </summary>
    /// <param name="target">User as stored.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when allowed.</returns>
    /// <exception cref="ForbiddenException">The user is not below the caller.</exception>
    private async Task EnsureCanManageAsync(User target, CancellationToken cancellationToken)
    {
        await _hierarchy.EnsureCanManageUserAsync(target, cancellationToken);
        if (await _hierarchy.IsAdministratorAsync(cancellationToken))
        {
            return;
        }

        HashSet<AccessGrant> grants = ExpandAccess(target.Access.Select(a => (a.Feature, a.Level, a.Scopes.Select(s => (s.ScopeType, s.ScopeId)))));
        foreach (AccessGrant grant in grants)
        {
            if (!await CallerHoldsAsync(grant, cancellationToken))
            {
                throw new ForbiddenException(AccessHierarchy.UserNotBelowMessage);
            }
        }
    }

    /// <summary>
    /// Roles given or taken away by an update.
    /// </summary>
    /// <param name="before">Roles held now.</param>
    /// <param name="after">Roles requested.</param>
    /// <returns>The roles that change hands.</returns>
    private static IEnumerable<Guid> RoleChanges(IEnumerable<Guid> before, IEnumerable<Guid> after)
    {
        var old = before.ToHashSet();
        var wanted = after.ToHashSet();
        return wanted.Except(old).Concat(old.Except(wanted));
    }

    /// <summary>
    /// Flattens feature access rows into comparable text, one entry per feature, level and place.
    /// </summary>
    /// <param name="rows">Feature access rows.</param>
    /// <returns>The entries.</returns>
    private static HashSet<string> Describe(IEnumerable<FeatureAccessDto> rows) =>
        rows.SelectMany(a => a.Scopes.Select(s => s.ToString()).DefaultIfEmpty(string.Empty).Select(s => $"{a.Feature}|{a.Level}|{s}"))
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Whether a user id is the caller's own.
    /// </summary>
    /// <param name="id">User id.</param>
    /// <returns>True for the caller.</returns>
    private bool IsSelf(Guid id) => _currentUser.UserId == id;

    /// <summary>Current UTC time.</summary>
    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Stops a user from deactivating themselves and locking the organisation out.
    /// </summary>
    /// <param name="id">User being deactivated.</param>
    /// <exception cref="BusinessRuleException">The caller is deactivating themselves.</exception>
    private void EnsureNotSelf(Guid id)
    {
        if (IsSelf(id))
        {
            throw new BusinessRuleException("You cannot deactivate your own account.");
        }
    }

    /// <summary>
    /// One capability in one place, as granted by a feature access row.
    /// </summary>
    /// <param name="Capability">Capability code.</param>
    /// <param name="ScopeType">Kind of place; null for an organisation-wide feature.</param>
    /// <param name="ScopeId">The object; null for global or organisation-wide.</param>
    private sealed record AccessGrant(string Capability, ScopeType? ScopeType, Guid? ScopeId);
}
