using Google.Cloud.Firestore;
using Platform.Api.Common;
using Platform.Api.Firestore;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Entities.Common;

namespace Platform.Api.Repositories;

/// <summary>
/// Firestore implementation of <see cref="IRepository{TEntity}"/>. One class
/// serves every entity; add an entity-specific repository only for access
/// patterns that genuinely do not fit here.
/// </summary>
/// <typeparam name="TEntity">Entity handled by this repository.</typeparam>
public class Repository<TEntity> : IRepository<TEntity> where TEntity : BaseEntity, new()
{
    /// <summary>True when <typeparamref name="TEntity"/> belongs to an organisation.</summary>
    private static readonly bool IsOrgScoped = typeof(IOrgScoped).IsAssignableFrom(typeof(TEntity));

    /// <summary>Stored name of <see cref="IOrgScoped.OrgId"/>.</summary>
    private static readonly string OrgIdField = FirestoreNaming.Field(nameof(IOrgScoped.OrgId));

    private readonly CollectionReference _collection;
    private readonly ICurrentUser _currentUser;
    private readonly IChangeTracker _changeTracker;

    /// <summary>
    /// Creates the repository.
    /// </summary>
    /// <param name="context">Database entry point.</param>
    /// <param name="currentUser">Caller identity, used for organisation scoping.</param>
    /// <param name="changeTracker">Receives staged writes for the unit of work.</param>
    public Repository(IFirestoreContext context, ICurrentUser currentUser, IChangeTracker changeTracker)
    {
        _collection = context.Collection<TEntity>();
        _currentUser = currentUser;
        _changeTracker = changeTracker;
    }

    /// <inheritdoc />
    public Query Query()
    {
        if (!IsOrgScoped)
        {
            return _collection;
        }

        // No organisation (anonymous caller) matches nothing: fail closed.
        Guid orgId = _currentUser.OrgId ?? Guid.Empty;
        return _collection.WhereEqualTo(OrgIdField, DocumentConverter.ToFirestoreValue(orgId));
    }

    /// <inheritdoc />
    public async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        DocumentSnapshot snapshot = await _collection.Document(id.ToString()).GetSnapshotAsync(cancellationToken);
        if (!snapshot.Exists)
        {
            return null;
        }

        TEntity entity = DocumentConverter.FromDocument<TEntity>(snapshot);
        return IsInScope(entity) ? entity : null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TEntity>> ListAsync(Query query, CancellationToken cancellationToken = default)
    {
        QuerySnapshot snapshot = await query.GetSnapshotAsync(cancellationToken);
        return snapshot.Documents.Select(DocumentConverter.FromDocument<TEntity>).ToList();
    }

    /// <inheritdoc />
    public async Task<PagedResult<TEntity>> GetPagedAsync(Query query, PagedRequest request, CancellationToken cancellationToken = default)
    {
        AggregateQuerySnapshot count = await query.Count().GetSnapshotAsync(cancellationToken);
        IReadOnlyList<TEntity> items = await ListAsync(
            query.Offset((request.Page - 1) * request.PageSize).Limit(request.PageSize),
            cancellationToken);

        return new PagedResult<TEntity>
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = (int)(count.Count ?? 0),
        };
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string propertyName, object? value, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        // Two rows are enough: one may be the record being updated.
        QuerySnapshot snapshot = await Query()
            .WhereEqualTo(FirestoreNaming.Field(propertyName), DocumentConverter.ToFirestoreValue(value))
            .Limit(2)
            .GetSnapshotAsync(cancellationToken);

        string? excluded = excludeId?.ToString();
        return snapshot.Documents.Any(d => d.Id != excluded);
    }

    /// <inheritdoc />
    public void Add(TEntity entity) => _changeTracker.Track(entity, isNew: true);

    /// <inheritdoc />
    public void Update(TEntity entity) => _changeTracker.Track(entity, isNew: false);

    /// <summary>
    /// Checks that an entity belongs to the caller's organisation.
    /// </summary>
    /// <param name="entity">Loaded entity.</param>
    /// <returns>True when visible to the caller.</returns>
    private bool IsInScope(TEntity entity) =>
        entity is not IOrgScoped scoped || scoped.OrgId == _currentUser.OrgId;
}
