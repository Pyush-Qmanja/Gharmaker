using System.Linq.Expressions;
using Platform.Shared.Entities.Common;

namespace Platform.Api.Repositories;

/// <summary>
/// Generic data access for one entity type. Repositories stage changes only;
/// <see cref="IUnitOfWork"/> commits them, so several repositories can take
/// part in one atomic save (required for ledger writes, P2/P3).
/// </summary>
/// <typeparam name="TEntity">Entity handled by this repository.</typeparam>
public interface IRepository<TEntity> where TEntity : BaseEntity
{
    /// <summary>
    /// Starting point for a custom query. Org filtering is already applied.
    /// </summary>
    /// <param name="asTracking">True when the caller will modify the results.</param>
    /// <returns>A composable query.</returns>
    IQueryable<TEntity> Query(bool asTracking = false);

    /// <summary>
    /// Loads one entity by key, tracked for update.
    /// </summary>
    /// <param name="id">Primary key.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The entity, or null when absent or outside the caller's organisation.</returns>
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether any entity matches a condition.
    /// </summary>
    /// <param name="predicate">Condition to test.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>True when at least one entity matches.</returns>
    Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages a new entity for insert.
    /// </summary>
    /// <param name="entity">Entity to insert.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the entity is staged.</returns>
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages an existing entity for update.
    /// </summary>
    /// <param name="entity">Entity to update.</param>
    void Update(TEntity entity);
}
