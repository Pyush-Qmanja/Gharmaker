using Platform.Api.Data;

namespace Platform.Api.Repositories;

/// <summary>
/// Commits everything staged by repositories in the current request as one
/// atomic save.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Saves all staged changes in one database transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>Number of rows written.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs work that saves more than once inside a single explicit transaction.
    /// Rolls back if <paramref name="work"/> throws.
    /// </summary>
    /// <param name="work">Operations to run; may call <see cref="SaveChangesAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the transaction.</param>
    /// <returns>A task that completes once the transaction commits.</returns>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default);
}

/// <summary>
/// EF Core implementation of <see cref="IUnitOfWork"/>.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    /// <summary>
    /// Creates the unit of work.
    /// </summary>
    /// <param name="context">Context shared with the request's repositories.</param>
    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

    /// <inheritdoc />
    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await work(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
