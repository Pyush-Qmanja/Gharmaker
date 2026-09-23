using Platform.Shared.Entities.Identity;

namespace Platform.Shared.Constants;

/// <summary>
/// The platform's features, each a pair of capabilities (view and manage).
/// The access grid in the role and user editors is built from <see cref="All"/>,
/// and <see cref="Capabilities.All"/> is derived from it, so a new feature is
/// added here once and appears everywhere.
/// </summary>
public static class Features
{
    /// <summary>Catalogue: categories, products, SKUs and units.</summary>
    public const string Catalog = "catalog";

    /// <summary>Brands.</summary>
    public const string Brands = "brands";

    /// <summary>Warehouses (limited by warehouse scope).</summary>
    public const string Warehouses = "warehouses";

    /// <summary>Users, their roles and access.</summary>
    public const string Users = "users";

    /// <summary>Roles.</summary>
    public const string Roles = "roles";

    /// <summary>Every feature, in display order.</summary>
    public static readonly IReadOnlyList<FeatureInfo> All = new[]
    {
        new FeatureInfo(Catalog, "Catalogue", "Catalogue", "Categories, products, SKUs and units; Excel import.",
            Capabilities.CatalogView, Capabilities.CatalogManage, ScopeType: null),
        new FeatureInfo(Brands, "Brands", "Catalogue", "Manufacturers' brands used by products.",
            Capabilities.BrandsView, Capabilities.BrandsManage, ScopeType: null),
        new FeatureInfo(Warehouses, "Warehouses", "Inventory", "Stock locations, addresses and who runs them.",
            Capabilities.WarehousesView, Capabilities.WarehousesManage, ScopeType.Warehouse),
        new FeatureInfo(Users, "Users", "Administration", "People who sign in, their roles and access.",
            Capabilities.UsersView, Capabilities.UsersManage, ScopeType: null),
        new FeatureInfo(Roles, "Roles", "Administration", "Named sets of access to give to users.",
            Capabilities.RolesView, Capabilities.RolesManage, ScopeType: null),
    };

    /// <summary>Lookup by code.</summary>
    private static readonly Dictionary<string, FeatureInfo> ByCode = All.ToDictionary(f => f.Code, StringComparer.Ordinal);

    /// <summary>
    /// Finds a feature by code.
    /// </summary>
    /// <param name="code">Feature code.</param>
    /// <returns>The feature, or null when the code is unknown.</returns>
    public static FeatureInfo? Find(string code) => ByCode.GetValueOrDefault(code);

    /// <summary>
    /// Position of a feature in <see cref="All"/>, for sorting into display order.
    /// </summary>
    /// <param name="code">Feature code.</param>
    /// <returns>The index; unknown codes sort last.</returns>
    public static int OrderOf(string code)
    {
        for (int i = 0; i < All.Count; i++)
        {
            if (All[i].Code == code)
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    /// <summary>
    /// Finds the feature a capability belongs to.
    /// </summary>
    /// <param name="capability">Capability code.</param>
    /// <returns>The feature, or null when no feature uses the code.</returns>
    public static FeatureInfo? OfCapability(string capability) =>
        All.FirstOrDefault(f => f.ViewCapability == capability || f.ManageCapability == capability);

    /// <summary>
    /// Adds the implied capabilities to a set of codes: every <c>manage</c>
    /// capability brings its feature's <c>view</c>. Drops blanks and duplicates.
    /// </summary>
    /// <param name="codes">Capability codes, e.g. from a role form.</param>
    /// <returns>The completed list, in catalogue order.</returns>
    public static List<string> Complete(IEnumerable<string?> codes)
    {
        var set = codes.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!.Trim()).ToHashSet(StringComparer.Ordinal);
        foreach (FeatureInfo feature in All.Where(f => set.Contains(f.ManageCapability)))
        {
            set.Add(feature.ViewCapability);
        }

        List<string> known = Capabilities.All.Select(c => c.Code).Where(set.Contains).ToList();
        return known.Concat(set.Except(known).Order(StringComparer.Ordinal)).ToList();
    }
}

/// <summary>
/// One feature: what it is, its two capabilities, and whether access to it is
/// limited by scope.
/// </summary>
/// <param name="Code">Stored code, e.g. <c>warehouses</c>. Permanent once shipped.</param>
/// <param name="Name">Display name.</param>
/// <param name="Group">Heading the access grid groups it under.</param>
/// <param name="Description">One-line explanation shown in the grid.</param>
/// <param name="ViewCapability">Capability for seeing records.</param>
/// <param name="ManageCapability">Capability for changing records.</param>
/// <param name="ScopeType">
/// Kind of object that limits access (e.g. warehouse), or null when the feature
/// is organisation-wide.
/// </param>
public sealed record FeatureInfo(
    string Code,
    string Name,
    string Group,
    string Description,
    string ViewCapability,
    string ManageCapability,
    ScopeType? ScopeType)
{
    /// <summary>True when access is limited to chosen objects (e.g. warehouses).</summary>
    public bool IsScoped => ScopeType.HasValue;

    /// <summary>
    /// Capabilities a level grants.
    /// </summary>
    /// <param name="level">Access level.</param>
    /// <returns>None, view, or view and manage.</returns>
    public IReadOnlyList<string> CapabilitiesFor(AccessLevel level) => level switch
    {
        AccessLevel.View => new[] { ViewCapability },
        AccessLevel.Manage => new[] { ViewCapability, ManageCapability },
        _ => Array.Empty<string>(),
    };

    /// <summary>
    /// Works out the level a set of capabilities gives for this feature.
    /// </summary>
    /// <param name="capabilities">Capability codes held.</param>
    /// <returns>Manage, view or none.</returns>
    public AccessLevel LevelIn(IEnumerable<string> capabilities)
    {
        var held = capabilities as IReadOnlyCollection<string> ?? capabilities.ToList();
        return held.Contains(ManageCapability) ? AccessLevel.Manage
            : held.Contains(ViewCapability) ? AccessLevel.View
            : AccessLevel.None;
    }
}
