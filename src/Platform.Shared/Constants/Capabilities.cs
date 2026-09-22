namespace Platform.Shared.Constants;

/// <summary>
/// Every capability the platform checks (P6). The API guards endpoints with
/// these codes and the UI builds the role editor from <see cref="All"/>, so a
/// new capability is added here once and appears in both.
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

    /// <summary>See users.</summary>
    public const string UsersView = "users.view";

    /// <summary>Create, edit and deactivate users, and assign their roles and scopes.</summary>
    public const string UsersManage = "users.manage";

    /// <summary>See roles.</summary>
    public const string RolesView = "roles.view";

    /// <summary>Create, edit and deactivate roles.</summary>
    public const string RolesManage = "roles.manage";

    /// <summary>The full catalogue, in display order, grouped for the role editor.</summary>
    public static readonly IReadOnlyList<CapabilityInfo> All = new[]
    {
        new CapabilityInfo(BrandsView, "Catalog", "View brands"),
        new CapabilityInfo(BrandsManage, "Catalog", "Manage brands"),
        new CapabilityInfo(UsersView, "Administration", "View users"),
        new CapabilityInfo(UsersManage, "Administration", "Manage users, their roles and scopes"),
        new CapabilityInfo(RolesView, "Administration", "View roles"),
        new CapabilityInfo(RolesManage, "Administration", "Manage roles"),
    };

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
