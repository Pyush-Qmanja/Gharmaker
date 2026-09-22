using Google.Cloud.Firestore;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Entities.Common;

namespace Platform.Api.Repositories;

/// <summary>
/// Generic data access for one entity type — the one way the application reads
/// and writes Firestore. Registered as an open generic, so every entity gets a
/// repository with no extra code.
/// </summary>
/// <remarks>
/// Organisation scoping is automatic: for <see cref="IOrgScoped"/> entities every
/// query and lookup is limited to the caller's organisation, and a record from
/// another organisation behaves as if it does not exist.
/// Writes are only staged here; <see cref="IUnitOfWork.SaveChangesAsync"/>
/// commits them together in one atomic batch.
/// </remarks>
/// <typeparam name="TEntity">Entity handled by this repository.</typeparam>
public interface IRepository<TEntity> where TEntity : BaseEntity, new()
{
    /// <summary>
    /// Starting point for a custom query, already restricted to the caller's
    /// organisation. Build filters with <c>FirestoreNaming.Field(nameof(...))</c>
    /// and values with <c>DocumentConverter.ToFirestoreValue(...)</c>.
    /// </summary>
    /// <returns>A composable query.</returns>
    Query Query();

    /// <summary>
    /// Loads one entity by key.
    /// </summary>
    /// <param name="id">Primary key (document id).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The entity, or null when absent or outside the caller's organisation.</returns>
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads several entities by key. Ids that are absent or outside the
    /// caller's organisation are simply not returned.
    /// </summary>
    /// <param name="ids">Primary keys; duplicates are ignored.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The entities found, in no particular order.</returns>
    Task<IReadOnlyList<TEntity>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a query built from <see cref="Query"/> and returns every match.
    /// Always add a <c>Limit</c> for anything that can grow.
    /// </summary>
    /// <param name="query">Query to run.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The matching entities.</returns>
    Task<IReadOnlyList<TEntity>> ListAsync(Query query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts a query and returns one page of it.
    /// </summary>
    /// <param name="query">Filtered and ordered query built from <see cref="Query"/>.</param>
    /// <param name="request">Page number and size.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The requested page with totals.</returns>
    Task<PagedResult<TEntity>> GetPagedAsync(Query query, PagedRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether another record in scope already has a value — the
    /// replacement for a unique index, which Firestore does not have.
    /// </summary>
    /// <param name="propertyName">C# property name, via <c>nameof</c>.</param>
    /// <param name="value">Value to look for.</param>
    /// <param name="excludeId">Record to ignore (the one being updated).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when a different record has the value.</returns>
    Task<bool> ExistsAsync(string propertyName, object? value, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages a new entity for insert.
    /// </summary>
    /// <param name="entity">Entity to insert.</param>
    void Add(TEntity entity);

    /// <summary>
    /// Stages an existing entity for update.
    /// </summary>
    /// <param name="entity">Entity previously loaded through this repository.</param>
    void Update(TEntity entity);
}
