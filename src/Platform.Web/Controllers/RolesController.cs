using FluentValidation;
using Microsoft.AspNetCore.Mvc.Rendering;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Identity;
using Platform.Web.Common;
using Platform.Web.Services.Api;

namespace Platform.Web.Controllers;

/// <summary>
/// Role screens. Everything is inherited from <see cref="CrudController{TDto,TCreate,TUpdate}"/>;
/// this class only supplies the capability checkboxes from the shared catalogue.
/// </summary>
public sealed class RolesController : CrudController<RoleDto, CreateRoleRequest, UpdateRoleRequest>
{
    /// <summary>Capability checkboxes, grouped as in the catalogue. Built once.</summary>
    private static readonly IReadOnlyList<SelectListItem> CapabilityOptions = BuildCapabilityOptions();

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

    /// <summary>
    /// Makes the capability catalogue available to the form.
    /// </summary>
    /// <param name="cancellationToken">Unused; nothing is fetched.</param>
    /// <returns>A completed task.</returns>
    protected override Task PrepareViewAsync(CancellationToken cancellationToken)
    {
        ViewData[ViewDataKeys.CapabilityOptions] = CapabilityOptions;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Converts the shared capability catalogue into grouped checkbox options.
    /// </summary>
    /// <returns>One option per capability.</returns>
    private static IReadOnlyList<SelectListItem> BuildCapabilityOptions()
    {
        var groups = new Dictionary<string, SelectListGroup>();
        return Capabilities.All
            .Select(c => new SelectListItem(c.Description, c.Code)
            {
                Group = groups.TryGetValue(c.Group, out var g) ? g : groups[c.Group] = new SelectListGroup { Name = c.Group },
            })
            .ToList();
    }
}
