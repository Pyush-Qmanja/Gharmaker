using Platform.Shared.Entities.Common;

namespace Platform.Shared.Dtos.Common;

/// <summary>
/// Filter for the audit log.
/// </summary>
public class AuditListRequest : PagedRequest
{
    /// <summary>Only this kind of record, e.g. <c>User</c>.</summary>
    public string? Entity { get; set; }

    /// <summary>Only this record.</summary>
    public Guid? EntityId { get; set; }
}

/// <summary>
/// One field of an audited change.
/// </summary>
public class AuditChangeDto
{
    /// <summary>Stored field name.</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>Old value as JSON; null on create.</summary>
    public string? Before { get; set; }

    /// <summary>New value as JSON.</summary>
    public string? After { get; set; }
}

/// <summary>
/// One audit log entry for display.
/// </summary>
public class AuditEntryDto : EntityDto
{
    /// <summary>Kind of record.</summary>
    public string Entity { get; set; } = string.Empty;

    /// <summary>The record's id.</summary>
    public Guid EntityId { get; set; }

    /// <summary>What happened.</summary>
    public AuditAction Action { get; set; }

    /// <summary>Label of the record at the time.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Fields that changed.</summary>
    public List<AuditChangeDto> Changes { get; set; } = new();

    /// <summary>Caller's IP address.</summary>
    public string? Ip { get; set; }

    /// <summary>Caller's device.</summary>
    public string? Device { get; set; }

    /// <summary>Name of the person who made the change.</summary>
    public string? CreatedByName { get; set; }
}
