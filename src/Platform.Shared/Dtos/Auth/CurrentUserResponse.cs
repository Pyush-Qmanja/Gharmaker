namespace Platform.Shared.Dtos.Auth;

/// <summary>
/// Returned by <c>GET /api/auth/me</c>: who the caller is and what they may do
/// right now. The UI uses it to show only the menus the user can use; the API
/// still enforces every capability itself.
/// </summary>
public class CurrentUserResponse
{
    /// <summary>User id.</summary>
    public Guid UserId { get; set; }

    /// <summary>Organisation id.</summary>
    public Guid OrgId { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Login email.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Capability codes currently granted through active roles.</summary>
    public List<string> Capabilities { get; set; } = new();

    /// <summary>Roles the user holds, so screens offer only the roles below them.</summary>
    public List<Guid> RoleIds { get; set; } = new();
}
