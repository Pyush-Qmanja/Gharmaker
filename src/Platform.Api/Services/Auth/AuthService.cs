using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Platform.Api.Common.Exceptions;
using Platform.Api.Data;
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
public sealed class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ITokenService _tokenService;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="context">Database context.</param>
    /// <param name="passwordHasher">Verifies password hashes.</param>
    /// <param name="tokenService">Issues the JWT.</param>
    public AuthService(AppDbContext context, IPasswordHasher<User> passwordHasher, ITokenService tokenService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    /// <inheritdoc />
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        string email = request.Email.Trim().ToLowerInvariant();

        // Sign-in happens before an organisation is known, so the org filter
        // is bypassed here and only here.
        User? user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive, cancellationToken);

        bool orgActive = user is not null && await _context.Organisations
            .AnyAsync(o => o.Id == user.OrgId && o.IsActive, cancellationToken);

        if (user is null || !orgActive
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
}
