using Platform.Shared.Constants;
using Platform.Shared.Dtos.Common;

namespace Platform.Shared.Dtos.Identity;

/// <summary>
/// Read model of a role.
/// </summary>
public class RoleDto : EntityDto
{
    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Capability codes the role grants.</summary>
    public List<string> Capabilities { get; set; } = new();

    /// <summary>False once deactivated; an inactive role grants nothing.</summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// Editable role fields shared by create and update.
/// </summary>
public interface IRoleFields
{
    /// <summary>Display name.</summary>
    string Name { get; }

    /// <summary>Capability codes to grant.</summary>
    List<string> Capabilities { get; }
}

/// <summary>
/// Body of <c>POST /api/roles</c>.
/// </summary>
public class CreateRoleRequest : IRoleFields, INormalisable
{
    /// <inheritdoc />
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    public List<string> Capabilities { get; set; } = new();

    /// <summary>
    /// Drops blank entries (the grid's "no access" choice) and adds the view
    /// capability that each manage capability implies.
    /// </summary>
    public void Normalise() => Capabilities = Features.Complete(Capabilities);
}

/// <summary>
/// Body of <c>PUT /api/roles/{id}</c>.
/// </summary>
public class UpdateRoleRequest : IRoleFields, IActivatableRequest, INormalisable
{
    /// <inheritdoc />
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    public List<string> Capabilities { get; set; } = new();

    /// <summary>False to deactivate, true to restore.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Drops blank entries (the grid's "no access" choice) and adds the view
    /// capability that each manage capability implies.
    /// </summary>
    public void Normalise() => Capabilities = Features.Complete(Capabilities);
}
