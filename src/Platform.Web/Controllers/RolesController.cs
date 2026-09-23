using FluentValidation;
using Platform.Shared.Dtos.Identity;
using Platform.Web.Services.Api;

namespace Platform.Web.Controllers;

/// <summary>
/// Role screens. Everything is inherited from <see cref="CrudController{TDto,TCreate,TUpdate}"/>;
/// the access grid is built in the view from the shared feature catalogue.
/// </summary>
public sealed class RolesController : CrudController<RoleDto, CreateRoleRequest, UpdateRoleRequest>
{
    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="api">Role API client.</param>
    /// <param name="createValidator">Shared create validator.</param>
    /// <param name="updateValidator">Shared update validator.</param>
    public RolesController(
        ICrudApiClient<RoleDto, CreateRoleRequest, UpdateRoleRequest> api,
        IValidator<CreateRoleRequest> createValidator,
        IValidator<UpdateRoleRequest> updateValidator)
        : base(api, createValidator, updateValidator)
    {
    }

    /// <inheritdoc />
    protected override string SingularName => "Role";

    /// <inheritdoc />
    protected override string PluralName => "Roles";

    /// <inheritdoc />
    protected override UpdateRoleRequest ToUpdateRequest(RoleDto dto) => new()
    {
        Name = dto.Name,
        Capabilities = dto.Capabilities.ToList(),
        IsActive = dto.IsActive,
    };
}
