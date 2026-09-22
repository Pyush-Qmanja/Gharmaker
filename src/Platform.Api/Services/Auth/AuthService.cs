using Google.Cloud.Firestore;
using Platform.Api.Common;
using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Repositories;
using Platform.Api.Security;
using Platform.Api.Security.Authorization;
using Platform.Shared.Dtos.Auth;
using Platform.Shared.Entities.Identity;

namespace Platform.Api.Services.Auth;

/// <summary>
/// Signs users in.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Verifies credentials with Firebase Authentication and issues the API's access token.
    /// </summary>
    /// <param name="request">Validated credentials.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The token and the signed-in user's details.</returns>
    /// <exception cref="AuthenticationFailedException">Wrong credentials, disabled account, no platform user, or inactive user or organisation.</exception>
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Describes the signed-in caller and their current capabilities.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The caller.</returns>
    /// <exception cref="AuthenticationFailedException">The user no longer exists or is inactive.</exception>
    Task<CurrentUserResponse> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of <see cref="IAuthService"/>: Firebase Authentication checks
/// the password, the <c>users</c> collection supplies the organisation and
/// status, and the API issues its own JWT.
/// </summary>
/// <remarks>
/// Sign-in happens before the caller's organisation is known, so this is one
/// of the two places (with seeding) allowed to read Firestore directly instead
/// of through the org-scoped <c>IRepository&lt;T&gt;</c>.
/// </remarks>
public sealed class AuthService : IAuthService
{
    private readonly IFirestoreContext _context;
    private readonly IIdentityProvider _identityProvider;
    private readonly ITokenService _tokenService;
    private readonly IRepository<User> _users;
    private readonly IPermissionService _permissions;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="context">Database entry point.</param>
    /// <param name="identityProvider">Verifies the password (Firebase Authentication).</param>
    /// <param name="tokenService">Issues the JWT.</param>
    /// <param name="users">Org-scoped user access, for the current caller.</param>
    /// <param name="permissions">Current caller's capabilities.</param>
    /// <param name="currentUser">Caller identity from the token.</param>
    public AuthService(
        IFirestoreContext context,
        IIdentityProvider identityProvider,
        ITokenService tokenService,
        IRepository<User> users,
        IPermissionService permissions,
        ICurrentUser currentUser)
    {
        _context = context;
        _identityProvider = identityProvider;
        _tokenService = tokenService;
        _users = users;
        _permissions = permissions;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<CurrentUserResponse> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        User? user = _currentUser.UserId is { } id ? await _users.GetByIdAsync(id, cancellationToken) : null;
        if (user is null || !user.IsActive)
        {
            throw new AuthenticationFailedException();
        }

        IReadOnlySet<string> capabilities = await _permissions.GetCapabilitiesAsync(cancellationToken);
        return new CurrentUserResponse
        {
            UserId = user.Id,
            OrgId = user.OrgId,
            Name = user.Name,
            Email = user.Email,
            Capabilities = capabilities.Order(StringComparer.Ordinal).ToList(),
        };
    }

    /// <inheritdoc />
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        string email = request.Email.Trim().ToLowerInvariant();

        string authUid = await _identityProvider.VerifyPasswordAsync(email, request.Password, cancellationToken)
            ?? throw new AuthenticationFailedException();

        User? user = await FindUserAsync(authUid, cancellationToken);
        if (user is null || !user.IsActive || !await IsOrganisationActiveAsync(user.OrgId, cancellationToken))
        {
            throw new AuthenticationFailedException();
        }

        var (token, expiresAt) = _tokenService.CreateAccessToken(user);
        return new LoginResponse
        {
            AccessToken = token,
            ExpiresAt = expiresAt,
            UserId = user.Id,
            OrgId = user.OrgId,
            Name = user.Name,
            Email = user.Email,
        };
    }

    /// <summary>
    /// Finds the platform user linked to a Firebase account.
    /// </summary>
    /// <param name="authUid">Firebase Authentication uid.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The user, or null when the account has no platform user.</returns>
    private async Task<User?> FindUserAsync(string authUid, CancellationToken cancellationToken)
    {
        QuerySnapshot matches = await _context.Collection<User>()
            .WhereEqualTo(FirestoreNaming.Field(nameof(User.AuthUid)), authUid)
            .Limit(1)
            .GetSnapshotAsync(cancellationToken);

        return matches.Documents.Select(DocumentConverter.FromDocument<User>).FirstOrDefault();
    }

    /// <summary>
    /// Checks that the user's organisation exists and is active.
    /// </summary>
    /// <param name="orgId">Organisation id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when the organisation may sign in.</returns>
    private async Task<bool> IsOrganisationActiveAsync(Guid orgId, CancellationToken cancellationToken)
    {
        DocumentSnapshot snapshot = await _context.Collection<Organisation>()
            .Document(orgId.ToString())
            .GetSnapshotAsync(cancellationToken);

        return snapshot.Exists && DocumentConverter.FromDocument<Organisation>(snapshot).IsActive;
    }
}
