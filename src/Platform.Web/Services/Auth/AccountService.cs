using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Auth;
using Platform.Web.Services.Api;

namespace Platform.Web.Services.Auth;

/// <summary>
/// Signs users in and out of the UI.
/// </summary>
public interface IAccountService
{
    /// <summary>
    /// Exchanges credentials for a JWT at the API, then issues the UI auth
    /// cookie holding that token.
    /// </summary>
    /// <param name="request">Validated credentials.</param>
    /// <param name="cancellationToken">Cancels the API call.</param>
    /// <returns>The API result; on failure no cookie is issued.</returns>
    Task<ApiResult<LoginResponse>> SignInAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the UI auth cookie.
    /// </summary>
    /// <returns>A task that completes once signed out.</returns>
    Task SignOutAsync();
}

/// <summary>
/// Cookie-based implementation of <see cref="IAccountService"/>.
/// </summary>
public sealed class AccountService : IAccountService
{
    private readonly IApiClient _api;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="api">Calls the API login endpoint.</param>
    /// <param name="httpContextAccessor">Issues and clears the cookie.</param>
    public AccountService(IApiClient api, IHttpContextAccessor httpContextAccessor)
    {
        _api = api;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public async Task<ApiResult<LoginResponse>> SignInAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _api.PostAsync<LoginRequest, LoginResponse>(ApiRoutes.Login, request, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return result;
        }

        LoginResponse login = result.Value;
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimNames.UserId, login.UserId.ToString()),
                new Claim(ClaimNames.OrgId, login.OrgId.ToString()),
                new Claim(ClaimNames.Name, login.Name),
                new Claim(ClaimNames.Email, login.Email),
                new Claim(ClaimNames.AccessToken, login.AccessToken),
            },
            CookieAuthenticationDefaults.AuthenticationScheme,
            ClaimNames.Name,
            roleType: null);

        // The cookie expires with the token, so the UI never holds a dead token.
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { ExpiresUtc = login.ExpiresAt, IsPersistent = false });

        return result;
    }

    /// <inheritdoc />
    public Task SignOutAsync() => HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    /// <summary>Current request; sign-in only ever runs inside one.</summary>
    private HttpContext HttpContext =>
        _httpContextAccessor.HttpContext ?? throw new InvalidOperationException("No active HTTP request.");
}
