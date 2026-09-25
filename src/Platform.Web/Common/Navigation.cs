using Platform.Shared.Constants;

namespace Platform.Web.Common;

/// <summary>
/// The app's screens, defined once. The sidebar, the top-bar breadcrumb and the
/// dashboard modules are all built from <see cref="Items"/>, and each entry is
/// shown only to users holding its capability. A new screen adds one line here.
/// </summary>
public static class Navigation
{
    /// <summary>Controller of the dashboard.</summary>
    public const string HomeController = "Home";

    /// <summary>Every screen, grouped and in menu order.</summary>
    public static readonly IReadOnlyList<NavigationItem> Items = new[]
    {
        new NavigationItem("Products", "Catalog", "Catalogue", Icons.Package, Capabilities.CatalogView,
            "Browse by category and brand, convert units, import from Excel.", $"{ApiRoutes.Catalog}/products"),
        new NavigationItem("Brands", "Brands", "Catalogue", Icons.Tag, Capabilities.BrandsView,
            "Manufacturers' brands used across the catalogue.", ApiRoutes.Brands),
        new NavigationItem("Units", "Uoms", "Catalogue", Icons.Ruler, Capabilities.CatalogView,
            "Units of measure and their standard sizes.", ApiRoutes.Uoms),
        new NavigationItem("Warehouses", "Warehouses", "Inventory", Icons.Warehouse, Capabilities.WarehousesView,
            "Stock locations, their addresses and who runs them.", ApiRoutes.Warehouses),
        new NavigationItem("Stock", "Stock", "Inventory", Icons.Layers, Capabilities.StockView,
            "What is on hand in each warehouse, every movement, and the ledger check.", $"{ApiRoutes.Stock}/balances?inStockOnly=true"),
        new NavigationItem("Goods receipts", "Receipts", "Inventory", Icons.Truck, Capabilities.ReceiptsView,
            "Stock received from suppliers against their invoice or challan.", $"{ApiRoutes.Stock}/documents?type=Receipt"),
        new NavigationItem("Transfers", "Transfers", "Inventory", Icons.Convert, Capabilities.TransfersView,
            "Stock sent between warehouses, in transit until received.", $"{ApiRoutes.Stock}/documents?type=Transfer"),
        new NavigationItem("Adjustments", "Adjustments", "Inventory", Icons.Clipboard, Capabilities.StockView,
            "Damage, expiry and count corrections, each with a reason.", $"{ApiRoutes.Stock}/documents?type=Adjustment"),
        new NavigationItem("Delivery areas", "DeliveryAreas", "Inventory", Icons.MapPin, Capabilities.DeliveryView,
            "PIN codes each warehouse delivers to, and how many days it takes.", ApiRoutes.DeliveryAreas),
        new NavigationItem("Orders", "Orders", "Sales", Icons.Receipt, Capabilities.OrdersView,
            "Orders placed on the online store, waiting for confirmation.", $"{ApiRoutes.Orders}?status=Placed"),
        new NavigationItem("Customers", "Customers", "Sales", Icons.User, Capabilities.CustomersView,
            "Online store accounts, their price tier and access.", ApiRoutes.Customers),
        new NavigationItem("Price lists", "PriceLists", "Sales", Icons.Rupee, Capabilities.PricingView,
            "Retail, tier and contract prices, with quantity slabs.", ApiRoutes.PriceLists),
        new NavigationItem("GST rates", "TaxRates", "Sales", Icons.Percent, Capabilities.PricingView,
            "GST by HSN code. A product without a rate cannot be sold.", ApiRoutes.TaxRates),
        new NavigationItem("Users", "Users", "Administration", Icons.Users, Capabilities.UsersView,
            "People who sign in, their roles and feature access.", ApiRoutes.Users),
        new NavigationItem("Roles", "Roles", "Administration", Icons.Shield, Capabilities.RolesView,
            "Standard sets of access to give to users.", ApiRoutes.Roles),
        new NavigationItem("Audit log", "Audit", "Administration", Icons.History, Capabilities.AuditView,
            "Who changed what, when and from where.", ApiRoutes.Audit),
        new NavigationItem("Business settings", "BusinessSettings", "Administration", Icons.Building, Capabilities.SettingsView,
            "Legal name, GSTIN and registered address used for GST.", CountRoute: null),
    };

    /// <summary>
    /// Finds the screen a controller belongs to, for the breadcrumb and the active menu entry.
    /// </summary>
    /// <param name="controller">MVC controller name.</param>
    /// <returns>The screen, or null (e.g. the dashboard).</returns>
    public static NavigationItem? Find(string? controller) =>
        Items.FirstOrDefault(i => string.Equals(i.Controller, controller, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// One screen in the sidebar and on the dashboard.
/// </summary>
/// <param name="Title">Menu and module title.</param>
/// <param name="Controller">MVC controller name (its Index action is linked).</param>
/// <param name="Group">Sidebar section it sits in.</param>
/// <param name="Icon">Icon name from <see cref="Icons"/>.</param>
/// <param name="Capability">Capability needed to see the entry.</param>
/// <param name="Description">Module text on the dashboard.</param>
/// <param name="CountRoute">Paged API list whose total is shown on the dashboard; null when the screen has nothing to count.</param>
public sealed record NavigationItem(
    string Title,
    string Controller,
    string Group,
    string Icon,
    string Capability,
    string Description,
    string? CountRoute);
