namespace Platform.Web.Common;

/// <summary>
/// ViewData keys for lookup data that controllers load for forms and tables
/// (see <c>CrudController.PrepareViewAsync</c>). One place, so a view and its
/// controller can never disagree on a key.
/// </summary>
public static class ViewDataKeys
{
    /// <summary>Roles as checkbox options (<c>IReadOnlyList&lt;SelectListItem&gt;</c>).</summary>
    public const string RoleOptions = "RoleOptions";

    /// <summary>Role id to name (<c>IReadOnlyDictionary&lt;Guid, string&gt;</c>).</summary>
    public const string RoleNames = "RoleNames";

    /// <summary>Capability catalogue as grouped checkbox options (<c>IReadOnlyList&lt;SelectListItem&gt;</c>).</summary>
    public const string CapabilityOptions = "CapabilityOptions";
}
