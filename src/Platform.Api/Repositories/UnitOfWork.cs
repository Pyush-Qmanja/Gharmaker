using Google.Cloud.Firestore;
using Platform.Api.Common;
using Platform.Api.Firestore;
using Platform.Shared.Entities.Common;

namespace Platform.Api.Repositories;

/// <summary>
/// Commits everything staged by repositories in the current request as one
/// atomic write.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Stamps audit and tenancy fields, then writes all staged changes in one
    /// Firestore batch: all succeed or none do.
    /// </summary>
    /// <param name="cancellationToken">Cancels the commit.</param>
    /// <returns>Number of documents written.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Receives writes staged by repositories. Separate from <see cref="IUnitOfWork"/>
/// so services can commit but only repositories can stage.
/// </summary>
public interface IChangeTracker
{
    /// <summary>
    /// Stages an entity for the next commit.
    /// </summary>
    /// <param name="entity">Entity to write.</param>
    /// <param name="isNew">True to insert (fails if the document exists), false to update.</param>
    void Track(BaseEntity entity, bool isNew);
}

/// <summary>
/// Firestore implementation of <see cref="IUnitOfWork"/> and <see cref="IChangeTracker"/>.
/// One instance per request, shared by all repositories in that request.
/// </summary>
/// <remarks>
/// This is the only place audit fields are set, which is why no service or
/// mapper ever assigns <c>CreatedAt</c>, <c>UpdatedAt</c> or <c>OrgId</c>.
/// </remarks>
public sealed class UnitOfWork : IUnitOfWork, IChangeTracker
{
    /// <summary>Firestore's limit on writes in one batch.</summary>
    private const int MaxBatchWrites = 500;

    /// <summary>Fields written on insert only and never overwritten by an update.</summary>
    private static readonly string[] InsertOnlyFields =
    {
        FirestoreNaming.Field(nameof(BaseEntity.CreatedAt)),
        FirestoreNaming.Field(nameof(BaseEntity.CreatedBy)),
        FirestoreNaming.Field(nameof(IOrgScoped.OrgId)),
    };

    private readonly IFirestoreContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly List<(BaseEntity Entity, bool IsNew)> _pending = new();

    /// <summary>
    /// Creates the unit of work.
    /// </summary>
    /// <param name="context">Database entry point.</param>
    /// <param name="currentUser">Caller identity for audit and tenancy fields.</param>
    /// <param name="timeProvider">Clock for audit timestamps.</param>
    public UnitOfWork(IFirestoreContext context, ICurrentUser currentUser, TimeProvider timeProvider)
    {
        _context = context;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public void Track(BaseEntity entity, bool isNew)
    {
        // Re-tracking the same entity keeps a single pending write.
        _pending.RemoveAll(p => ReferenceEquals(p.Entity, entity));
        _pending.Add((entity, isNew));
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_pending.Count == 0)
        {
            return 0;
        }

        if (_pending.Count > MaxBatchWrites)
        {
            throw new InvalidOperationException(
                $"A single save may write at most {MaxBatchWrites} documents; {_pending.Count} were staged.");
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        Guid? userId = _currentUser.UserId;
        WriteBatch batch = _context.Database.StartBatch();

        foreach (var (entity, isNew) in _pending)
        {
            DocumentReference document = _context.Database
                .Collection(FirestoreNaming.Collection(entity.GetType()))
                .Document(entity.Id.ToString());

            if (isNew)
            {
                StampInsert(entity, now, userId);
                batch.Create(document, DocumentConverter.ToDocument(entity));
            }
            else
            {
                entity.UpdatedAt = now;
                entity.UpdatedBy = userId;
                Dictionary<string, object?> fields = DocumentConverter.ToDocument(entity);
                foreach (string field in InsertOnlyFields)
                {
                    fields.Remove(field);
                }

                batch.Update(document, fields.ToDictionary(f => f.Key, f => f.Value!));
            }
        }

        await batch.CommitAsync(cancellationToken);
        int written = _pending.Count;
        _pending.Clear();
        return written;
    }

    /// <summary>
    /// Sets the creation audit fields and, for org-scoped entities, the organisation.
    /// </summary>
    /// <param name="entity">Entity being inserted.</param>
    /// <param name="now">Commit time (UTC).</param>
    /// <param name="userId">Caller, or null for system writes.</param>
    /// <exception cref="InvalidOperationException">An org-scoped entity has no organisation.</exception>
    private void StampInsert(BaseEntity entity, DateTime now, Guid? userId)
    {
        entity.CreatedAt = now;
        entity.CreatedBy ??= userId;

        if (entity is IOrgScoped scoped && scoped.OrgId == Guid.Empty)
        {
            scoped.OrgId = _currentUser.OrgId
                ?? throw new InvalidOperationException($"Cannot insert {entity.GetType().Name} without an organisation.");
        }
    }
}
