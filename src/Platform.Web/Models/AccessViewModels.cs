using Microsoft.AspNetCore.Mvc.Rendering;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Identity;
using Platform.Shared.Entities.Identity;

namespace Platform.Web.Models;

/// <summary>
/// Which request the access grid posts into.
/// </summary>
public enum AccessGridMode
{
    /// <summary>A role: one capability per feature, posted as <c>Capabilities[i]</c>; no scopes.</summary>
    Role,

    /// <summary>A user: one access row per feature, posted as <c>Access[i].*</c>, with scopes for scoped features.</summary>
    User,
}

/// <summary>
/// One feature's row in the access grid.
/// </summary>
/// <param name="Feature">The feature.</param>
/// <param name="Index">Position in the posted list.</param>
/// <param name="Level">Level currently chosen.</param>
/// <param name="SelectedScopes">Scopes currently chosen (text form, e.g. <c>Warehouse:&lt;id&gt;</c>).</param>
public sealed record AccessGridRow(FeatureInfo Feature, int Index, AccessLevel Level, IReadOnlySet<string> SelectedScopes);

/// <summary>
/// Everything the access grid partial (<c>Views/Shared/_AccessGrid.cshtml</c>)
/// needs: one row per feature with its current level, and the places a
/// scoped feature can be limited to.
/// </summary>
public sealed class AccessGridModel
{
    /// <summary>Levels shown as columns of the segmented control, in order.</summary>
    public static readonly IReadOnlyList<AccessLevel> Levels = new[] { AccessLevel.None, AccessLevel.View, AccessLevel.Manage };

    private AccessGridModel(AccessGridMode mode, IReadOnlyList<AccessGridRow> rows, IReadOnlyList<SelectListItem> scopeOptions)
    {
        Mode = mode;
        Rows = rows;
        ScopeOptions = scopeOptions;
    }

    /// <summary>Which request the grid posts into.</summary>
    public AccessGridMode Mode { get; }

    /// <summary>One row per feature, in catalogue order.</summary>
    public IReadOnlyList<AccessGridRow> Rows { get; }

    /// <summary>Every scope the editor may grant (global and each warehouse...).</summary>
    public IReadOnlyList<SelectListItem> ScopeOptions { get; }

    /// <summary>True when the grid has a "where" column.</summary>
    public bool ShowScopes => Mode == AccessGridMode.User;

    /// <summary>
    /// Builds the grid for a role form.
    /// </summary>
    /// <param name="role">Role being edited.</param>
    /// <returns>The grid model.</returns>
    public static AccessGridModel ForRole(IRoleFields role) =>
        new(AccessGridMode.Role,
            Features.All.Select((f, i) => new AccessGridRow(f, i, f.LevelIn(role.Capabilities), new HashSet<string>())).ToList(),
            Array.Empty<SelectListItem>());

    /// <summary>
    /// Builds the grid for a user form.
    /// </summary>
    /// <param name="user">User being edited.</param>
    /// <param name="scopeOptions">Scopes the editor may grant.</param>
    /// <returns>The grid model.</returns>
    public static AccessGridModel ForUser(IUserFields user, IReadOnlyList<SelectListItem> scopeOptions) =>
        new(AccessGridMode.User,
            Features.All.Select((f, i) =>
            {
                FeatureAccessDto? row = user.Access.FirstOrDefault(a => a.Feature == f.Code);
                return new AccessGridRow(
                    f,
                    i,
                    row?.Level ?? AccessLevel.None,
                    (row?.Scopes ?? new List<ScopeGrantDto>()).Select(s => s.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase));
            }).ToList(),
            scopeOptions);

