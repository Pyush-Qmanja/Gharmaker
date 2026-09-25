using Google.Cloud.Firestore;
using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Mapping;
using Platform.Api.Repositories;
using Platform.Api.Security.Authorization;
using Platform.Shared.Dtos.Identity;
using Platform.Shared.Entities.Identity;

namespace Platform.Api.Services.Identity;

/// <summary>
/// CRUD for roles, within the role hierarchy: the built-in Administrator role
/// is never changed; anyone else may create, change or deactivate only roles
/// below their own, and may add or remove only features they hold themselves.
/// Deactivating a role immediately removes its capabilities from every user
/// holding it, because capabilities are read per request.
/// </summary>
public sealed class RoleService : CrudService<Role, RoleDto, CreateRoleRequest, UpdateRoleRequest>
{
    private readonly IAccessHierarchy _hierarchy;
    private readonly IPermissionService _permissions;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="repository">Role data access.</param>
    /// <param name="unitOfWork">Commits staged changes.</param>
    /// <param name="mapper">Role mapping.</param>
    /// <param name="hierarchy">Role hierarchy rules.</param>
    /// <param name="permissions">Caller's access, to stop handing out features they do not hold.</param>
    public RoleService(
        IRepository<Role> repository,
        IUnitOfWork unitOfWork,
        IEntityMapper<Role, RoleDto, CreateRoleRequest, UpdateRoleRequest> mapper,
        IAccessHierarchy hierarchy,
        IPermissionService permissions)
        : base(repository, unitOfWork, mapper)
    {
        _hierarchy = hierarchy;
        _permissions = permissions;
    }

    /// <summary>
    /// Creates a role below one of the caller's own roles, holding only features the caller holds.
    /// </summary>
    /// <param name="request">Validated create request.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The created role.</returns>
    public override async Task<RoleDto> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        await _hierarchy.EnsureCanPlaceRoleAsync(request.ParentId, roleId: null, cancellationToken);
        await EnsureFeaturesHeldAsync(request.Capabilities, cancellationToken);
        return await base.CreateAsync(request, cancellationToken);
    }

    /// <summary>
    /// Changes a role below the caller; only features the caller holds may be added or removed.
    /// </summary>
    /// <param name="id">Role id.</param>
    /// <param name="request">Validated update request.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The updated role.</returns>
    public override async Task<RoleDto> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        Role role = await LoadAsync(id, cancellationToken);
        await _hierarchy.EnsureCanManageRoleAsync(role, cancellationToken);
        await _hierarchy.EnsureCanPlaceRoleAsync(request.ParentId, id, cancellationToken);

        var wanted = request.Capabilities.ToHashSet(StringComparer.Ordinal);
        var current = role.Capabilities.ToHashSet(StringComparer.Ordinal);
        await EnsureFeaturesHeldAsync(wanted.Except(current).Concat(current.Except(wanted)), cancellationToken);
        return await base.UpdateAsync(id, request, cancellationToken);
    }

    /// <summary>
    /// Sorts roles by name.
    /// </summary>
    /// <param name="query">Org-scoped query.</param>
    /// <returns>The ordered query.</returns>
    protected override Query ApplyOrder(Query query) =>
        query.OrderBy(FirestoreNaming.Field(nameof(Role.Name)));

    /// <summary>
    /// Only a role below the caller can be deactivated, and never the Administrator role.
    /// </summary>
    /// <param name="entity">Role about to be deactivated.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task that completes when allowed.</returns>
    protected override Task BeforeDeleteAsync(Role entity, CancellationToken cancellationToken) =>
        _hierarchy.EnsureCanManageRoleAsync(entity, cancellationToken);

    /// <summary>
    /// A role name is unique within an organisation.
    /// </summary>
    /// <param name="entity">Role about to be written.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task that completes when the name is free.</returns>
    protected override Task EnsureUniqueAsync(Role entity, CancellationToken cancellationToken) =>
        RequireUniqueAsync(entity, nameof(Role.Name), entity.Name, cancellationToken);

    /// <summary>
    /// Refuses a change to features the caller does not hold themselves.
    /// </summary>
    /// <param name="capabilities">Capabilities being added or removed.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when every one is held.</returns>
    /// <exception cref="ForbiddenException">A capability is not held by the caller.</exception>
    private async Task EnsureFeaturesHeldAsync(IEnumerable<string> capabilities, CancellationToken cancellationToken)
    {
        IReadOnlySet<string> held = await _permissions.GetCapabilitiesAsync(cancellationToken);
        if (capabilities.Any(c => !held.Contains(c)))
        {
            throw new ForbiddenException("You can only give or remove features you hold yourself.");
        }
    }
}
