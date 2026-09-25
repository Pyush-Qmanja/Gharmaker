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

    /// <summary>
    /// Who the token belongs to: <see cref="Actors.Staff"/> (absent on older
    /// tokens) or <see cref="Actors.Customer"/>. Staff endpoints never accept a customer token.
    /// </summary>
    public const string Actor = "actor";
}

/// <summary>
/// Values of the <see cref="ClaimNames.Actor"/> claim.
/// </summary>
public static class Actors
{
    /// <summary>A platform user (staff) signing in to the admin app.</summary>
    public const string Staff = "staff";

    /// <summary>A customer of the online store.</summary>
    public const string Customer = "customer";
}
