using Platform.Shared.Entities.Common;

namespace Platform.Shared.Entities.Identity;

/// <summary>
/// A person who signs in to the platform. The password lives in Firebase
/// Authentication, never here. Access (P6) comes from two places: roles, which
/// apply in <see cref="Scopes"/>, and per-feature <see cref="Access"/>, which
/// carries its own scopes.
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

    /// <summary>Where the user's roles apply (P6). Empty means nowhere.</summary>
    public List<ScopeGrant> Scopes { get; set; } = new();

    /// <summary>
    /// Feature access given straight to the user, on top of their roles, each
    /// with its own scopes. At most one entry per feature; never level None.
    /// </summary>
    public List<FeatureAccess> Access { get; set; } = new();

    /// <inheritdoc />
    public bool IsActive { get; set; } = true;
}
