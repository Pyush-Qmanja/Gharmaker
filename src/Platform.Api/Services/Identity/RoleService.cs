using Google.Cloud.Firestore;
using Platform.Api.Firestore;
using Platform.Api.Mapping;
using Platform.Api.Repositories;
using Platform.Shared.Dtos.Identity;
using Platform.Shared.Entities.Identity;

namespace Platform.Api.Services.Identity;

/// <summary>
/// CRUD for roles. Deactivating a role immediately removes its capabilities
/// from every user holding it, because capabilities are read per request.
/// </summary>
public sealed class RoleService : CrudService<Role, RoleDto, CreateRoleRequest, UpdateRoleRequest>
{
    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="repository">Role data access.</param>
    /// <param name="unitOfWork">Commits staged changes.</param>
    /// <param name="mapper">Role mapping.</param>
    public RoleService(
        IRepository<Role> repository,
        IUnitOfWork unitOfWork,
        IEntityMapper<Role, RoleDto, CreateRoleRequest, UpdateRoleRequest> mapper)
        : base(repository, unitOfWork, mapper)
    {
    }

    /// <summary>
    /// Sorts roles by name.
    /// </summary>
    /// <param name="query">Org-scoped query.</param>
    /// <returns>The ordered query.</returns>
    protected override Query ApplyOrder(Query query) =>
        query.OrderBy(FirestoreNaming.Field(nameof(Role.Name)));

    /// <summary>
    /// A role name is unique within an organisation.
    /// </summary>
    /// <param name="entity">Role about to be written.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task that completes when the name is free.</returns>
    protected override Task EnsureUniqueAsync(Role entity, CancellationToken cancellationToken) =>
        RequireUniqueAsync(entity, nameof(Role.Name), entity.Name, cancellationToken);
}
