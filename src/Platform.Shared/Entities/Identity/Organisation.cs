using Platform.Shared.Entities.Common;

namespace Platform.Shared.Entities.Identity;

/// <summary>
/// A tenant of the platform. Every business row carries its <c>OrgId</c>.
/// </summary>
public class Organisation : BaseEntity, ISoftDeletable
{
    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    public bool IsActive { get; set; } = true;
}
