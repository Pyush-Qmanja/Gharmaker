using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Platform.Web.Controllers;

/// <summary>
/// Landing page and the error page.
/// </summary>
public sealed class HomeController : PlatformControllerBase
{
    /// <summary>
    /// Shows the dashboard.
    /// </summary>
    /// <returns>The dashboard page.</returns>
    [HttpGet]
    public IActionResult Index() => View();

    /// <summary>
    /// Shows the generic error page with a request id for support.
    /// </summary>
    /// <returns>The error page.</returns>
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        ViewData["RequestId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View();
    }
}
