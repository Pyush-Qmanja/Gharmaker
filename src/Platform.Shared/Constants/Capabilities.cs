namespace Platform.Shared.Constants;

/// <summary>
/// Every capability the platform checks (P6). The API guards endpoints with
/// these codes; the UI builds its access grid from <see cref="Features"/>. A new
/// pair of capabilities is declared here and joined into a feature there.
/// </summary>
/// <remarks>
/// Code format: <c>&lt;resource&gt;.&lt;action&gt;</c>, lower-case. Roles store
/// the codes; renaming a code orphans it in existing roles, so treat codes as
/// permanent once shipped.
/// </remarks>
public static class Capabilities
{
    /// <summary>See brands.</summary>
    public const string BrandsView = "brands.view";

    /// <summary>Create, edit and deactivate brands.</summary>
    public const string BrandsManage = "brands.manage";

    /// <summary>Browse categories, products, SKUs and units.</summary>
    public const string CatalogView = "catalog.view";

    /// <summary>Import and edit categories, products, SKUs and units.</summary>
    public const string CatalogManage = "catalog.manage";

    /// <summary>See warehouses in the user's scope.</summary>
    public const string WarehousesView = "warehouses.view";

    /// <summary>Create, edit and deactivate warehouses (creating needs global scope).</summary>
    public const string WarehousesManage = "warehouses.manage";

    /// <summary>See users.</summary>
    public const string UsersView = "users.view";

    /// <summary>Create, edit and deactivate users, and assign their roles and scopes.</summary>
    public const string UsersManage = "users.manage";

    /// <summary>See roles.</summary>
    public const string RolesView = "roles.view";

    /// <summary>Create, edit and deactivate roles.</summary>
    public const string RolesManage = "roles.manage";

    /// <summary>See stock levels and movement history in the user's warehouses.</summary>
    public const string StockView = "stock.view";

    /// <summary>Post adjustments (damage, expiry, count corrections) and opening stock; reconcile the ledger.</summary>
    public const string StockAdjust = "stock.adjust";

    /// <summary>See goods receipts.</summary>
    public const string ReceiptsView = "receipts.view";

    /// <summary>Post and reverse goods receipts.</summary>
    public const string ReceiptsManage = "receipts.manage";

    /// <summary>See transfers.</summary>
    public const string TransfersView = "transfers.view";

    /// <summary>Send transfers from, and receive them into, the user's warehouses.</summary>
    public const string TransfersManage = "transfers.manage";

    /// <summary>Read the audit log.</summary>
    public const string AuditView = "audit.view";

    /// <summary>
    /// Every capability, in display order: each feature in <see cref="Features.All"/>
    /// contributes its view capability and, if it has one, its manage capability.
    /// </summary>
    public static readonly IReadOnlyList<CapabilityInfo> All = Features.All
        .SelectMany(f => f.ManageCapability is null
            ? new[] { new CapabilityInfo(f.ViewCapability, f.Group, $"View {f.Name.ToLowerInvariant()}") }
            : new[]
            {
                new CapabilityInfo(f.ViewCapability, f.Group, $"View {f.Name.ToLowerInvariant()}"),
                new CapabilityInfo(f.ManageCapability, f.Group, $"Manage {f.Name.ToLowerInvariant()}"),
            })
        .ToList();

    /// <summary>Fast lookup of valid codes.</summary>
    private static readonly HashSet<string> Known = All.Select(c => c.Code).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Checks whether a code is a real capability.
    /// </summary>
    /// <param name="code">Capability code.</param>
    /// <returns>True when the code is in <see cref="All"/>.</returns>
    public static bool IsKnown(string code) => Known.Contains(code);
}

/// <summary>
/// One entry of the capability catalogue.
/// </summary>
/// <param name="Code">Code stored on roles and checked by the API.</param>
/// <param name="Group">Heading the role editor groups it under.</param>
/// <param name="Description">Human description shown next to the checkbox.</param>
public sealed record CapabilityInfo(string Code, string Group, string Description);
