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

    /// <summary>One-line description under a page title (<c>string</c>).</summary>
    public const string Subtitle = "Subtitle";
}
