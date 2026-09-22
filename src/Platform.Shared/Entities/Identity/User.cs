using Platform.Shared.Entities.Common;

namespace Platform.Shared.Entities.Identity;

/// <summary>
/// A person who signs in to the platform. Capabilities and scopes (P6) are
/// attached in Phase 1; this entity carries identity only.
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

    /// <summary>Password hash. Never leaves the API.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <inheritdoc />
    public bool IsActive { get; set; } = true;
}
