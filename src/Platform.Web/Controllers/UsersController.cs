using FluentValidation;
using Microsoft.AspNetCore.Mvc.Rendering;
using Platform.Shared.Dtos.Identity;
using Platform.Shared.Dtos.Inventory;
using Platform.Shared.Entities.Identity;
using Platform.Web.Common;
using Platform.Web.Services.Api;

namespace Platform.Web.Controllers;

/// <summary>
/// User screens. Everything is inherited from <see cref="CrudController{TDto,TCreate,TUpdate}"/>;
/// this class only loads the roles and scopes shown in the table and offered in the form.
/// </summary>
public sealed class UsersController : CrudController<UserDto, CreateUserRequest, UpdateUserRequest>
{
    /// <summary>Label of the global scope option.</summary>
    private const string GlobalScopeLabel = "Everywhere (global scope)";

    private readonly ICrudApiClient<RoleDto, CreateRoleRequest, UpdateRoleRequest> _roles;
    private readonly ICrudApiClient<WarehouseDto, CreateWarehouseRequest, UpdateWarehouseRequest> _warehouses;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="api">User API client.</param>
    /// <param name="createValidator">Shared create validator.</param>
    /// <param name="updateValidator">Shared update validator.</param>
    /// <param name="roles">Role API client, for role names and options.</param>
    /// <param name="warehouses">Warehouse API client, for scope names and options.</param>
    public UsersController(
        ICrudApiClient<UserDto, CreateUserRequest, UpdateUserRequest> api,
        IValidator<CreateUserRequest> createValidator,
        IValidator<UpdateUserRequest> updateValidator,
        ICrudApiClient<RoleDto, CreateRoleRequest, UpdateRoleRequest> roles,
        ICrudApiClient<WarehouseDto, CreateWarehouseRequest, UpdateWarehouseRequest> warehouses)
        : base(api, createValidator, updateValidator)
    {
        _roles = roles;
        _warehouses = warehouses;
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
    /// Loads roles and the scopes the signed-in user can see: name maps for the
    /// table and checkbox options for the form. The API only returns warehouses
    /// in the signed-in user's own scope, so they can only offer what they hold.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>A task that completes when ViewData is ready.</returns>
    protected override async Task PrepareViewAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<RoleDto> roles = await _roles.ListForLookupAsync(cancellationToken);
        ViewData[ViewDataKeys.RoleNames] = roles.ToDictionary(r => r.Id, r => r.Name);
        ViewData[ViewDataKeys.RoleOptions] = roles
            .Where(r => r.IsActive)
            .Select(r => new SelectListItem(r.Name, r.Id.ToString()))
            .ToList();

        IReadOnlyList<WarehouseDto> warehouses = await _warehouses.ListForLookupAsync(cancellationToken);
        var everywhere = new SelectListGroup { Name = "Everywhere" };
        var warehouseGroup = new SelectListGroup { Name = "Warehouses" };

        var scopeOptions = new List<SelectListItem>
        {
            new(GlobalScopeLabel, new ScopeGrantDto { ScopeType = ScopeType.Global }.ToString()) { Group = everywhere },
        };
        scopeOptions.AddRange(warehouses.Select(w => new SelectListItem(
            WarehouseLabel(w),
            new ScopeGrantDto { ScopeType = ScopeType.Warehouse, ScopeId = w.Id }.ToString())
        {
            Group = warehouseGroup,
        }));

        ViewData[ViewDataKeys.ScopeOptions] = scopeOptions;
        ViewData[ViewDataKeys.ScopeNames] = scopeOptions.ToDictionary(o => o.Value, o => o.Text);
    }

    /// <summary>
    /// Builds a warehouse's label in the scope picker.
    /// </summary>
    /// <param name="warehouse">Warehouse.</param>
    /// <returns>"CODE — Name", marked when inactive.</returns>
    private static string WarehouseLabel(WarehouseDto warehouse) =>
        $"{warehouse.Code} — {warehouse.Name}{(warehouse.IsActive ? string.Empty : " (inactive)")}";
}
