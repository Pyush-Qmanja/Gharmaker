using Platform.Shared.Entities.Common;

namespace Platform.Shared.Entities.Identity;

/// <summary>
/// A person who signs in to the platform. The password lives in Firebase
/// Authentication, never here. Capabilities and scopes (P6) are attached in
/// Phase 1; this entity carries profile and organisation only.
/// </summary>
public class User : BaseEntity, IOrgScoped, ISoftDeletable
{
    /// <inheritdoc />
    public Guid OrgId { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Login email, stored lower-case, unique across the platform.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Mobile number in E.164 format, optional.</summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Id of the matching Firebase Authentication account. Firebase holds the
    /// credentials; this record holds the profile and organisation.
    /// </summary>
    public string AuthUid { get; set; } = string.Empty;

    /// <summary>Roles held; the user's capabilities are the union of their active roles.</summary>
    public List<Guid> RoleIds { get; set; } = new();

    /// <summary>Where the user's capabilities apply (P6). Empty means nowhere.</summary>
    public List<ScopeGrant> Scopes { get; set; } = new();

    /// <inheritdoc />
    public bool IsActive { get; set; } = true;
}
