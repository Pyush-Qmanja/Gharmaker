namespace Platform.Shared.Entities.Common;

/// <summary>
/// Marks a master-data entity that is deactivated rather than deleted.
/// Transactions never implement this — they are never deleted at all.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>False once the record has been deactivated.</summary>
    bool IsActive { get; set; }
}
