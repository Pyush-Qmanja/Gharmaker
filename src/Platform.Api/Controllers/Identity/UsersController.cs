using Microsoft.AspNetCore.Mvc;
using Platform.Api.Security.Authorization;
using Platform.Api.Services;
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
    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="service">User operations.</param>
    public UsersController(ICrudService<UserDto, CreateUserRequest, UpdateUserRequest> service)
        : base(service)
    {
    }
}
