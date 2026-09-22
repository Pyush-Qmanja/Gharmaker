using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Identity;
using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Security;
using Platform.Shared.Dtos.Auth;
using Platform.Shared.Entities.Identity;

namespace Platform.Api.Services.Auth;

/// <summary>
/// Signs users in.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Verifies credentials and issues an access token.
    /// </summary>
    /// <param name="request">Validated credentials.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The token and the signed-in user's details.</returns>
    /// <exception cref="AuthenticationFailedException">Unknown email, wrong password, or inactive user or organisation.</exception>
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Password-based implementation of <see cref="IAuthService"/>.
/// </summary>
/// <remarks>
/// Sign-in happens before the caller's organisation is known, so this is one
/// of the two places (with seeding) allowed to read Firestore directly instead
/// of through the org-scoped <c>IRepository&lt;T&gt;</c>.
/// </remarks>
public sealed class AuthService : IAuthService
{
    private readonly IFirestoreContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ITokenService _tokenService;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="context">Database entry point.</param>
    /// <param name="passwordHasher">Verifies password hashes.</param>
    /// <param name="tokenService">Issues the JWT.</param>
    public AuthService(IFirestoreContext context, IPasswordHasher<User> passwordHasher, ITokenService tokenService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    /// <inheritdoc />
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        string email = request.Email.Trim().ToLowerInvariant();

        QuerySnapshot matches = await _context.Collection<User>()
            .WhereEqualTo(FirestoreNaming.Field(nameof(User.Email)), email)
            .Limit(1)
            .GetSnapshotAsync(cancellationToken);

        User? user = matches.Documents.Select(DocumentConverter.FromDocument<User>).FirstOrDefault();
        if (user is null || !user.IsActive
            || !await IsOrganisationActiveAsync(user.OrgId, cancellationToken)
            || _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
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
