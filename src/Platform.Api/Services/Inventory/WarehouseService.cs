using Google.Cloud.Firestore;
using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Mapping;
using Platform.Api.Repositories;
using Platform.Api.Security.Authorization;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Inventory;
using Platform.Shared.Entities.Identity;
using Platform.Shared.Entities.Inventory;

namespace Platform.Api.Services.Inventory;

/// <summary>
/// CRUD for warehouses, limited to the caller's warehouse scope. A warehouse
/// is governed by its own id, so a user scoped to one warehouse sees only that one.
/// </summary>
public sealed class WarehouseService : ScopedCrudService<Warehouse, WarehouseDto, CreateWarehouseRequest, UpdateWarehouseRequest>
{
    /// <summary>Stored name of <see cref="Warehouse.Code"/>.</summary>
    private static readonly string CodeField = FirestoreNaming.Field(nameof(Warehouse.Code));

    private readonly IRepository<User> _users;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="repository">Warehouse data access.</param>
    /// <param name="unitOfWork">Commits staged changes.</param>
    /// <param name="mapper">Warehouse mapping.</param>
    /// <param name="permissions">The caller's scopes.</param>
    /// <param name="users">User data access, to validate the responsible user.</param>
    public WarehouseService(
        IRepository<Warehouse> repository,
        IUnitOfWork unitOfWork,
        IEntityMapper<Warehouse, WarehouseDto, CreateWarehouseRequest, UpdateWarehouseRequest> mapper,
        IPermissionService permissions,
        IRepository<User> users)
        : base(repository, unitOfWork, mapper, permissions)
    {
        _users = users;
    }

    /// <inheritdoc />
    protected override FeatureInfo Feature { get; } = Features.Find(Features.Warehouses)!;

    /// <inheritdoc />
    protected override Guid ScopeIdOf(Warehouse entity) => entity.Id;

    /// <inheritdoc />
    protected override bool MatchesSearch(Warehouse entity, string search) =>
        entity.Code.Contains(search, StringComparison.OrdinalIgnoreCase)
        || entity.Name.Contains(search, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    protected override IOrderedEnumerable<Warehouse> OrderInMemory(IEnumerable<Warehouse> entities) =>
        entities.OrderBy(w => w.Code, StringComparer.Ordinal);

    /// <summary>
    /// Sorts by code.
    /// </summary>
    /// <param name="query">Org-scoped query.</param>
    /// <returns>The ordered query.</returns>
    protected override Query ApplyOrder(Query query) => query.OrderBy(CodeField);

    /// <summary>
    /// Prefix match on the (upper-case) code.
    /// </summary>
    /// <param name="query">Org-scoped query.</param>
    /// <param name="search">Trimmed search text.</param>
    /// <returns>The filtered query, ordered by code.</returns>
    protected override Query ApplySearch(Query query, string search)
    {
        string prefix = search.ToUpperInvariant();
        return query.WhereStartsWith(CodeField, prefix);
    }

    /// <summary>
    /// A code is unique within an organisation.
    /// </summary>
    /// <param name="entity">Warehouse about to be written.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task that completes when the code is free.</returns>
    protected override Task EnsureUniqueAsync(Warehouse entity, CancellationToken cancellationToken) =>
        RequireUniqueAsync(entity, nameof(Warehouse.Code), entity.Code, cancellationToken);

    /// <summary>
    /// Adds the responsible-user check to the scope check.
    /// </summary>
    /// <param name="entity">Warehouse about to be written.</param>
    /// <param name="isNew">True for a create.</param>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <returns>A task that completes when the write may go ahead.</returns>
    /// <exception cref="FieldValidationException">The responsible user does not exist or is inactive.</exception>
    protected override async Task BeforeWriteAsync(Warehouse entity, bool isNew, CancellationToken cancellationToken)
    {
        await base.BeforeWriteAsync(entity, isNew, cancellationToken);

        if (entity.OwnerUserId is { } ownerId
            && await _users.GetByIdAsync(ownerId, cancellationToken) is not { IsActive: true })
        {
            throw new FieldValidationException(nameof(Warehouse.OwnerUserId), "Choose an active user in your organisation.");
        }
    }
}
