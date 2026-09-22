using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.Services.Auth;
using Platform.Shared.Dtos.Auth;

namespace Platform.Api.Controllers;

/// <summary>
/// Sign-in: <c>/api/auth</c>.
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="authService">Sign-in operations.</param>
    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Exchanges email and password for a JWT access token.
    /// </summary>
    /// <param name="request">Credentials.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with the token, 400 if malformed, or 401 if the credentials are wrong.</returns>
    [HttpPost("login")]
    [AllowAnonymous]
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
}
