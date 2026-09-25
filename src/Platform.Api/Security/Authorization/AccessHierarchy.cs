using Google.Cloud.Firestore;
using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Repositories;
using Platform.Shared.Common;
using Platform.Shared.Dtos.Identity;
using Platform.Shared.Entities.Identity;

namespace Platform.Api.Security.Authorization;

/// <summary>
/// The role hierarchy rules. Administrators (holders of the built-in top role)
/// may manage every role and user; everyone else only what is strictly below a
/// role they hold. Works alongside the "only give what you hold" checks, never
/// instead of them. Nothing here looks at a role's name (P6).
/// </summary>
public interface IAccessHierarchy
{
    /// <summary>
    /// Whether the caller holds the built-in top role.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>True for an administrator.</returns>
    Task<bool> IsAdministratorAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Refuses unless the caller may change or deactivate an existing role.
    /// </summary>
    /// <param name="role">Role as stored.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when allowed.</returns>
    /// <exception cref="ForbiddenException">The role is the top role, or not below the caller.</exception>
    Task EnsureCanManageRoleAsync(Role role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refuses unless a role (new, or existing when <paramref name="roleId"/> is
    /// set) may report to the chosen parent.
    /// </summary>
    /// <param name="parentId">Chosen parent; null means directly under the top role.</param>
    /// <param name="roleId">The role being moved, or null for a new role.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when allowed.</returns>
    /// <exception cref="FieldValidationException">The parent is unknown or would create a loop.</exception>
    /// <exception cref="ForbiddenException">The parent is not at or below the caller's roles.</exception>
    Task EnsureCanPlaceRoleAsync(Guid? parentId, Guid? roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refuses unless every role being given or taken away is below the caller.
    /// </summary>
    /// <param name="roleIds">Roles changing hands.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when allowed.</returns>
    /// <exception cref="ForbiddenException">A role is not below the caller.</exception>
    Task EnsureCanGiveRolesAsync(IEnumerable<Guid> roleIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refuses unless every role a user holds is below the caller, so nobody can
    /// change or deactivate a peer or someone above them.
    /// </summary>
    /// <param name="user">User as stored.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when allowed.</returns>
    /// <exception cref="ForbiddenException">The user is not below the caller.</exception>
    Task EnsureCanManageUserAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refuses when a change would leave the organisation with no active administrator.
    /// </summary>
    /// <param name="user">User as stored, before the change.</param>
    /// <param name="keepsAdministrator">Whether the user is still an active administrator after it.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when another active administrator remains.</returns>
    /// <exception cref="BusinessRuleException">The user is the last active administrator.</exception>
    Task EnsureAdministratorRemainsAsync(User user, bool keepsAdministrator, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a user holds the built-in top role among some role ids.
    /// </summary>
    /// <param name="roleIds">Role ids.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>True when the top role is among them.</returns>
    Task<bool> IncludesAdministratorAsync(IEnumerable<Guid> roleIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Firestore-backed <see cref="IAccessHierarchy"/>. Scoped per request; reads
/// the organisation's roles once.
/// </summary>
public sealed class AccessHierarchy : IAccessHierarchy
{
    /// <summary>Refusal for a role outside the caller's branch.</summary>
    public const string RoleNotBelowMessage = "You can only change roles below your own in the hierarchy.";

    /// <summary>Refusal for a user outside the caller's branch.</summary>
    public const string UserNotBelowMessage = "You can only change people below you in the hierarchy.";

    /// <summary>Refusal for giving a role outside the caller's branch.</summary>
    public const string GiveRoleMessage = "You can only give or remove roles below your own in the hierarchy.";

    /// <summary>Refusal for touching the built-in top role.</summary>
    public const string SystemRoleMessage = "The Administrator role always has every feature and cannot be changed.";

    /// <summary>Refusal for removing the last administrator.</summary>
    public const string LastAdministratorMessage = "The organisation needs at least one active administrator. Make someone else an administrator first.";

    private readonly ICallerAccount _caller;
    private readonly IRepository<Role> _roles;
    private readonly IRepository<User> _users;
    private (RoleTree Tree, List<Role> Roles)? _loaded;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="caller">The calling staff user.</param>
    /// <param name="roles">Role data access (org-scoped).</param>
    /// <param name="users">User data access (org-scoped), to count administrators.</param>
    public AccessHierarchy(ICallerAccount caller, IRepository<Role> roles, IRepository<User> users)
    {
        _caller = caller;
        _roles = roles;
        _users = users;
    }

    /// <inheritdoc />
    public async Task<bool> IsAdministratorAsync(CancellationToken cancellationToken = default) =>
        (await HeldRoleIdsAsync(cancellationToken)).Contains((await TreeAsync(cancellationToken)).RootId ?? Guid.Empty);

    /// <inheritdoc />
    public async Task EnsureCanManageRoleAsync(Role role, CancellationToken cancellationToken = default)
    {
        if (role.IsSystem)
        {
            throw new ForbiddenException(SystemRoleMessage);
        }

        if (!await IsAdministratorAsync(cancellationToken)
            && !(await TreeAsync(cancellationToken)).IsBelow(role.Id, await HeldRoleIdsAsync(cancellationToken)))
        {
            throw new ForbiddenException(RoleNotBelowMessage);
        }
    }

    /// <inheritdoc />
    public async Task EnsureCanPlaceRoleAsync(Guid? parentId, Guid? roleId, CancellationToken cancellationToken = default)
    {
        RoleTree tree = await TreeAsync(cancellationToken);
        Guid? parent = parentId ?? tree.RootId;
        if (parentId is { } chosen && (await RolesAsync(cancellationToken)).All(r => r.Id != chosen))
        {
            throw new FieldValidationException(nameof(IRoleFields.ParentId), "Choose an existing role for this one to report to.");
        }

        if (parent is { } p && roleId is { } id && tree.WouldLoop(id, p))
        {
            throw new FieldValidationException(nameof(IRoleFields.ParentId), "A role cannot report to itself or to a role below it.");
        }

        if (await IsAdministratorAsync(cancellationToken))
        {
            return;
        }

        if (parent is not { } place || !tree.IsAtOrBelow(place, await HeldRoleIdsAsync(cancellationToken)))
        {
            throw new ForbiddenException("Choose one of your own roles, or a role below it, for this role to report to.");
        }
    }

    /// <inheritdoc />
    public async Task EnsureCanGiveRolesAsync(IEnumerable<Guid> roleIds, CancellationToken cancellationToken = default)
    {
        List<Guid> ids = roleIds.Distinct().ToList();
        if (ids.Count == 0 || await IsAdministratorAsync(cancellationToken))
        {
            return;
        }

        RoleTree tree = await TreeAsync(cancellationToken);
        IReadOnlySet<Guid> held = await HeldRoleIdsAsync(cancellationToken);
        if (ids.Any(id => !tree.IsBelow(id, held)))
        {
            throw new ForbiddenException(GiveRoleMessage);
        }
    }

    /// <inheritdoc />
    public async Task EnsureCanManageUserAsync(User user, CancellationToken cancellationToken = default)
    {
        if (await IsAdministratorAsync(cancellationToken))
        {
            return;
        }

        RoleTree tree = await TreeAsync(cancellationToken);
        IReadOnlySet<Guid> held = await HeldRoleIdsAsync(cancellationToken);
        if (user.RoleIds.Any(id => !tree.IsBelow(id, held)))
        {
            throw new ForbiddenException(UserNotBelowMessage);
        }
    }

    /// <inheritdoc />
    public async Task EnsureAdministratorRemainsAsync(User user, bool keepsAdministrator, CancellationToken cancellationToken = default)
    {
        if (keepsAdministrator || !user.IsActive || (await TreeAsync(cancellationToken)).RootId is not { } rootId || !user.RoleIds.Contains(rootId))
        {
            return;
        }

        Query query = _users.Query().WhereArrayContains(FirestoreNaming.Field(nameof(User.RoleIds)), rootId.ToString());
        IReadOnlyList<User> administrators = await _users.ListAsync(query, cancellationToken);
        if (!administrators.Any(a => a.IsActive && a.Id != user.Id))
        {
            throw new BusinessRuleException(LastAdministratorMessage);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IncludesAdministratorAsync(IEnumerable<Guid> roleIds, CancellationToken cancellationToken = default) =>
        (await TreeAsync(cancellationToken)).RootId is { } rootId && roleIds.Contains(rootId);

    /// <summary>
    /// The active roles the caller holds; none for an inactive or unknown caller.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Role ids.</returns>
    private async Task<IReadOnlySet<Guid>> HeldRoleIdsAsync(CancellationToken cancellationToken)
    {
        User? caller = await _caller.GetAsync(cancellationToken);
        if (caller is not { IsActive: true })
        {
            return new HashSet<Guid>();
        }

        var active = (await RolesAsync(cancellationToken)).Where(r => r.IsActive).Select(r => r.Id).ToHashSet();
        return caller.RoleIds.Where(active.Contains).ToHashSet();
    }

    /// <summary>
    /// The organisation's roles, read once per request.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Every role, active or not.</returns>
    private async Task<List<Role>> RolesAsync(CancellationToken cancellationToken) =>
        (await LoadAsync(cancellationToken)).Roles;

    /// <summary>
    /// The role tree, built once per request.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The tree.</returns>
    private async Task<RoleTree> TreeAsync(CancellationToken cancellationToken) =>
        (await LoadAsync(cancellationToken)).Tree;

    /// <summary>
    /// Reads every role of the organisation and builds the tree.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Tree and roles.</returns>
    private async Task<(RoleTree Tree, List<Role> Roles)> LoadAsync(CancellationToken cancellationToken)
    {
        if (_loaded is { } loaded)
        {
            return loaded;
        }

        List<Role> roles = (await _roles.ListAsync(_roles.Query(), cancellationToken)).ToList();
        var tree = new RoleTree(roles.Select(r => new RoleNode(r.Id, r.ParentId, r.IsSystem, r.Name)));
        _loaded = (tree, roles);
        return (tree, roles);
    }
}
