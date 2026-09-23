using Platform.Shared.Entities.Common;

namespace Platform.Shared.Entities.Inventory;

/// <summary>
/// One movement of one SKU in one warehouse — the single source of truth for
/// stock (P2). Append-only: never updated or deleted; a mistake is undone by
/// a reversing entry that names this one in <see cref="ReversesEntryId"/>.
/// <see cref="BaseEntity.CreatedAt"/> is when it moved and
/// <see cref="BaseEntity.CreatedBy"/> is who moved it.
/// </summary>
public class StockLedgerEntry : BaseEntity, IOrgScoped, INotAudited
{
    /// <inheritdoc />
    public Guid OrgId { get; set; }

    /// <summary>Where it moved.</summary>
    public Guid WarehouseId { get; set; }

    /// <summary>What moved.</summary>
    public Guid SkuId { get; set; }

    /// <summary>How much moved; always positive, in <see cref="Uom"/>.</summary>
    public decimal Quantity { get; set; }

    /// <summary>The SKU's base unit.</summary>
    public string Uom { get; set; } = string.Empty;

    /// <summary>In or out.</summary>
    public StockDirection Direction { get; set; }

    /// <summary>Why it moved.</summary>
    public StockReason Reason { get; set; }

    /// <summary>Kind of source record, e.g. <c>StockDocument</c>.</summary>
    public string RefType { get; set; } = string.Empty;

    /// <summary>The source record's id.</summary>
    public Guid RefId { get; set; }

    /// <summary>The source record's human reference, for reading the ledger.</summary>
    public string ReferenceNo { get; set; } = string.Empty;

    /// <summary>The entry this one cancels, if it is a reversal.</summary>
    public Guid? ReversesEntryId { get; set; }

    /// <summary>On hand in this warehouse right after this entry, in <see cref="Uom"/>.</summary>
    public decimal BalanceAfter { get; set; }

    /// <summary>
    /// The entry's effect on on-hand: <see cref="Quantity"/> for in, minus it for out.
    /// </summary>
    /// <returns>Signed quantity.</returns>
    public decimal SignedQuantity() => Direction == StockDirection.In ? Quantity : -Quantity;
}
