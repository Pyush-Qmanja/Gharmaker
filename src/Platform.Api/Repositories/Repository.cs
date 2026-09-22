using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Platform.Api.Data;
using Platform.Shared.Entities.Common;

namespace Platform.Api.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IRepository{TEntity}"/>. Registered as
/// an open generic, so a new entity needs no repository class of its own.
/// Add an entity-specific repository only for queries that do not fit here.
/// </summary>
/// <typeparam name="TEntity">Entity handled by this repository.</typeparam>
public class Repository<TEntity> : IRepository<TEntity> where TEntity : BaseEntity
{
    /// <summary>The entity set this repository works on.</summary>
    protected readonly DbSet<TEntity> Set;

    /// <summary>
    /// Creates the repository.
    /// </summary>
    /// <param name="context">Shared unit-of-work context.</param>
    public Repository(AppDbContext context)
    {
        Set = context.Set<TEntity>();
    }

    /// <inheritdoc />
    public IQueryable<TEntity> Query(bool asTracking = false) =>
        asTracking ? Set.AsTracking() : Set.AsNoTracking();

    /// <inheritdoc />
    public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(predicate, cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        await Set.AddAsync(entity, cancellationToken);

    /// <inheritdoc />
    public void Update(TEntity entity) => Set.Update(entity);
}
