using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Identity;
using Platform.Shared.Dtos.Inventory;
using Platform.Shared.Entities.Identity;
using Platform.Web.Common;
using Platform.Web.Models;
using Platform.Web.Services.Api;
using Platform.Web.Services.Auth;

namespace Platform.Web.Controllers;

/// <summary>
/// User screens. Everything is inherited from <see cref="CrudController{TDto,TCreate,TUpdate}"/>;
/// this class only loads the roles and places shown in the table and offered in the form.
/// </summary>
public sealed class UsersController : CrudController<UserDto, CreateUserRequest, UpdateUserRequest>
{
    private readonly ICrudApiClient<RoleDto, CreateRoleRequest, UpdateRoleRequest> _roles;
    private readonly ICrudApiClient<WarehouseDto, CreateWarehouseRequest, UpdateWarehouseRequest> _warehouses;
    private readonly IUserAccess _access;
    private readonly IApiClient _apiClient;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="api">User API client.</param>
    /// <param name="createValidator">Shared create validator.</param>
    /// <param name="updateValidator">Shared update validator.</param>
    /// <param name="roles">Role API client, for role names, options and what each grants.</param>
    /// <param name="warehouses">Warehouse API client, for scope names and options.</param>
    /// <param name="access">The signed-in user's roles, to offer only roles below them.</param>
    /// <param name="apiClient">Raw API client, for signing a user out everywhere.</param>
    public UsersController(
        ICrudApiClient<UserDto, CreateUserRequest, UpdateUserRequest> api,
        IValidator<CreateUserRequest> createValidator,
        IValidator<UpdateUserRequest> updateValidator,
        ICrudApiClient<RoleDto, CreateRoleRequest, UpdateRoleRequest> roles,
        ICrudApiClient<WarehouseDto, CreateWarehouseRequest, UpdateWarehouseRequest> warehouses,
        IUserAccess access,
        IApiClient apiClient)
        : base(api, createValidator, updateValidator)
    {
        _roles = roles;
        _warehouses = warehouses;
        _access = access;
        _apiClient = apiClient;
    }

    /// <summary>
    /// Signs a user out on every device at once (lost phone, shared computer,
    /// someone leaving). Their next click takes them to the sign-in page.
    /// </summary>
    /// <param name="id">User id.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>Back to the user's page with the outcome.</returns>
    [HttpPost]
    public async Task<IActionResult> EndSessions(Guid id, CancellationToken cancellationToken)
    {
        var result = await _apiClient.PostAsync<object, object>(
            $"{ApiRoutes.Users}/{id}/{ApiRoutes.EndSessionsSegment}", new object(), cancellationToken);
        if (result.IsForbidden && !string.IsNullOrEmpty(result.ErrorMessage))
        {
            FlashError(result.ErrorMessage);
        }
        else if (HandleAccess(result) is { } denied)
        {
            return denied;
        }
        else if (result.IsSuccess)
        {
            FlashSuccess("Signed out on every device. Their next click takes them to the sign-in page.");
        }
        else
        {
            FlashError(result.ErrorMessage ?? "Could not sign the user out.");
        }

        return RedirectToAction(nameof(Edit), new { id });
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
        Access = dto.Access.ToList(),
        IsActive = dto.IsActive,
    };

    /// <summary>
    /// Loads roles and the places the signed-in user can see: name maps for the
    /// table, role options and what each role grants, and the scope options for
    /// both the role scope and the per-feature access grid. The API only returns
    /// warehouses in the signed-in user's own scope, so they can only offer what they see.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>A task that completes when ViewData is ready.</returns>
    protected override async Task PrepareViewAsync(CancellationToken cancellationToken)
    {
        Task<IReadOnlyList<RoleDto>> rolesTask = _roles.ListForLookupAsync(cancellationToken);
        Task<IReadOnlyList<WarehouseDto>> warehousesTask = _warehouses.ListForLookupAsync(cancellationToken);
        IReadOnlyList<RoleDto> roles = await rolesTask;
        IReadOnlyList<WarehouseDto> warehouses = await warehousesTask;

        ViewData[ViewDataKeys.Roles] = roles;
        ViewData[ViewDataKeys.RoleNames] = roles.ToDictionary(r => r.Id, r => r.Name);
        var chart = new RoleChart(roles, await _access.GetRoleIdsAsync());
        ViewData[ViewDataKeys.RoleChart] = chart;
        ViewData[ViewDataKeys.RoleOptions] = chart.RoleOptions();

        var everywhere = new SelectListGroup { Name = "Everywhere" };
        var warehouseGroup = new SelectListGroup { Name = "Warehouses" };
        var scopeOptions = new List<SelectListItem>
        {
            new(AccessSummary.Everywhere, new ScopeGrantDto { ScopeType = ScopeType.Global }.ToString()) { Group = everywhere },
        };
        scopeOptions.AddRange(warehouses.OrderBy(w => w.Code).Select(w => new SelectListItem(
            WarehouseLabel(w),
            new ScopeGrantDto { ScopeType = ScopeType.Warehouse, ScopeId = w.Id }.ToString())
        {
            Group = warehouseGroup,
        }));

        ViewData[ViewDataKeys.ScopeOptions] = scopeOptions;
        ViewData[ViewDataKeys.ScopeNames] = scopeOptions.ToDictionary(o => o.Value, o => o.Text);
    }

    /// <summary>
    /// Builds a warehouse's label in the scope pickers.
    /// </summary>
    /// <param name="warehouse">Warehouse.</param>
    /// <returns>"CODE — Name", marked when inactive.</returns>
    private static string WarehouseLabel(WarehouseDto warehouse) =>
        $"{warehouse.Code} — {warehouse.Name}{(warehouse.IsActive ? string.Empty : " (inactive)")}";
}
