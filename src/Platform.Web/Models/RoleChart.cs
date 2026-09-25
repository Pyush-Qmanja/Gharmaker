using Microsoft.AspNetCore.Mvc.Rendering;
using Platform.Shared.Common;
using Platform.Shared.Dtos.Identity;

namespace Platform.Web.Models;

/// <summary>
/// One extra button in a list page header, e.g. "Role hierarchy" on Roles.
/// </summary>
/// <param name="Text">Button text.</param>
/// <param name="Action">Action of the same controller.</param>
/// <param name="Icon">Icon name from <c>Icons</c>.</param>
public sealed record HeaderLink(string Text, string Action, string Icon);

/// <summary>
/// The role hierarchy as the signed-in user sees it: the chart in order, and
/// which roles they may manage, give, or attach new roles under. Mirrors the
/// API's rules only to grey out choices; the API enforces them.
/// </summary>
public sealed class RoleChart
{
    /// <summary>Text shown for the built-in role's feature access.</summary>
    public const string EveryFeature = "Every feature";

    private readonly RoleTree _tree;
    private readonly IReadOnlySet<Guid> _held;
    private readonly Dictionary<Guid, RoleDto> _roles;

    /// <summary>
    /// Builds the chart.
    /// </summary>
    /// <param name="roles">Every role of the organisation.</param>
    /// <param name="heldRoleIds">Roles the signed-in user holds.</param>
    public RoleChart(IReadOnlyList<RoleDto> roles, IReadOnlySet<Guid> heldRoleIds)
    {
        _roles = roles.ToDictionary(r => r.Id);
        _tree = new RoleTree(roles.Select(r => new RoleNode(r.Id, r.ParentId, r.IsSystem, r.Name)));
        _held = heldRoleIds.Where(id => _roles.TryGetValue(id, out RoleDto? r) && r.IsActive).ToHashSet();
        IsAdministrator = _tree.RootId is { } root && _held.Contains(root);
        Rows = _tree.Ordered().Select(o => new RoleChartRow(_roles[o.Node.Id], o.Depth, _held.Contains(o.Node.Id), CanManage(o.Node.Id))).ToList();
    }

    /// <summary>True when the signed-in user holds the built-in top role.</summary>
    public bool IsAdministrator { get; }

    /// <summary>Every role in chart order with its depth.</summary>
    public IReadOnlyList<RoleChartRow> Rows { get; }

    /// <summary>
    /// Whether the user may change or deactivate a role.
    /// </summary>
    /// <param name="roleId">Role id.</param>
    /// <returns>True for a role below theirs (any non-built-in role for an administrator).</returns>
    public bool CanManage(Guid roleId) =>
        _roles.TryGetValue(roleId, out RoleDto? role) && !role.IsSystem && (IsAdministrator || _tree.IsBelow(roleId, _held));

    /// <summary>
    /// Whether the user may give a role to someone, or take it away.
    /// </summary>
    /// <param name="roleId">Role id.</param>
    /// <returns>True for a role below theirs, or any role for an administrator.</returns>
    public bool CanGive(Guid roleId) => IsAdministrator || _tree.IsBelow(roleId, _held);

    /// <summary>
    /// The name of the role a role reports to.
    /// </summary>
    /// <param name="role">Role.</param>
    /// <returns>The parent's name, or null for the top role.</returns>
    public string? ParentName(RoleDto role) =>
        _tree.ParentOf(role.Id) is { } parent && _roles.TryGetValue(parent, out RoleDto? found) ? found.Name : null;

    /// <summary>
    /// "Reports to" choices: active roles the user holds or that are below
    /// theirs, never the role itself or one below it, indented by depth.
    /// </summary>
    /// <param name="editingId">Role being edited, or null for a new role.</param>
    /// <returns>Options in chart order.</returns>
    public IReadOnlyList<SelectListItem> ParentOptions(Guid? editingId) =>
        Rows.Where(r => r.Role.IsActive
                        && (IsAdministrator || _tree.IsAtOrBelow(r.Role.Id, _held))
                        && (editingId is not { } id || !_tree.WouldLoop(id, r.Role.Id)))
            .Select(r => new SelectListItem(Indent(r), r.Role.Id.ToString()))
            .ToList();

    /// <summary>
    /// Role checkboxes for the user form, in chart order; roles the user may
    /// not give or take away are shown greyed out (and kept as they are).
    /// </summary>
    /// <returns>Options for active roles.</returns>
    public IReadOnlyList<SelectListItem> RoleOptions() =>
        Rows.Where(r => r.Role.IsActive)
            .Select(r => new SelectListItem(Indent(r), r.Role.Id.ToString()) { Disabled = !CanGive(r.Role.Id) })
            .ToList();

    /// <summary>
    /// A role's name indented to show its place in the chart.
    /// </summary>
    /// <param name="row">Chart row.</param>
    /// <returns>The label.</returns>
    private static string Indent(RoleChartRow row) =>
        row.Depth == 0 ? row.Role.Name : string.Concat(Enumerable.Repeat(" ", row.Depth - 1)) + "└ " + row.Role.Name;
}

/// <summary>
/// One role in the chart.
/// </summary>
/// <param name="Role">The role.</param>
/// <param name="Depth">0 for the top role, 1 for roles reporting to it, and so on.</param>
/// <param name="IsHeld">True when the signed-in user holds it.</param>
/// <param name="CanManage">True when the signed-in user may change it.</param>
public sealed record RoleChartRow(RoleDto Role, int Depth, bool IsHeld, bool CanManage);
