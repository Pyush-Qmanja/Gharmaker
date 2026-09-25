namespace Platform.Shared.Constants;

/// <summary>
/// Route of every API resource. The API uses these in <c>[Route]</c> and the
/// UI uses them to call the API, so a route is renamed in one place.
/// </summary>
public static class ApiRoutes
{
    /// <summary>Sign-in, the current caller, and signing out everywhere.</summary>
    public const string Auth = "api/auth";

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

    /// <summary>Last segment of "sign out everywhere": <c>api/users/{id}/sessions/end</c>, or <c>api/auth/sessions/end</c> for oneself.</summary>
    public const string EndSessionsSegment = "sessions/end";

    /// <summary>Stock: balances, ledger, documents (receipts, transfers, adjustments), opening stock, reconcile.</summary>
    public const string Stock = "api/stock";

    /// <summary>Audit log.</summary>
    public const string Audit = "api/audit";

    /// <summary>Price lists (retail, tier, contract).</summary>
    public const string PriceLists = "api/price-lists";

    /// <summary>Prices of SKUs in a price list, dated (P8).</summary>
    public const string Prices = "api/prices";

    /// <summary>GST rates by HSN code, dated (P8).</summary>
    public const string TaxRates = "api/tax-rates";

    /// <summary>PIN codes each warehouse delivers to.</summary>
    public const string DeliveryAreas = "api/delivery-areas";

    /// <summary>Customer accounts (staff side).</summary>
    public const string Customers = "api/customers";

    /// <summary>Customer orders (staff side).</summary>
    public const string Orders = "api/orders";

    /// <summary>Business details used on tax documents.</summary>
    public const string BusinessSettings = "api/settings/business";

    /// <summary>Everything customer-facing (P1: no warehouse detail below this route).</summary>
    public const string Storefront = "api/storefront";
}
