namespace Platform.Shared.Entities.Common;

/// <summary>
/// What a write to the audit log records about a change.
/// </summary>
public enum AuditAction
{
    /// <summary>A record was created.</summary>
    Created = 0,

    /// <summary>A record was changed.</summary>
    Updated = 1,

    /// <summary>A record was deactivated (soft-deleted).</summary>
    Deactivated = 2,

    /// <summary>A deactivated record was made active again.</summary>
    Restored = 3,
}

/// <summary>
/// One change to one record (P10): who, what, before, after, from where, when.
/// Written by <c>UnitOfWork</c> in the same commit as the change itself, so a
/// service can never forget it. <see cref="BaseEntity.CreatedAt"/> is when and
/// <see cref="BaseEntity.CreatedBy"/> is who.
/// </summary>
public class AuditEntry : BaseEntity, IOrgScoped, INotAudited
{
    /// <inheritdoc />
    public Guid OrgId { get; set; }

    /// <summary>Kind of record, e.g. <c>User</c>, <c>StockDocument</c>.</summary>
    public string Entity { get; set; } = string.Empty;

    /// <summary>The record's id.</summary>
    public Guid EntityId { get; set; }

    /// <summary>What happened.</summary>
    public AuditAction Action { get; set; }

    /// <summary>A short label for the record at the time, e.g. its name or reference.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Each field that changed, with its old and new value.</summary>
    public List<AuditChange> Changes { get; set; } = new();

    /// <summary>Caller's IP address.</summary>
    public string? Ip { get; set; }

    /// <summary>Caller's device (browser or app user agent).</summary>
    public string? Device { get; set; }
}

/// <summary>
/// One field of an audited change. Values are stored as JSON text so any
/// shape (numbers, lists, nested objects) can be shown and compared.
/// </summary>
public class AuditChange
{
    /// <summary>Stored field name, e.g. <c>role_ids</c>.</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>Value before, as JSON; null when the record was created.</summary>
    public string? Before { get; set; }

    /// <summary>Value after, as JSON.</summary>
    public string? After { get; set; }
}
