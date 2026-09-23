using Platform.Shared.Constants;

namespace Platform.Web.Common;

/// <summary>
/// The app's screens, defined once. The top menu and the dashboard tiles are
/// both built from <see cref="Items"/>, and each entry is shown only to users
/// holding its capability. A new screen adds one line here.
/// </summary>
public static class Navigation
{
    /// <summary>Every screen, in menu order.</summary>
    public static readonly IReadOnlyList<NavigationItem> Items = new[]
    {
        new NavigationItem("Brands", "Brands", Capabilities.BrandsView, "Manage the brands in the catalogue."),
        new NavigationItem("Warehouses", "Warehouses", Capabilities.WarehousesView, "Stock locations you are responsible for."),
        new NavigationItem("Users", "Users", Capabilities.UsersView, "People who can sign in, their roles and scope."),
        new NavigationItem("Roles", "Roles", Capabilities.RolesView, "Named sets of capabilities to give to users."),
    };
}

/// <summary>
/// One screen in the menu and on the dashboard.
/// </summary>
/// <param name="Title">Menu and tile title.</param>
/// <param name="Controller">MVC controller name (its Index action is linked).</param>
/// <param name="Capability">Capability needed to see the entry.</param>
/// <param name="Description">Tile text on the dashboard.</param>
public sealed record NavigationItem(string Title, string Controller, string Capability, string Description);
