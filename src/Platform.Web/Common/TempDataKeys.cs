namespace Platform.Web.Common;

/// <summary>
/// TempData keys for flash messages, read by <c>Views/Shared/_Alerts.cshtml</c>.
/// </summary>
public static class TempDataKeys
{
    /// <summary>Message shown after a successful action.</summary>
    public const string Success = "Success";

    /// <summary>Message shown after a failed action.</summary>
    public const string Error = "Error";
}
