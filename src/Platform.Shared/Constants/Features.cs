using Platform.Shared.Entities.Identity;

namespace Platform.Shared.Constants;

/// <summary>
/// The platform's features, each a view capability and (usually) a manage
/// capability. The access grid in the role and user editors is built from
/// <see cref="All"/>, and <see cref="Capabilities.All"/> is derived from it, so
/// a new feature is added here once and appears everywhere.
/// </summary>
public static class Features
{
    /// <summary>Catalogue: categories, products, SKUs and units.</summary>
    public const string Catalog = "catalog";

    /// <summary>Brands.</summary>
    public const string Brands = "brands";

    /// <summary>Warehouses (limited by warehouse scope).</summary>
    public const string Warehouses = "warehouses";

    /// <summary>Stock levels, movements, adjustments and opening stock (limited by warehouse scope).</summary>
    public const string Stock = "stock";

    /// <summary>Goods receipts (limited by warehouse scope).</summary>
    public const string Receipts = "receipts";

    /// <summary>Transfers between warehouses (limited by warehouse scope).</summary>
    public const string Transfers = "transfers";

    /// <summary>PIN codes each warehouse delivers to, and how fast (warehouse-scoped).</summary>
    public const string Delivery = "delivery";

    /// <summary>Price lists, prices and GST rates.</summary>
    public const string Pricing = "pricing";

    /// <summary>Customer accounts of the online store.</summary>
    public const string Customers = "customers";

    /// <summary>Orders placed on the online store.</summary>
    public const string Orders = "orders";

    /// <summary>Business details printed on tax documents.</summary>
    public const string Settings = "settings";

    /// <summary>Users, their roles and access.</summary>
    public const string Users = "users";

    /// <summary>Roles.</summary>
    public const string Roles = "roles";

    /// <summary>Audit log (view only).</summary>
    public const string Audit = "audit";

    /// <summary>Every feature, in display order.</summary>
    public static readonly IReadOnlyList<FeatureInfo> All = new[]
    {
        new FeatureInfo(Catalog, "Catalogue", "Catalogue", "Categories, products, SKUs and units; Excel import.",
            Capabilities.CatalogView, Capabilities.CatalogManage, ScopeType: null),
        new FeatureInfo(Brands, "Brands", "Catalogue", "Manufacturers' brands used by products.",
            Capabilities.BrandsView, Capabilities.BrandsManage, ScopeType: null),
        new FeatureInfo(Warehouses, "Warehouses", "Inventory", "Stock locations, addresses and who runs them.",
            Capabilities.WarehousesView, Capabilities.WarehousesManage, ScopeType.Warehouse),
        new FeatureInfo(Stock, "Stock", "Inventory", "Stock levels and movements. Manage: adjustments, opening stock, ledger check.",
            Capabilities.StockView, Capabilities.StockAdjust, ScopeType.Warehouse),
        new FeatureInfo(Receipts, "Goods receipts", "Inventory", "Stock received from suppliers. Manage: post and reverse receipts.",
            Capabilities.ReceiptsView, Capabilities.ReceiptsManage, ScopeType.Warehouse),
        new FeatureInfo(Transfers, "Transfers", "Inventory", "Stock moved between warehouses. Manage: send and receive.",
            Capabilities.TransfersView, Capabilities.TransfersManage, ScopeType.Warehouse),
        new FeatureInfo(Delivery, "Delivery areas", "Inventory", "PIN codes each warehouse delivers to, and in how many days.",
            Capabilities.DeliveryView, Capabilities.DeliveryManage, ScopeType.Warehouse),
        new FeatureInfo(Pricing, "Pricing and GST", "Sales", "Price lists, quantity slabs and GST rates. Manage: set prices and rates.",
            Capabilities.PricingView, Capabilities.PricingManage, ScopeType: null),
        new FeatureInfo(Customers, "Customers", "Sales", "Online store accounts. Manage: change price tier, block or restore.",
            Capabilities.CustomersView, Capabilities.CustomersManage, ScopeType: null),
        new FeatureInfo(Orders, "Orders", "Sales", "Orders placed on the online store. Manage: confirm or cancel an order.",
            Capabilities.OrdersView, Capabilities.OrdersManage, ScopeType: null),
        new FeatureInfo(Users, "Users", "Administration", "People who sign in, their roles and access.",
            Capabilities.UsersView, Capabilities.UsersManage, ScopeType: null),
        new FeatureInfo(Roles, "Roles", "Administration", "Named sets of access to give to users.",
            Capabilities.RolesView, Capabilities.RolesManage, ScopeType: null),
        new FeatureInfo(Audit, "Audit log", "Administration", "Who changed what, when and from where.",
            Capabilities.AuditView, ManageCapability: null, ScopeType: null),
        new FeatureInfo(Settings, "Business settings", "Administration", "Legal name, GSTIN and registered address used for tax.",
            Capabilities.SettingsView, Capabilities.SettingsManage, ScopeType: null),
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
    /// Finds a feature that must exist, e.g. from one of the constants above.
    /// </summary>
    /// <param name="code">Feature code.</param>
    /// <returns>The feature.</returns>
    /// <exception cref="ArgumentException">The code is not a feature.</exception>
    public static FeatureInfo Get(string code) =>
        Find(code) ?? throw new ArgumentException($"Unknown feature '{code}'.", nameof(code));

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
        foreach (FeatureInfo feature in All.Where(f => f.ManageCapability is not null && set.Contains(f.ManageCapability)))
        {
            set.Add(feature.ViewCapability);
        }

        List<string> known = Capabilities.All.Select(c => c.Code).Where(set.Contains).ToList();
        return known.Concat(set.Except(known).Order(StringComparer.Ordinal)).ToList();
    }
}

/// <summary>
/// One feature: what it is, its capabilities, and whether access to it is
/// limited by scope.
/// </summary>
/// <param name="Code">Stored code, e.g. <c>warehouses</c>. Permanent once shipped.</param>
/// <param name="Name">Display name.</param>
/// <param name="Group">Heading the access grid groups it under.</param>
/// <param name="Description">One-line explanation shown in the grid.</param>
/// <param name="ViewCapability">Capability for seeing records.</param>
/// <param name="ManageCapability">Capability for changing records; null for a view-only feature.</param>
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
    string? ManageCapability,
    ScopeType? ScopeType)
{
    /// <summary>True when access is limited to chosen objects (e.g. warehouses).</summary>
    public bool IsScoped => ScopeType.HasValue;

    /// <summary>True when the feature has a manage level.</summary>
    public bool CanManage => ManageCapability is not null;

    /// <summary>
    /// The manage capability of a feature that has one.
    /// </summary>
    /// <exception cref="InvalidOperationException">The feature is view-only.</exception>
    public string RequiredManageCapability =>
        ManageCapability ?? throw new InvalidOperationException($"Feature '{Code}' is view-only.");

    /// <summary>
    /// Capabilities a level grants.
    /// </summary>
    /// <param name="level">Access level.</param>
    /// <returns>None, view, or view and manage (view only for a view-only feature).</returns>
    public IReadOnlyList<string> CapabilitiesFor(AccessLevel level) => level switch
    {
        AccessLevel.View => new[] { ViewCapability },
        AccessLevel.Manage when ManageCapability is not null => new[] { ViewCapability, ManageCapability },
        AccessLevel.Manage => new[] { ViewCapability },
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
        return ManageCapability is not null && held.Contains(ManageCapability) ? AccessLevel.Manage
            : held.Contains(ViewCapability) ? AccessLevel.View
            : AccessLevel.None;
    }
}
