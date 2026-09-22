namespace Platform.Shared.Dtos.Auth;

/// <summary>
/// Returned by a successful login: the bearer token and who it belongs to.
/// </summary>
public class LoginResponse
{
    /// <summary>JWT to send as <c>Authorization: Bearer ...</c>.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>UTC instant the token stops being accepted.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Signed-in user's id.</summary>
    public Guid UserId { get; set; }

    /// <summary>Signed-in user's organisation.</summary>
    public Guid OrgId { get; set; }

    /// <summary>Signed-in user's display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Signed-in user's email.</summary>
    public string Email { get; set; } = string.Empty;
}
