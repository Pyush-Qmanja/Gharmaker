using System.ComponentModel;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Entities.Identity;

namespace Platform.Shared.Dtos.Identity;

/// <summary>
/// One area of access in a scope list. In HTML forms it travels as
/// text (<c>Global</c>, <c>Warehouse:&lt;id&gt;</c>) via <see cref="ScopeGrantDtoConverter"/>.
/// </summary>
[TypeConverter(typeof(ScopeGrantDtoConverter))]
public class ScopeGrantDto
{
    /// <summary>Kind of object covered.</summary>
    public ScopeType ScopeType { get; set; }

    /// <summary>The object covered; null only for <see cref="ScopeType.Global"/>.</summary>
    public Guid? ScopeId { get; set; }

    /// <summary>
    /// Returns the compact text form, e.g. <c>Warehouse:3f2c...</c>.
    /// </summary>
    /// <returns>The text form.</returns>
    public override string ToString() => ScopeGrantDtoConverter.Format(this);
}

/// <summary>
/// Access to one feature given straight to a user, with its own scopes.
/// </summary>
public class FeatureAccessDto
{
    /// <summary>Feature code from <see cref="Features"/>.</summary>
    public string Feature { get; set; } = string.Empty;

    /// <summary>How much of the feature is allowed; None means no direct access.</summary>
    public AccessLevel Level { get; set; }

    /// <summary>Where the access applies; only for features limited by scope.</summary>
    public List<ScopeGrantDto> Scopes { get; set; } = new();
}

/// <summary>
/// Read model of a user. Never carries credentials — those live in Firebase Authentication.
/// </summary>
public class UserDto : EntityDto
{
    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Login email.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Mobile number in E.164 format, if any.</summary>
    public string? Phone { get; set; }

    /// <summary>Roles held.</summary>
    public List<Guid> RoleIds { get; set; } = new();

    /// <summary>Where the user's roles apply.</summary>
    public List<ScopeGrantDto> Scopes { get; set; } = new();

    /// <summary>Feature access given straight to the user, on top of their roles.</summary>
    public List<FeatureAccessDto> Access { get; set; } = new();

    /// <summary>False once deactivated; an inactive user cannot sign in or call the API.</summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// Editable user fields shared by create and update.
/// </summary>
public interface IUserFields
{
    /// <summary>Display name.</summary>
    string Name { get; }

    /// <summary>Mobile number in E.164 format, optional.</summary>
    string? Phone { get; }

    /// <summary>Roles to hold.</summary>
    List<Guid> RoleIds { get; }

    /// <summary>Where the user's roles apply.</summary>
    List<ScopeGrantDto> Scopes { get; }

    /// <summary>Feature access given straight to the user, each with its own scopes.</summary>
    List<FeatureAccessDto> Access { get; }
}

/// <summary>
/// Canonical form of the access fields, shared by create and update.
/// </summary>
public static class UserFieldsNormaliser
{
    /// <summary>
    /// Drops "no access" rows, keeps one row per feature (in catalogue order),
    /// and clears scopes on organisation-wide features (they have nowhere to be limited to).
    /// </summary>
    /// <param name="access">Access rows as posted.</param>
    /// <returns>The canonical rows.</returns>
    public static List<FeatureAccessDto> Normalise(IEnumerable<FeatureAccessDto> access) =>
        access
            .Where(a => a.Level != AccessLevel.None)
            .GroupBy(a => a.Feature, StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(a => a.Level).First())
            .OrderBy(a => Features.OrderOf(a.Feature))
            .Select(a => new FeatureAccessDto
            {
                Feature = a.Feature,
                Level = a.Level,
                Scopes = Features.Find(a.Feature)?.IsScoped == true
                    ? a.Scopes.DistinctBy(s => (s.ScopeType, s.ScopeId)).ToList()
                    : new List<ScopeGrantDto>(),
            })
            .ToList();
}

/// <summary>
/// Body of <c>POST /api/users</c>. Creates the Firebase Authentication account
/// and the platform user together.
/// </summary>
public class CreateUserRequest : IUserFields, INormalisable
{
    /// <inheritdoc />
    public string Name { get; set; } = string.Empty;

    /// <summary>Login email; unique across the platform. Cannot be changed later.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Initial password, sent straight to Firebase Authentication and never stored by the platform.</summary>
    public string Password { get; set; } = string.Empty;

    /// <inheritdoc />
    public string? Phone { get; set; }

    /// <inheritdoc />
    public List<Guid> RoleIds { get; set; } = new();

    /// <inheritdoc />
    public List<ScopeGrantDto> Scopes { get; set; } = new();

    /// <inheritdoc />
    public List<FeatureAccessDto> Access { get; set; } = new();

    /// <inheritdoc />
    public void Normalise() => Access = UserFieldsNormaliser.Normalise(Access);
}

/// <summary>
/// Body of <c>PUT /api/users/{id}</c>.
/// </summary>
public class UpdateUserRequest : IUserFields, IActivatableRequest, INormalisable
{
    /// <inheritdoc />
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    public string? Phone { get; set; }

    /// <inheritdoc />
    public List<Guid> RoleIds { get; set; } = new();

    /// <inheritdoc />
    public List<ScopeGrantDto> Scopes { get; set; } = new();

    /// <inheritdoc />
    public List<FeatureAccessDto> Access { get; set; } = new();

    /// <summary>False to deactivate (also disables the Firebase account), true to restore.</summary>
    public bool IsActive { get; set; } = true;

    /// <inheritdoc />
    public void Normalise() => Access = UserFieldsNormaliser.Normalise(Access);
}
