namespace Platform.Web.Common;

/// <summary>
/// ViewData keys for lookup data that controllers load for forms and tables
/// (see <c>CrudController.PrepareViewAsync</c>). One place, so a view and its
/// controller can never disagree on a key.
/// </summary>
public static class ViewDataKeys
{
    /// <summary>Roles with what each grants (<c>IReadOnlyList&lt;RoleDto&gt;</c>).</summary>
    public const string Roles = "Roles";

    /// <summary>The role hierarchy as the signed-in user sees it (<c>RoleChart</c>).</summary>
    public const string RoleChart = "RoleChart";

    /// <summary>One-line notice for the sign-in page (<c>string</c>).</summary>
    public const string Notice = "Notice";

    /// <summary>Extra buttons for a list page header (<c>IReadOnlyList&lt;HeaderLink&gt;</c>).</summary>
    public const string HeaderLinks = "HeaderLinks";

    /// <summary>"Reports to" choices for the role form (<c>IReadOnlyList&lt;SelectListItem&gt;</c>).</summary>
    public const string ParentOptions = "ParentOptions";

    /// <summary>True when the role being edited is the built-in Administrator role (<c>bool</c>).</summary>
    public const string IsSystemRole = "IsSystemRole";

    /// <summary>Active roles as checkbox options (<c>IReadOnlyList&lt;SelectListItem&gt;</c>).</summary>
    public const string RoleOptions = "RoleOptions";

    /// <summary>Role id to name (<c>IReadOnlyDictionary&lt;Guid, string&gt;</c>).</summary>
    public const string RoleNames = "RoleNames";

    /// <summary>Scopes the signed-in user can grant, as grouped checkbox options (<c>IReadOnlyList&lt;SelectListItem&gt;</c>).</summary>
    public const string ScopeOptions = "ScopeOptions";

    /// <summary>Scope text form (e.g. <c>Warehouse:&lt;id&gt;</c>) to label (<c>IReadOnlyDictionary&lt;string, string&gt;</c>).</summary>
    public const string ScopeNames = "ScopeNames";

    /// <summary>Active users as drop-down options (<c>IReadOnlyList&lt;SelectListItem&gt;</c>).</summary>
    public const string UserOptions = "UserOptions";

    /// <summary>User id to name (<c>IReadOnlyDictionary&lt;Guid, string&gt;</c>).</summary>
    public const string UserNames = "UserNames";

    /// <summary>Whether the user may create a record on this screen (<c>bool</c>).</summary>
    public const string CanCreate = "CanCreate";

    /// <summary>Warehouses whose stock the user may see (<c>IReadOnlySet&lt;Guid&gt;</c>).</summary>
    public const string StockWarehouseIds = "StockWarehouseIds";

    /// <summary>Warehouses for a filter (<c>IReadOnlyList&lt;SelectListItem&gt;</c>).</summary>
    public const string WarehouseOptions = "WarehouseOptions";

    /// <summary>Warehouses a transfer can go to (<c>IReadOnlyList&lt;SelectListItem&gt;</c>).</summary>
    public const string DestinationOptions = "DestinationOptions";

    /// <summary>Choices for a reason drop-down (<c>IReadOnlyList&lt;SelectListItem&gt;</c>).</summary>
    public const string ReasonOptions = "ReasonOptions";

    /// <summary>One-line description under a page title (<c>string</c>).</summary>
    public const string Subtitle = "Subtitle";
}
