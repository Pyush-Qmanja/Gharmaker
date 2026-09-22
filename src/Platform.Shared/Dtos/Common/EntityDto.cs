namespace Platform.Shared.Dtos.Common;

/// <summary>
/// Base of every read DTO. Carries the key and the audit fields under exactly
/// the same names as <c>BaseEntity</c>, so a field is called the same thing in
/// the database, the API and the UI.
/// </summary>
public abstract class EntityDto
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>UTC instant the record was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>User who created the record.</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>UTC instant of the last change, if any.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>User who last changed the record, if any.</summary>
    public Guid? UpdatedBy { get; set; }
}
