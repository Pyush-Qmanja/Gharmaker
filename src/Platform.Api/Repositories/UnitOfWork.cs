using Google.Cloud.Firestore;
using Grpc.Core;
using Platform.Api.Common.Exceptions;
using Platform.Api.Common;
using Platform.Api.Firestore;
using Platform.Shared.Entities.Common;

namespace Platform.Api.Repositories;

/// <summary>
/// Commits changes: either everything staged by repositories in the current
/// request (one batch), or a unit of work that must read and write
/// consistently (one transaction). Both stamp audit fields and write the
/// audit log (P10) in the same commit.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Stamps audit and tenancy fields, then writes all staged changes and
    /// their audit entries in one Firestore batch: all succeed or none do.
    /// A save above Firestore's 500-write limit is split into several batches.
    /// </summary>
    /// <param name="cancellationToken">Cancels the commit.</param>
    /// <returns>Number of documents written, audit entries included.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs work inside a Firestore transaction: its reads see a consistent
    /// snapshot and its writes (plus audit entries) commit together only if
    /// nothing it read changed meanwhile; otherwise Firestore runs it again.
    /// The work must therefore do all reads first, have no side effects
    /// outside the session, and be safe to repeat.
    /// </summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="work">Reads through the session, then stages writes on it.</param>
    /// <param name="cancellationToken">Cancels the transaction.</param>
    /// <returns>The work's result from the attempt that committed.</returns>
    Task<T> RunInTransactionAsync<T>(Func<ITransactionSession, Task<T>> work, CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads and staged writes of one transaction (see <see cref="IUnitOfWork.RunInTransactionAsync{T}"/>).
/// Reads are limited to the caller's organisation.
/// </summary>
public interface ITransactionSession
{
    /// <summary>
    /// Reads one document by id.
    /// </summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <param name="id">Primary key.</param>
    /// <returns>The entity, or null when absent or in another organisation.</returns>
    Task<TEntity?> GetAsync<TEntity>(Guid id) where TEntity : BaseEntity, new();

    /// <summary>
    /// Reads several documents by id in one round trip; missing ones are skipped.
    /// </summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <param name="ids">Primary keys.</param>
    /// <returns>The entities found, keyed by id.</returns>
    Task<IReadOnlyDictionary<Guid, TEntity>> GetManyAsync<TEntity>(IEnumerable<Guid> ids) where TEntity : BaseEntity, new();

    /// <summary>
    /// Stages an insert (fails the commit if the document already exists).
    /// </summary>
    /// <param name="entity">Entity to create.</param>
    void Add(BaseEntity entity);

    /// <summary>
    /// Stages an update of an entity read in this session.
    /// </summary>
    /// <param name="entity">Entity to update.</param>
    void Update(BaseEntity entity);
}

/// <summary>
/// Receives writes staged by repositories, and the state of entities as they
/// were loaded (for the audit log's "before"). Separate from
/// <see cref="IUnitOfWork"/> so services can commit but only repositories can stage.
/// </summary>
public interface IChangeTracker
{
    /// <summary>
    /// Stages an entity for the next commit.
    /// </summary>
    /// <param name="entity">Entity to write.</param>
    /// <param name="isNew">True to insert (fails if the document exists), false to update.</param>
    void Track(BaseEntity entity, bool isNew);

    /// <summary>
    /// Remembers an entity's stored fields as loaded, so an update can record
    /// what changed.
    /// </summary>
    /// <param name="entity">Entity just read from Firestore.</param>
    void Loaded(BaseEntity entity);
}

/// <summary>
/// Firestore implementation of <see cref="IUnitOfWork"/> and <see cref="IChangeTracker"/>.
/// One instance per request, shared by all repositories in that request.
/// </summary>
/// <remarks>
/// This is the only place audit fields are set and audit entries are written,
/// which is why no service or mapper ever assigns <c>CreatedAt</c>,
/// <c>UpdatedAt</c> or <c>OrgId</c>, or writes to the audit log itself.
/// </remarks>
public sealed class UnitOfWork : IUnitOfWork, IChangeTracker
{
    /// <summary>Firestore's limit on writes in one batch or transaction.</summary>
    private const int MaxWrites = 500;

    /// <summary>How many times Firestore tries a transaction that lost a race, per round.</summary>
    private const int MaxTransactionAttempts = 20;

    /// <summary>Rounds of Firestore attempts, with a random pause between, before answering "busy".</summary>
    private const int BusyRounds = 3;

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
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly List<(BaseEntity Entity, bool IsNew)> _pending = new();
    private readonly Dictionary<(Type, Guid), Dictionary<string, object?>> _loaded = new();

    /// <summary>
    /// Creates the unit of work.
    /// </summary>
    /// <param name="context">Firestore access.</param>
    /// <param name="currentUser">Caller, for <c>CreatedBy</c>/<c>UpdatedBy</c> and <c>OrgId</c>.</param>
    /// <param name="timeProvider">Clock, for <c>CreatedAt</c>/<c>UpdatedAt</c>.</param>
    /// <param name="httpContextAccessor">Request, for the audit log's IP and device.</param>
    public UnitOfWork(IFirestoreContext context, ICurrentUser currentUser, TimeProvider timeProvider, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public void Track(BaseEntity entity, bool isNew)
    {
        // Re-tracking the same entity keeps a single pending write.
        _pending.RemoveAll(p => ReferenceEquals(p.Entity, entity));
        _pending.Add((entity, isNew));
    }

    /// <inheritdoc />
    public void Loaded(BaseEntity entity)
    {
        if (entity is not INotAudited)
        {
            _loaded[(entity.GetType(), entity.Id)] = DocumentConverter.ToDocument(entity);
        }
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_pending.Count == 0)
        {
            return 0;
        }

        List<PreparedWrite> writes = Prepare(_pending, _loaded);
        foreach (PreparedWrite[] chunk in writes.Chunk(MaxWrites))
        {
            WriteBatch batch = _context.Database.StartBatch();
            foreach (PreparedWrite write in chunk)
            {
                if (write.IsCreate)
                {
                    batch.Create(write.Document, write.Fields);
                }
                else
                {
                    batch.Update(write.Document, ForUpdate(write.Fields));
                }
            }

            await batch.CommitAsync(cancellationToken);
        }

        foreach (var (entity, _) in _pending)
        {
            Loaded(entity);
        }

        _pending.Clear();
        return writes.Count;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Firestore retries a transaction that lost a race up to
    /// <see cref="MaxTransactionAttempts"/> times; if the lock still times out,
    /// the whole transaction is run again after a short random pause, up to
    /// <see cref="BusyRounds"/> rounds. Only then does the caller get a
    /// <see cref="BusyException"/> (409, nothing saved) instead of an error.
    /// </remarks>
    public async Task<T> RunInTransactionAsync<T>(Func<ITransactionSession, Task<T>> work, CancellationToken cancellationToken = default)
    {
        for (int round = 1; ; round++)
        {
            try
            {
                return await RunOnceAsync(work, cancellationToken);
            }
            catch (RpcException ex) when (ex.StatusCode is StatusCode.Aborted or StatusCode.DeadlineExceeded)
            {
                if (round >= BusyRounds)
                {
                    throw new BusyException();
                }

                // Let the crowd thin out, at a random moment so retries do not collide again.
                await Task.Delay(Random.Shared.Next(100, 400) * round, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Runs the transaction with Firestore's retries.
    /// </summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="work">The work.</param>
    /// <param name="cancellationToken">Cancels the transaction.</param>
    /// <returns>The work's result.</returns>
    private Task<T> RunOnceAsync<T>(Func<ITransactionSession, Task<T>> work, CancellationToken cancellationToken) =>
        _context.Database.RunTransactionAsync(
            async transaction =>
            {
                var session = new TransactionSession(transaction, _context, _currentUser);
                T result = await work(session);

                List<PreparedWrite> writes = Prepare(session.Pending, session.Loaded);
                if (writes.Count > MaxWrites)
                {
                    throw new InvalidOperationException(
                        $"A transaction may write at most {MaxWrites} documents; {writes.Count} were staged.");
                }

                foreach (PreparedWrite write in writes)
                {
                    if (write.IsCreate)
                    {
                        transaction.Create(write.Document, write.Fields);
                    }
                    else
                    {
                        transaction.Update(write.Document, ForUpdate(write.Fields));
                    }
                }

                return result;
            },
            TransactionOptions.ForMaxAttempts(MaxTransactionAttempts),
            cancellationToken);

    /// <summary>
    /// Stamps each staged entity, converts it to stored fields, and adds its
    /// audit entry (if any) as a further create.
    /// </summary>
    /// <param name="pending">Staged entities.</param>
    /// <param name="loaded">Stored fields of entities as loaded, for the audit "before".</param>
    /// <returns>Every document write to commit.</returns>
    private List<PreparedWrite> Prepare(
        IEnumerable<(BaseEntity Entity, bool IsNew)> pending,
        IReadOnlyDictionary<(Type, Guid), Dictionary<string, object?>> loaded)
    {
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        Guid? userId = _currentUser.UserId;
        AuditActor actor = Actor();
        var writes = new List<PreparedWrite>();

        foreach (var (entity, isNew) in pending)
        {
            if (isNew)
            {
                StampInsert(entity, now, userId);
            }
            else
            {
                entity.UpdatedAt = now;
                entity.UpdatedBy = userId;
            }

            Dictionary<string, object?> fields = DocumentConverter.ToDocument(entity);
            writes.Add(new PreparedWrite(DocumentFor(entity), isNew, fields));

            loaded.TryGetValue((entity.GetType(), entity.Id), out Dictionary<string, object?>? before);
            if (AuditBuilder.Build(entity, isNew, before, fields, actor) is { } audit)
            {
                StampInsert(audit, now, userId);
                audit.CreatedBy = actor.UserId;
                writes.Add(new PreparedWrite(DocumentFor(audit), IsCreate: true, DocumentConverter.ToDocument(audit)));
            }
        }

        return writes;
    }

    /// <summary>
    /// Who is acting and from where, for the audit log.
    /// </summary>
    /// <returns>The actor.</returns>
    private AuditActor Actor()
    {
        HttpContext? http = _httpContextAccessor.HttpContext;
        return new AuditActor(
            _currentUser.UserId,
            _currentUser.OrgId,
            http?.Connection.RemoteIpAddress?.ToString(),
            http is null ? "system" : http.Request.Headers.UserAgent.ToString());
    }

    /// <summary>
    /// The document an entity is stored in.
    /// </summary>
    /// <param name="entity">Entity.</param>
    /// <returns>Its document reference.</returns>
    private DocumentReference DocumentFor(BaseEntity entity) =>
        _context.Database.Collection(FirestoreNaming.Collection(entity.GetType())).Document(entity.Id.ToString());

    /// <summary>
    /// The fields an update writes: all but those an update must never overwrite.
    /// </summary>
    /// <param name="fields">Stored fields.</param>
    /// <returns>The fields to update.</returns>
    private static Dictionary<string, object> ForUpdate(Dictionary<string, object?> fields)
    {
        var copy = new Dictionary<string, object>(fields.Count);
        foreach (var (key, value) in fields)
        {
            if (!InsertOnlyFields.Contains(key))
            {
                copy[key] = value!;
            }
        }

        return copy;
    }

    /// <summary>
    /// Sets creation time, creator and (for org-scoped data) the organisation on an insert.
    /// </summary>
    /// <param name="entity">Entity being inserted.</param>
    /// <param name="now">Commit time (UTC).</param>
    /// <param name="userId">Acting user, or null for system work.</param>
    /// <exception cref="InvalidOperationException">An org-scoped entity is inserted with no organisation known.</exception>
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

    /// <summary>
    /// One document write, ready for a batch or transaction.
    /// </summary>
    /// <param name="Document">Target document.</param>
    /// <param name="IsCreate">True to create, false to update.</param>
    /// <param name="Fields">Fields to write.</param>
    private sealed record PreparedWrite(DocumentReference Document, bool IsCreate, Dictionary<string, object?> Fields);

    /// <summary>
    /// <see cref="ITransactionSession"/> over a Firestore transaction.
    /// </summary>
    private sealed class TransactionSession : ITransactionSession
    {
        private readonly Transaction _transaction;
        private readonly IFirestoreContext _context;
        private readonly ICurrentUser _currentUser;

        /// <summary>
        /// Creates the session.
        /// </summary>
        /// <param name="transaction">Firestore transaction.</param>
        /// <param name="context">Firestore access.</param>
        /// <param name="currentUser">Caller, to limit reads to their organisation.</param>
        public TransactionSession(Transaction transaction, IFirestoreContext context, ICurrentUser currentUser)
        {
            _transaction = transaction;
            _context = context;
            _currentUser = currentUser;
        }

        /// <summary>Writes staged in this attempt.</summary>
        public List<(BaseEntity Entity, bool IsNew)> Pending { get; } = new();

        /// <summary>Stored fields of entities read in this attempt.</summary>
        public Dictionary<(Type, Guid), Dictionary<string, object?>> Loaded { get; } = new();

        /// <inheritdoc />
        public async Task<TEntity?> GetAsync<TEntity>(Guid id) where TEntity : BaseEntity, new()
        {
            DocumentSnapshot snapshot = await _transaction.GetSnapshotAsync(_context.Collection<TEntity>().Document(id.ToString()));
            return snapshot.Exists ? Accept(DocumentConverter.FromDocument<TEntity>(snapshot)) : null;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyDictionary<Guid, TEntity>> GetManyAsync<TEntity>(IEnumerable<Guid> ids) where TEntity : BaseEntity, new()
        {
            DocumentReference[] references = ids.Distinct()
                .Select(id => _context.Collection<TEntity>().Document(id.ToString()))
                .ToArray();
            var found = new Dictionary<Guid, TEntity>();
            if (references.Length == 0)
            {
                return found;
            }

            foreach (DocumentSnapshot snapshot in await _transaction.GetAllSnapshotsAsync(references))
            {
                if (snapshot.Exists && Accept(DocumentConverter.FromDocument<TEntity>(snapshot)) is { } entity)
                {
                    found[entity.Id] = entity;
                }
            }

            return found;
        }

        /// <inheritdoc />
        public void Add(BaseEntity entity) => Pending.Add((entity, true));

        /// <inheritdoc />
        public void Update(BaseEntity entity)
        {
            Pending.RemoveAll(p => ReferenceEquals(p.Entity, entity));
            Pending.Add((entity, false));
        }

        /// <summary>
        /// Keeps an entity only if it belongs to the caller's organisation, and remembers its loaded state.
        /// </summary>
        /// <typeparam name="TEntity">Entity type.</typeparam>
        /// <param name="entity">Entity read.</param>
        /// <returns>The entity, or null when it is another organisation's.</returns>
        private TEntity? Accept<TEntity>(TEntity entity) where TEntity : BaseEntity
        {
            if (entity is IOrgScoped scoped && scoped.OrgId != _currentUser.OrgId)
            {
                return null;
            }

            if (entity is not INotAudited)
            {
                Loaded[(entity.GetType(), entity.Id)] = DocumentConverter.ToDocument(entity);
            }

            return entity;
        }
    }
}
