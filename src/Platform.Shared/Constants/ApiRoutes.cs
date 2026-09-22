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

    /// <summary>Roles and their capabilities.</summary>
    public const string Roles = "api/roles";

    /// <summary>Users, their roles and scopes.</summary>
    public const string Users = "api/users";
}
