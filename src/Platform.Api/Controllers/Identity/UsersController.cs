using Microsoft.AspNetCore.Mvc;
using Platform.Api.Security.Authorization;
using Platform.Api.Services;
using Platform.Api.Services.Identity;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Identity;

namespace Platform.Api.Controllers.Identity;

/// <summary>
/// Users, their roles and scopes: <c>/api/users</c>. All endpoints come from
/// <see cref="CrudControllerBase{TDto,TCreate,TUpdate}"/>.
/// </summary>
[Route(ApiRoutes.Users)]
[CrudCapabilities(Capabilities.UsersView, Capabilities.UsersManage)]
public sealed class UsersController : CrudControllerBase<UserDto, CreateUserRequest, UpdateUserRequest>
{
    private readonly IUserSessionService _sessions;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="service">User operations.</param>
    /// <param name="sessions">Ends a user's open sessions.</param>
    public UsersController(ICrudService<UserDto, CreateUserRequest, UpdateUserRequest> service, IUserSessionService sessions)
        : base(service)
    {
        _sessions = sessions;
    }

    /// <summary>
    /// Signs a user out on every device now (lost phone, shared computer,
    /// suspicious sign-in). They can sign in again straight away. Needs users
    /// manage, and the user must be below the caller in the hierarchy.
    /// </summary>
    /// <param name="id">User id.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>204 when done.</returns>
    [HttpPost("{id:guid}/" + ApiRoutes.EndSessionsSegment)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EndSessions(Guid id, CancellationToken cancellationToken)
    {
        await _sessions.EndSessionsAsync(id, cancellationToken);
        return NoContent();
    }
}
