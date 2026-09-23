namespace Platform.Shared.Constants;

/// <summary>
/// Route of every API resource. The API uses these in <c>[Route]</c> and the
/// UI uses them to call the API, so a route is renamed in one place.
/// </summary>
public static class ApiRoutes
{
    /// <summary>Sign-in endpoint.</summary>
    public const string Login = "api/auth/login";

    /// <summary>The signed-in caller and their current capabilities.</summary>
    public const string CurrentUser = "api/auth/me";

    /// <summary>Brand master data.</summary>
    public const string Brands = "api/brands";

    /// <summary>Units of measure.</summary>
    public const string Uoms = "api/uoms";

    /// <summary>Catalogue browsing and import (categories, products, SKUs).</summary>
    public const string Catalog = "api/catalog";

    /// <summary>Warehouses (internal only, P1).</summary>
    public const string Warehouses = "api/warehouses";

    /// <summary>Roles and their capabilities.</summary>
    public const string Roles = "api/roles";

    /// <summary>Users, their roles and scopes.</summary>
    public const string Users = "api/users";

    /// <summary>Stock: balances, ledger, documents (receipts, transfers, adjustments), opening stock, reconcile.</summary>
    public const string Stock = "api/stock";

    /// <summary>Audit log.</summary>
    public const string Audit = "api/audit";
}
