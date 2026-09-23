using Microsoft.AspNetCore.Mvc;
using Platform.Web.Common;
using Platform.Web.Services.Auth;

namespace Platform.Web.ViewComponents;

/// <summary>
/// Renders the sidebar: the dashboard link plus the screens from
/// <see cref="Navigation.Items"/> that the current user may open, grouped by section.
/// </summary>
public sealed class NavigationMenuViewComponent : ViewComponent
{
    private readonly IUserAccess _access;

    /// <summary>
    /// Creates the component.
    /// </summary>
    /// <param name="access">Current user's capabilities.</param>
    public NavigationMenuViewComponent(IUserAccess access)
    {
        _access = access;
    }

    /// <summary>
    /// Filters the screens by capability and renders the sidebar.
    /// </summary>
    /// <returns>The rendered menu.</returns>
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var visible = new List<NavigationItem>();
        foreach (NavigationItem item in Navigation.Items)
        {
            if (await _access.CanAsync(item.Capability))
            {
                visible.Add(item);
            }
        }

        return View(visible);
    }
}
