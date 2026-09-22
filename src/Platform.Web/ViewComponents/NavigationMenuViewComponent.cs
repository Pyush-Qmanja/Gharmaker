using Microsoft.AspNetCore.Mvc;
using Platform.Web.Common;
using Platform.Web.Services.Auth;

namespace Platform.Web.ViewComponents;

/// <summary>
/// Renders the screens from <see cref="Navigation.Items"/> that the current user
/// may open — as menu links (default view) or dashboard tiles (<c>Tiles</c> view).
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
    /// Filters the screens by capability and renders the chosen view.
    /// </summary>
    /// <param name="view"><c>Default</c> for menu links, <c>Tiles</c> for the dashboard.</param>
    /// <returns>The rendered list.</returns>
    public async Task<IViewComponentResult> InvokeAsync(string view = "Default")
    {
        var visible = new List<NavigationItem>();
        foreach (NavigationItem item in Navigation.Items)
        {
            if (await _access.CanAsync(item.Capability))
            {
                visible.Add(item);
            }
        }

        return View(view, visible);
    }
}
