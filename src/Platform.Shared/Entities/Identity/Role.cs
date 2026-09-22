using Platform.Shared.Entities.Common;

namespace Platform.Shared.Entities.Identity;

/// <summary>
/// A named set of capabilities, defined per organisation. The role's name is
/// only a label for people: code never checks it (P6) — it checks the
/// capabilities the role holds.
/// </summary>
public class Role : BaseEntity, IOrgScoped, ISoftDeletable
{
    /// <inheritdoc />
    public Guid OrgId { get; set; }

    /// <summary>Display name, unique per organisation.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Capability codes from <c>Capabilities</c>.</summary>
    public List<string> Capabilities { get; set; } = new();

    /// <summary>An inactive role grants nothing.</summary>
    public bool IsActive { get; set; } = true;
}
