using Platform.Shared.Constants;
using Platform.Shared.Dtos.Auth;
using Platform.Web.Services.Api;

namespace Platform.Web.Services.Auth;

/// <summary>
/// State of the signed-in staff session, as the API sees it right now.
/// </summary>
public enum SessionState
{
    /// <summary>Nobody is signed in.</summary>
    SignedOut,

    /// <summary>The session is valid.</summary>
    Live,

    /// <summary>The API refused the session: deactivated, signed out everywhere, or expired.</summary>
    Ended,

    /// <summary>The API could not be asked (e.g. unreachable); treated as still signed in.</summary>
    Unknown,
}

/// <summary>
/// What the signed-in user may do, for deciding which menus, tiles and
/// buttons to show, and whether their session is still live. Menus are purely
/// cosmetic: the API enforces every capability itself.
/// </summary>
public interface IUserAccess
{
    /// <summary>
    /// Checks one capability for the current user.
    /// </summary>
    /// <param name="capability">Code from <see cref="Capabilities"/>.</param>
    /// <returns>True when the user holds it right now.</returns>
    Task<bool> CanAsync(string capability);

    /// <summary>
    /// The roles the current user holds, for offering only roles below them.
    /// </summary>
    /// <returns>Role ids; empty when signed out or unknown.</returns>
    Task<IReadOnlySet<Guid>> GetRoleIdsAsync();

    /// <summary>
    /// The current user as the API knows them now (name, email, access).
    /// </summary>
    /// <returns>The user, or null when signed out, ended or unknown.</returns>
    Task<CurrentUserResponse?> GetCurrentAsync();

    /// <summary>
    /// Whether the signed-in session is still accepted by the API.
    /// </summary>
    /// <returns>The session state.</returns>
    Task<SessionState> GetSessionStateAsync();
}

/// <summary>
/// <see cref="IUserAccess"/> backed by <c>GET /api/auth/me</c>, called at most
/// once per request. Because it runs on every page, a changed role or a
/// deactivation shows on the user's very next click, without signing in again.
/// </summary>
public sealed class UserAccess : IUserAccess
{
    private readonly IApiClient _api;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private Task<(SessionState State, CurrentUserResponse? User)>? _loaded;

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
        (await LoadAsync()).User?.Capabilities.Contains(capability, StringComparer.Ordinal) == true;

    /// <inheritdoc />
    public async Task<IReadOnlySet<Guid>> GetRoleIdsAsync() =>
        (await LoadAsync()).User?.RoleIds.ToHashSet() ?? new HashSet<Guid>();

    /// <inheritdoc />
    public async Task<CurrentUserResponse?> GetCurrentAsync() => (await LoadAsync()).User;

    /// <inheritdoc />
    public async Task<SessionState> GetSessionStateAsync() => (await LoadAsync()).State;

    /// <summary>
    /// Asks the API once per request.
    /// </summary>
    /// <returns>The session state and, when live, the user.</returns>
    private Task<(SessionState State, CurrentUserResponse? User)> LoadAsync() =>
        _loaded ??= FetchAsync();

    /// <summary>
    /// Calls <c>/api/auth/me</c>: 401 means the session has ended; any other failure is unknown.
    /// </summary>
    /// <returns>The session state and, when live, the user.</returns>
    private async Task<(SessionState State, CurrentUserResponse? User)> FetchAsync()
    {
        if (_httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated != true)
        {
            return (SessionState.SignedOut, null);
        }

        var result = await _api.GetAsync<CurrentUserResponse>(ApiRoutes.CurrentUser);
        if (result.IsUnauthorized)
        {
            return (SessionState.Ended, null);
        }

        return result.IsSuccess && result.Value is not null
            ? (SessionState.Live, result.Value)
            : (SessionState.Unknown, null);
    }
}