    /// <summary>
    /// Scope options that apply to one feature: everywhere, plus objects of the feature's scope type.
    /// </summary>
    /// <param name="feature">A scoped feature.</param>
    /// <returns>The options.</returns>
    public IReadOnlyList<SelectListItem> OptionsFor(FeatureInfo feature)
    {
        string global = new ScopeGrantDto { ScopeType = ScopeType.Global }.ToString();
        string prefix = $"{feature.ScopeType}:";
        return ScopeOptions
            .Where(o => o.Value == global || o.Value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Posted field name of a row's level: <c>Capabilities[i]</c> for a role, <c>Access[i].Level</c> for a user.
    /// </summary>
    /// <param name="row">Grid row.</param>
    /// <returns>The field name.</returns>
    public string LevelField(AccessGridRow row) =>
        Mode == AccessGridMode.Role ? $"{nameof(IRoleFields.Capabilities)}[{row.Index}]" : $"{nameof(IUserFields.Access)}[{row.Index}].{nameof(FeatureAccessDto.Level)}";

    /// <summary>
    /// Posted value for choosing a level: the capability code for a role (blank for none), the level name for a user.
    /// </summary>
    /// <param name="row">Grid row.</param>
    /// <param name="level">Level the radio stands for.</param>
    /// <returns>The value.</returns>
    public string LevelValue(AccessGridRow row, AccessLevel level) =>
        Mode == AccessGridMode.User ? level.ToString()
        : level switch
        {
            AccessLevel.View => row.Feature.ViewCapability,
            AccessLevel.Manage => row.Feature.ManageCapability ?? string.Empty,
            _ => string.Empty,
        };
}

/// <summary>
/// One line of a user's effective access: a feature, how much, where, and what gives it.
/// </summary>
/// <param name="Feature">The feature.</param>
/// <param name="Level">Level granted by this source.</param>
/// <param name="Places">Where it applies, as labels.</param>
/// <param name="Source">"Direct" or the role's name.</param>
/// <param name="IsDirect">True for direct access, false for a role.</param>
public sealed record EffectiveAccessRow(FeatureInfo Feature, AccessLevel Level, IReadOnlyList<string> Places, string Source, bool IsDirect);

/// <summary>
/// Turns stored access (capability codes, access rows) into words for tables
/// and the "what this user can do" panel. The one place those descriptions are built.
/// </summary>
public static class AccessSummary
{
    /// <summary>Label for a place that is the whole organisation.</summary>
    public const string Everywhere = "Everywhere";

    /// <summary>Label for a scope the viewer cannot see.</summary>
    public const string HiddenPlace = "A place outside your scope";

    /// <summary>
    /// Describes a role's capabilities, one label per feature: "Catalogue: Manage".
    /// </summary>
    /// <param name="capabilities">Capability codes.</param>
    /// <returns>The labels, in feature order.</returns>
    public static IReadOnlyList<string> Describe(IEnumerable<string> capabilities)
    {
        var held = capabilities.ToHashSet(StringComparer.Ordinal);
        return Features.All
            .Select(f => (f, Level: f.LevelIn(held)))
            .Where(x => x.Level != AccessLevel.None)
            .Select(x => $"{x.f.Name}: {x.Level}")
            .ToList();
    }

    /// <summary>
    /// Describes a user's direct access rows: "Warehouses: Manage · 2 places".
    /// </summary>
    /// <param name="access">Access rows.</param>
    /// <returns>The labels, in feature order.</returns>
    public static IReadOnlyList<string> Describe(IEnumerable<FeatureAccessDto> access)
    {
        var rows = access.Where(a => a.Level != AccessLevel.None).ToDictionary(a => a.Feature, StringComparer.Ordinal);
        return Features.All
            .Where(f => rows.ContainsKey(f.Code))
            .Select(f =>
            {
                FeatureAccessDto row = rows[f.Code];
                string where = !f.IsScoped || row.Scopes.Any(s => s.ScopeType == ScopeType.Global)
                    ? string.Empty
                    : $" · {row.Scopes.Count} {(row.Scopes.Count == 1 ? "place" : "places")}";
                return $"{f.Name}: {row.Level}{where}";
            })
            .ToList();
    }

    /// <summary>
    /// Works out everything a user can do, one line per feature per source
    /// (each role, and direct access).
    /// </summary>
    /// <param name="user">User's roles, role scopes and direct access.</param>
    /// <param name="roles">All roles the viewer can see.</param>
    /// <param name="scopeNames">Scope text form to label.</param>
    /// <returns>The lines, in feature order, roles before direct access.</returns>
    public static IReadOnlyList<EffectiveAccessRow> Effective(
        IUserFields user,
        IReadOnlyList<RoleDto> roles,
        IReadOnlyDictionary<string, string> scopeNames)
    {
        List<RoleDto> held = roles.Where(r => r.IsActive && user.RoleIds.Contains(r.Id)).ToList();
        IReadOnlyList<string> rolePlaces = Places(user.Scopes, scopeNames);
        var lines = new List<EffectiveAccessRow>();

        foreach (FeatureInfo feature in Features.All)
        {
            foreach (RoleDto role in held)
            {
                AccessLevel level = feature.LevelIn(role.Capabilities);
                if (level != AccessLevel.None)
                {
                    lines.Add(new EffectiveAccessRow(feature, level, feature.IsScoped ? rolePlaces : new[] { Everywhere }, role.Name, IsDirect: false));
                }
            }

            if (user.Access.FirstOrDefault(a => a.Feature == feature.Code && a.Level != AccessLevel.None) is { } direct)
            {
                lines.Add(new EffectiveAccessRow(
                    feature, direct.Level, feature.IsScoped ? Places(direct.Scopes, scopeNames) : new[] { Everywhere }, "Direct", IsDirect: true));
            }
        }

        return lines;
    }

    /// <summary>
    /// Labels a list of scopes.
    /// </summary>
    /// <param name="scopes">Scopes.</param>
    /// <param name="scopeNames">Scope text form to label.</param>
    /// <returns>The labels; empty when there are no scopes.</returns>
    private static IReadOnlyList<string> Places(IEnumerable<ScopeGrantDto> scopes, IReadOnlyDictionary<string, string> scopeNames) =>
        scopes
            .Select(s => s.ScopeType == ScopeType.Global ? Everywhere : scopeNames.GetValueOrDefault(s.ToString(), HiddenPlace))
            .ToList();
}
