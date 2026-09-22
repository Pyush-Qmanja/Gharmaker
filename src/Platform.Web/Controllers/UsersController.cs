using FluentValidation;
using Microsoft.AspNetCore.Mvc.Rendering;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Identity;
using Platform.Web.Common;
using Platform.Web.Services.Api;

namespace Platform.Web.Controllers;

/// <summary>
/// User screens. Everything is inherited from <see cref="CrudController{TDto,TCreate,TUpdate}"/>;
/// this class only loads the roles shown in the table and offered in the form.
/// </summary>
public sealed class UsersController : CrudController<UserDto, CreateUserRequest, UpdateUserRequest>
{
    private readonly ICrudApiClient<RoleDto, CreateRoleRequest, UpdateRoleRequest> _roles;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="api">User API client.</param>
    /// <param name="createValidator">Shared create validator.</param>
    /// <param name="updateValidator">Shared update validator.</param>
    /// <param name="roles">Role API client, for role names and options.</param>
    public UsersController(
        ICrudApiClient<UserDto, CreateUserRequest, UpdateUserRequest> api,
        IValidator<CreateUserRequest> createValidator,
        IValidator<UpdateUserRequest> updateValidator,
        ICrudApiClient<RoleDto, CreateRoleRequest, UpdateRoleRequest> roles)
        : base(api, createValidator, updateValidator)
    {
        _roles = roles;
    }

    /// <inheritdoc />
    protected override string SingularName => "User";

    /// <inheritdoc />
    protected override string PluralName => "Users";

    /// <inheritdoc />
    protected override UpdateUserRequest ToUpdateRequest(UserDto dto) => new()
    {
        Name = dto.Name,
        Phone = dto.Phone,
        RoleIds = dto.RoleIds.ToList(),
        Scopes = dto.Scopes.ToList(),
        IsActive = dto.IsActive,
    };

    /// <summary>
    /// Loads role names for the table and active roles as form options. If the
    /// user may not view roles, both are left empty and the screens degrade gracefully.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>A task that completes when ViewData is ready.</returns>
    protected override async Task PrepareViewAsync(CancellationToken cancellationToken)
    {
        var result = await _roles.GetPagedAsync(new PagedRequest { PageSize = Paging.MaxPageSize }, cancellationToken);
        IReadOnlyList<RoleDto> roles = result.Value?.Items ?? Array.Empty<RoleDto>();

        ViewData[ViewDataKeys.RoleNames] = roles.ToDictionary(r => r.Id, r => r.Name);
        ViewData[ViewDataKeys.RoleOptions] = roles
            .Where(r => r.IsActive)
            .Select(r => new SelectListItem(r.Name, r.Id.ToString()))
            .ToList();
    }
}
