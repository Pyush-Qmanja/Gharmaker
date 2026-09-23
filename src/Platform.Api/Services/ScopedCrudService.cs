using Platform.Api.Common.Exceptions;
using Platform.Api.Mapping;
using Platform.Api.Repositories;
using Platform.Api.Security.Authorization;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Entities.Common;
using Platform.Shared.Entities.Identity;

namespace Platform.Api.Services;

/// <summary>
/// <see cref="CrudService{TEntity,TDto,TCreate,TUpdate}"/> for data that is
/// limited by scope (P6) — warehouses now; bins, sites and their contents later.
/// Every read, list, update and delete is restricted to the caller's scope,
/// and anything outside it behaves as if it does not exist (404).
/// </summary>
/// <remarks>
/// A derived service names its <see cref="Feature"/> (which gives the
/// capabilities and the <see cref="ScopeType"/> that governs the entity) and
/// how to find the governing id (<see cref="ScopeIdOf"/>: the entity's own
/// id for a warehouse, its <c>WarehouseId</c> for a bin). Callers with global
/// scope see everything and use the normal Firestore paging; everyone else is
/// served from their (small) set of scoped objects, searched and sorted in memory.
/// Reads use the scope where the caller holds the feature's view capability;
/// creates, updates and deactivations need its manage capability for that object.
/// </remarks>
/// <typeparam name="TEntity">Persisted entity.</typeparam>
/// <typeparam name="TDto">Read model.</typeparam>
/// <typeparam name="TCreate">Create request body.</typeparam>
/// <typeparam name="TUpdate">Update request body.</typeparam>
public abstract class ScopedCrudService<TEntity, TDto, TCreate, TUpdate> : CrudService<TEntity, TDto, TCreate, TUpdate>
    where TEntity : BaseEntity, new()
{
    /// <summary>The caller's capabilities and scopes.</summary>
    protected readonly IPermissionService Permissions;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="repository">Data access for the entity.</param>
    /// <param name="unitOfWork">Commits staged changes.</param>
    /// <param name="mapper">Entity/DTO conversion.</param>
    /// <param name="permissions">The caller's scopes.</param>
    protected ScopedCrudService(
        IRepository<TEntity> repository,
        IUnitOfWork unitOfWork,
        IEntityMapper<TEntity, TDto, TCreate, TUpdate> mapper,
        IPermissionService permissions)
        : base(repository, unitOfWork, mapper)
    {
        Permissions = permissions;
    }

    /// <summary>Feature that governs access to this entity; must be limited by scope.</summary>
    protected abstract FeatureInfo Feature { get; }

    /// <summary>Scope type that governs access to this entity.</summary>
    protected ScopeType ScopeType => Feature.ScopeType
        ?? throw new InvalidOperationException($"Feature '{Feature.Code}' is not limited by scope.");

    /// <summary>
    /// Returns the id of the object that governs access to an entity.
    /// </summary>
    /// <param name="entity">Entity.</param>
    /// <returns>The governing id (e.g. the warehouse id).</returns>
    protected abstract Guid ScopeIdOf(TEntity entity);

    /// <summary>
    /// Free-text match used when listing for a scoped (non-global) caller.
    /// </summary>
    /// <param name="entity">Candidate.</param>
    /// <param name="search">Trimmed search text.</param>
    /// <returns>True when the entity matches.</returns>
    protected abstract bool MatchesSearch(TEntity entity, string search);

    /// <summary>
    /// Sort order used when listing for a scoped (non-global) caller. Should match <c>ApplyOrder</c>.
    /// </summary>
    /// <param name="entities">Entities to sort.</param>
    /// <returns>The sorted entities.</returns>
    protected abstract IOrderedEnumerable<TEntity> OrderInMemory(IEnumerable<TEntity> entities);

    /// <summary>
    /// Loads every entity governed by the given scope ids. Default: the ids are
    /// the entities' own ids. Override for child entities (query by parent id).
    /// </summary>
    /// <param name="scopeIds">Ids the caller is scoped to.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The entities in scope.</returns>
    protected virtual Task<IReadOnlyList<TEntity>> LoadInScopeAsync(IReadOnlySet<Guid> scopeIds, CancellationToken cancellationToken) =>
        Repository.GetByIdsAsync(scopeIds, cancellationToken);

    /// <summary>
    /// Lists one page: Firestore paging for global callers, the caller's
    /// scoped set for everyone else.
    /// </summary>
    /// <param name="request">Paging and search parameters.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The requested page.</returns>
    public override async Task<PagedResult<TDto>> GetPagedAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        IReadOnlySet<Guid>? scopeIds = await Permissions.GetScopeIdsAsync(Feature.ViewCapability, ScopeType, cancellationToken);
        if (scopeIds is null)
        {
            return await base.GetPagedAsync(request, cancellationToken);
        }

        IEnumerable<TEntity> inScope = scopeIds.Count == 0
            ? Array.Empty<TEntity>()
            : await LoadInScopeAsync(scopeIds, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string search = request.Search.Trim();
            inScope = inScope.Where(e => MatchesSearch(e, search));
        }

        List<TEntity> all = OrderInMemory(inScope).ToList();
        return new PagedResult<TDto>
        {
            Items = all.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).Select(Mapper.ToDto).ToList(),
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = all.Count,
        };
    }

    /// <summary>
    /// Loads an entity, treating one outside the caller's scope as not found (P6).
    /// Covers get, update and delete.
    /// </summary>
    /// <param name="id">Primary key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The entity.</returns>
    /// <exception cref="NotFoundException">Absent or out of scope.</exception>
    protected override async Task<TEntity> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        TEntity entity = await base.LoadAsync(id, cancellationToken);
        return await Permissions.CoversAsync(Feature.ViewCapability, ScopeType, ScopeIdOf(entity), cancellationToken)
            ? entity
            : throw new NotFoundException(EntityName);
    }

    /// <summary>
    /// Refuses a create or update unless the caller may manage the entity
    /// where it ends up — for a new warehouse that means only callers who manage
    /// warehouses everywhere may create one.
    /// </summary>
    /// <param name="entity">Entity about to be written.</param>
    /// <param name="isNew">True for a create.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task that completes when the write may go ahead.</returns>
    /// <exception cref="ForbiddenException">The result would be outside the caller's scope.</exception>
    protected override async Task BeforeWriteAsync(TEntity entity, bool isNew, CancellationToken cancellationToken)
    {
        await base.BeforeWriteAsync(entity, isNew, cancellationToken);
        if (!await Permissions.CoversAsync(Feature.ManageCapability, ScopeType, ScopeIdOf(entity), cancellationToken))
        {
            throw new ForbiddenException($"You cannot change this {EntityName.ToLowerInvariant()} — it is outside the scope where you manage {Feature.Name.ToLowerInvariant()}.");
        }
    }

    /// <summary>
    /// Refuses a deactivation unless the caller manages the entity where it is.
    /// (The load already hid it with 404 if they cannot even see it.)
    /// </summary>
    /// <param name="entity">Entity about to be deactivated.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task that completes when the deactivation may go ahead.</returns>
    /// <exception cref="ForbiddenException">The caller only views it.</exception>
    protected override async Task BeforeDeleteAsync(TEntity entity, CancellationToken cancellationToken)
    {
        await base.BeforeDeleteAsync(entity, cancellationToken);
        if (!await Permissions.CoversAsync(Feature.ManageCapability, ScopeType, ScopeIdOf(entity), cancellationToken))
        {
            throw new ForbiddenException($"You cannot deactivate this {EntityName.ToLowerInvariant()} — it is outside the scope where you manage {Feature.Name.ToLowerInvariant()}.");
        }
    }
}
