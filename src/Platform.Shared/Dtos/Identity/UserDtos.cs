using Platform.Shared.Dtos.Common;
using Platform.Shared.Entities.Identity;

namespace Platform.Shared.Dtos.Identity;

/// <summary>
/// One area of access in a user's scope list.
/// </summary>
public class ScopeGrantDto
{
    /// <summary>Kind of object covered.</summary>
    public ScopeType ScopeType { get; set; }

    /// <summary>The object covered; null only for <see cref="ScopeType.Global"/>.</summary>
    public Guid? ScopeId { get; set; }
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

    /// <summary>Where the user's capabilities apply.</summary>
    public List<ScopeGrantDto> Scopes { get; set; } = new();

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

    /// <summary>Where the user's capabilities apply.</summary>
    List<ScopeGrantDto> Scopes { get; }
}

/// <summary>
/// Body of <c>POST /api/users</c>. Creates the Firebase Authentication account
/// and the platform user together.
/// </summary>
public class CreateUserRequest : IUserFields
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
}

/// <summary>
/// Body of <c>PUT /api/users/{id}</c>.
/// </summary>
public class UpdateUserRequest : IUserFields, IActivatableRequest
{
    /// <inheritdoc />
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    public string? Phone { get; set; }

    /// <inheritdoc />
    public List<Guid> RoleIds { get; set; } = new();

    /// <inheritdoc />
    public List<ScopeGrantDto> Scopes { get; set; } = new();

    /// <summary>False to deactivate (also disables the Firebase account), true to restore.</summary>
    public bool IsActive { get; set; } = true;
}
