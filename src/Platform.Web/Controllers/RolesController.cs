using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Platform.Shared.Dtos.Identity;
using Platform.Web.Common;
using Platform.Web.Models;
using Platform.Web.Services.Api;
using Platform.Web.Services.Auth;

namespace Platform.Web.Controllers;

/// <summary>
/// Role management screens, on the generic CRUD pages, plus the role
/// hierarchy chart. Each role reports to a parent; people manage only the
/// roles below their own.
/// </summary>
public sealed class RolesController : CrudController<RoleDto, CreateRoleRequest, UpdateRoleRequest>
{
    private readonly IUserAccess _access;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="api">Typed client for <c>/api/roles</c>.</param>
    /// <param name="createValidator">Create validator.</param>
    /// <param name="updateValidator">Update validator.</param>
    /// <param name="access">The signed-in user's roles, to draw the hierarchy from their point of view.</param>
    public RolesController(
        ICrudApiClient<RoleDto, CreateRoleRequest, UpdateRoleRequest> api,
        IValidator<CreateRoleRequest> createValidator,
        IValidator<UpdateRoleRequest> updateValidator,
        IUserAccess access)
        : base(api, createValidator, updateValidator)
    {
        _access = access;
    }

    /// <inheritdoc />
    protected override string SingularName => "Role";

    /// <inheritdoc />
    protected override string PluralName => "Roles";

    /// <summary>
    /// The role hierarchy: every role under the one it reports to, with what it
    /// can do and whether the signed-in user may change it.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The chart page.</returns>
    [HttpGet]
    public async Task<IActionResult> Hierarchy(CancellationToken cancellationToken) =>
        View(await ChartAsync(cancellationToken));

    /// <inheritdoc />
    protected override UpdateRoleRequest ToUpdateRequest(RoleDto dto) => new()
    {
        Name = dto.Name,
        ParentId = dto.ParentId,
        Capabilities = dto.Capabilities.ToList(),
        IsActive = dto.IsActive,
    };

    /// <summary>
    /// Loads the chart for the list (parent names), the form ("Reports to"
    /// choices, built-in role notice) and the header link to the chart.
    /// </summary>
    /// <param name="cancellationToken">Cancels the lookups.</param>
    /// <returns>A task that completes when the view data is set.</returns>
    protected override async Task PrepareViewAsync(CancellationToken cancellationToken)
    {
        RoleChart chart = await ChartAsync(cancellationToken);
        Guid? editingId = Guid.TryParse(RouteData.Values["id"] as string, out Guid id) ? id : null;

        ViewData[ViewDataKeys.RoleChart] = chart;
        ViewData[ViewDataKeys.ParentOptions] = chart.ParentOptions(editingId);
        ViewData[ViewDataKeys.IsSystemRole] = chart.Rows.Any(r => r.Role.Id == editingId && r.Role.IsSystem);
        ViewData[ViewDataKeys.HeaderLinks] = new[] { new HeaderLink("Role hierarchy", nameof(Hierarchy), Icons.Layers) };
    }

    /// <summary>
    /// Builds the chart from every role and the signed-in user's roles.
    /// </summary>
    /// <param name="cancellationToken">Cancels the lookups.</param>
    /// <returns>The chart.</returns>
    private async Task<RoleChart> ChartAsync(CancellationToken cancellationToken) =>
        new(await Api.ListForLookupAsync(cancellationToken), await _access.GetRoleIdsAsync());
}
