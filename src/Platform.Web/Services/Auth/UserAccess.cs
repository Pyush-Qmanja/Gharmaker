using Platform.Shared.Constants;
using Platform.Shared.Dtos.Auth;
using Platform.Web.Services.Api;

namespace Platform.Web.Services.Auth;

/// <summary>
/// What the signed-in user may do, for deciding which menus, tiles and
/// buttons to show. Purely cosmetic: the API enforces every capability itself.
/// </summary>
public interface IUserAccess
{
    /// <summary>
    /// Checks one capability for the current user.
    /// </summary>
    /// <param name="capability">Code from <see cref="Capabilities"/>.</param>
    /// <returns>True when the user holds it right now.</returns>
    Task<bool> CanAsync(string capability);
}

/// <summary>
/// <see cref="IUserAccess"/> backed by <c>GET /api/auth/me</c>, called at most
/// once per page request so a changed role shows on the next page load.
/// </summary>
public sealed class UserAccess : IUserAccess
{
    private readonly IApiClient _api;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private IReadOnlySet<string>? _capabilities;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="api">API client.</param>
    /// <param name="httpContextAccessor">Tells whether anyone is signed in.</param>
    public UserAccess(IApiClient api, IHttpContextAccessor httpContextAccessor)
    {
        _api = api;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public async Task<bool> CanAsync(string capability) =>
        (await LoadAsync()).Contains(capability);

    /// <summary>
    /// Fetches the current user's capabilities once per request.
    /// </summary>
    /// <returns>The capability codes; empty when signed out or the call fails.</returns>
    private async Task<IReadOnlySet<string>> LoadAsync()
    {
        if (_capabilities is not null)
        {
            return _capabilities;
        }

        if (_httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated != true)
        {
            return _capabilities = new HashSet<string>();
        }

        var result = await _api.GetAsync<CurrentUserResponse>(ApiRoutes.CurrentUser);
        return _capabilities = result.Value?.Capabilities.ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>();
    }
}
