using Platform.Shared.Common;

namespace Platform.Shared.Entities.Common;

/// <summary>
/// Root of every persisted entity. Holds the primary key and the four audit
/// fields. These are the ONLY names used for these concepts anywhere in the
/// platform — see <c>.claude/rules/field-names.md</c>.
/// </summary>
/// <remarks>
/// Audit fields are set by <c>AppDbContext.SaveChangesAsync</c>. Application
/// code never assigns them by hand.
/// </remarks>
public abstract class BaseEntity
{
    /// <summary>Primary key, a UUID v7 generated on construction.</summary>
    public Guid Id { get; set; } = IdGenerator.NewId();

    /// <summary>UTC instant the row was inserted.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>User who inserted the row; null for system and seed data.</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>UTC instant of the last change; null until first updated.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>User who last changed the row; null until first updated.</summary>
    public Guid? UpdatedBy { get; set; }
}
