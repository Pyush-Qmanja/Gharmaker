using Platform.Api.Common;
using Platform.Api.Repositories;
using Platform.Shared.Entities.Identity;

namespace Platform.Api.Security.Authorization;

/// <summary>
/// The staff user making the current request, read from the database once per
/// request and shared by everything that needs it (the session guard, the
/// permission checks, the hierarchy rules). Never cached across requests, so a
/// change made by an administrator applies to a signed-in user on their next call.
/// </summary>
public interface ICallerAccount
{
    /// <summary>
    /// Loads the calling staff user.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The user, or null for a customer, an anonymous caller or an unknown id.</returns>
    Task<User?> GetAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Firestore-backed <see cref="ICallerAccount"/>, scoped per request.
/// </summary>
public sealed class CallerAccount : ICallerAccount
{
    private readonly ICurrentUser _currentUser;
    private readonly IRepository<User> _users;
    private Task<User?>? _user;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="currentUser">Caller identity from the token.</param>
    /// <param name="users">User data access (org-scoped).</param>
    public CallerAccount(ICurrentUser currentUser, IRepository<User> users)
    {
        _currentUser = currentUser;
        _users = users;
    }

    /// <inheritdoc />
    public Task<User?> GetAsync(CancellationToken cancellationToken = default) =>
        _user ??= LoadAsync(cancellationToken);

    /// <summary>
    /// Reads the user; a customer never resolves to a staff user, whatever their id.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The user, or null.</returns>
    private async Task<User?> LoadAsync(CancellationToken cancellationToken) =>
        _currentUser.UserId is { } userId && !_currentUser.IsCustomer
            ? await _users.GetByIdAsync(userId, cancellationToken)
            : null;
}
