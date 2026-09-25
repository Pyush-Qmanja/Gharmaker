using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Api.Security;
using Platform.Api.Common;
using Platform.Api.Services.Auth;
using Platform.Api.Services.Identity;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Auth;

namespace Platform.Api.Controllers;

/// <summary>
/// Sign-in: <c>/api/auth</c>.
/// </summary>
[ApiController]
[Route(ApiRoutes.Auth)]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserSessionService _sessions;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="authService">Sign-in operations.</param>
    /// <param name="sessions">Ends the caller's open sessions.</param>
    /// <param name="currentUser">Caller identity from the token.</param>
    public AuthController(IAuthService authService, IUserSessionService sessions, ICurrentUser currentUser)
    {
        _authService = authService;
        _sessions = sessions;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Exchanges email and password for a JWT access token.
    /// </summary>
    /// <param name="request">Credentials.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with the token, 400 if malformed, or 401 if the credentials are wrong.</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.SignIn)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken) =>
        Ok(await _authService.LoginAsync(request, cancellationToken));

    /// <summary>
    /// Returns the signed-in caller and the capabilities they hold right now.
    /// The UI uses this to decide which menus to show.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with the caller, or 401 when the token is missing or expired, or the user is inactive.</returns>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken cancellationToken) =>
        Ok(await _authService.GetCurrentUserAsync(cancellationToken));

    /// <summary>
    /// Signs the caller out on every device, this one included. Use after a
    /// lost phone or a sign-in on a shared computer.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>204 when done.</returns>
    [HttpPost(ApiRoutes.EndSessionsSegment)]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EndSessions(CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } id)
        {
            return Unauthorized();
        }

        await _sessions.EndSessionsAsync(id, cancellationToken);
        return NoContent();
    }
}
