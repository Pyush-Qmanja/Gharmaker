namespace Platform.Shared.Constants;

/// <summary>
/// Claim types written into the JWT by the API and read back by the API and
/// the UI. Defined once so issuer and readers can never drift apart.
/// </summary>
public static class ClaimNames
{
    /// <summary>User id (standard JWT subject).</summary>
    public const string UserId = "sub";

    /// <summary>Organisation id of the user.</summary>
    public const string OrgId = "org_id";

    /// <summary>Display name of the user.</summary>
    public const string Name = "name";

    /// <summary>Email of the user.</summary>
    public const string Email = "email";

    /// <summary>
    /// Raw access token, stored only in the UI's encrypted auth cookie so the
    /// UI can forward it to the API.
    /// </summary>
    public const string AccessToken = "access_token";
}
