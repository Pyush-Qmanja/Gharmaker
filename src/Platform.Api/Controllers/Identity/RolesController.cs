using Microsoft.AspNetCore.Mvc;
using Platform.Api.Security.Authorization;
using Platform.Api.Services;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Identity;

namespace Platform.Api.Controllers.Identity;

/// <summary>
/// Roles and their capabilities: <c>/api/roles</c>. All endpoints come from
/// <see cref="CrudControllerBase{TDto,TCreate,TUpdate}"/>.
/// </summary>
[Route(ApiRoutes.Roles)]
[CrudCapabilities(Capabilities.RolesView, Capabilities.RolesManage)]
public sealed class RolesController : CrudControllerBase<RoleDto, CreateRoleRequest, UpdateRoleRequest>
{
    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="service">Role operations.</param>
    public RolesController(ICrudService<RoleDto, CreateRoleRequest, UpdateRoleRequest> service)
        : base(service)
    {
    }
}
