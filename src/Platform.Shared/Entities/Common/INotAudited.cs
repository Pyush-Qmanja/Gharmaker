namespace Platform.Shared.Entities.Common;

/// <summary>
/// Marks an entity whose writes are not copied to the audit log (P10) because
/// the entity is itself a trail or a cache of one — ledger entries, balances,
/// counters and the audit log itself. The document that caused the write
/// (e.g. the goods receipt) is audited instead.
/// </summary>
public interface INotAudited
{
}
