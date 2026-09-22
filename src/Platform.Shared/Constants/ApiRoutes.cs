namespace Platform.Shared.Constants;

/// <summary>
/// Route of every API resource. The API uses these in <c>[Route]</c> and the
/// UI uses them to call the API, so a route is renamed in one place.
/// </summary>
public static class ApiRoutes
{
    /// <summary>Sign-in endpoint.</summary>
    public const string Login = "api/auth/login";

    /// <summary>Brand master data.</summary>
    public const string Brands = "api/brands";
}
