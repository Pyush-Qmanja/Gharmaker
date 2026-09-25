using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Common;
using Platform.Web.Common;
using Platform.Web.Extensions;
using Platform.Web.Models;
using Platform.Web.Services.Api;
using Platform.Web.Services.Auth;

namespace Platform.Web.Controllers;

/// <summary>
/// Dashboard and the error page.
/// </summary>
public sealed class HomeController : PlatformControllerBase
{
    /// <summary>Shortcuts offered on the dashboard, each shown only to users holding its capability.</summary>
    private static readonly IReadOnlyList<QuickAction> AllActions = new[]
    {
        new QuickAction("Receive goods", "Record stock arriving from a supplier.", Icons.Truck, "Receipts", "Create", Capabilities.ReceiptsManage),
        new QuickAction("Send a transfer", "Move stock to another warehouse.", Icons.Convert, "Transfers", "Create", Capabilities.TransfersManage),
        new QuickAction("Adjust stock", "Record damage, expiry or a count correction.", Icons.Clipboard, "Adjustments", "Create", Capabilities.StockAdjust),
        new QuickAction("Import opening stock", "Load stock on hand from an Excel sheet.", Icons.Layers, "Stock", "Opening", Capabilities.StockAdjust),
        new QuickAction("Import catalogue", "Add or update products from an Excel sheet.", Icons.Spreadsheet, "Catalog", "Import", Capabilities.CatalogManage),
        new QuickAction("Add a warehouse", "Register a new stock location.", Icons.Warehouse, "Warehouses", "Create", Capabilities.WarehousesManage),
        new QuickAction("Invite a user", "Create a sign-in and choose what they can do.", Icons.Users, "Users", "Create", Capabilities.UsersManage),
        new QuickAction("Create a role", "Bundle feature access to give to many people.", Icons.Shield, "Roles", "Create", Capabilities.RolesManage),
        new QuickAction("Add a brand", "Add a manufacturer's brand to the catalogue.", Icons.Tag, "Brands", "Create", Capabilities.BrandsManage),
        new QuickAction("Set prices", "Price products in a list, with quantity slabs.", Icons.Rupee, "PriceLists", "Index", Capabilities.PricingManage),
        new QuickAction("Add delivery PIN codes", "Choose where each warehouse delivers.", Icons.MapPin, "DeliveryAreas", "Add", Capabilities.DeliveryManage),
        new QuickAction("Open the online store", "See the store as your customers do.", Icons.Store, "Home", "Index", Capabilities.CatalogView, Area: "Shop"),
    };

    private readonly IApiClient _api;
    private readonly IUserAccess _access;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="api">API client, for the record counts.</param>
    /// <param name="access">Current user's capabilities, to pick modules and shortcuts.</param>
    public HomeController(IApiClient api, IUserAccess access)
    {
        _api = api;
        _access = access;
    }

    /// <summary>
    /// Shows the dashboard: modules the user can open with their record counts,
    /// and the shortcuts they may use.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The dashboard page.</returns>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var visible = new List<NavigationItem>();
        foreach (NavigationItem item in Navigation.Items)
        {
            if (await _access.CanAsync(item.Capability))
            {
                visible.Add(item);
            }
        }

        int?[] counts = await Task.WhenAll(visible.Select(item => CountAsync(item.CountRoute, cancellationToken)));

        var actions = new List<QuickAction>();
        foreach (QuickAction action in AllActions)
        {
            if (await _access.CanAsync(action.Capability))
            {
                actions.Add(action);
            }
        }

        DateTime now = DateTime.UtcNow;
        return View(new DashboardViewModel
        {
            Greeting = now.Greeting(),
            Today = now.ToIst().ToString("dddd, d MMMM yyyy"),
            Modules = visible.Select((item, i) => new DashboardModule(item, counts[i])).ToList(),
            Actions = actions,
        });
    }

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

    /// <summary>
    /// Reads the total of a paged list by asking for its first row only.
    /// </summary>
    /// <param name="route">Paged API list.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The total the user can see, or null when the call fails.</returns>
    private async Task<int?> CountAsync(string? route, CancellationToken cancellationToken)
    {
        if (route is null)
        {
            return null;
        }

        string separator = route.Contains('?') ? "&" : "?";
        var result = await _api.GetAsync<PagedResult<JsonElement>>($"{route}{separator}page=1&pageSize=1", cancellationToken);
        return result.IsSuccess ? result.Value?.TotalCount : null;
    }
}
