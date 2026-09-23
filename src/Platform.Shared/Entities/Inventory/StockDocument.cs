using Platform.Shared.Entities.Common;

namespace Platform.Shared.Entities.Inventory;

/// <summary>
/// A document that moves stock: goods receipt, transfer, adjustment or
/// opening stock. Posting it writes ledger entries and balances in one
/// transaction. Once posted it is never edited (P9) — only its status moves
/// on (a transfer is received; any document can be reversed by a new one).
/// </summary>
public class StockDocument : BaseEntity, IOrgScoped
{
    /// <inheritdoc />
    public Guid OrgId { get; set; }

    /// <summary>Human reference, unique per organisation, e.g. <c>GRN-2026-000012</c>.</summary>
    public string ReferenceNo { get; set; } = string.Empty;

    /// <summary>Kind of document.</summary>
    public StockDocumentType Type { get; set; }

    /// <summary>Where it is in its life.</summary>
    public StockDocumentStatus Status { get; set; }

    /// <summary>Why stock moves; the reason on its ledger entries.</summary>
    public StockReason Reason { get; set; }

    /// <summary>Warehouse the stock is received into, adjusted in, or sent from.</summary>
    public Guid WarehouseId { get; set; }

    /// <summary>Destination warehouse; transfers only.</summary>
    public Guid? ToWarehouseId { get; set; }

    /// <summary>
    /// Every warehouse the document touches (source and, for a transfer, destination),
    /// so one query finds the documents in a user's warehouses.
    /// </summary>
    public List<Guid> WarehouseIds { get; set; } = new();

    /// <summary>Supplier's name; receipts only.</summary>
    public string? SupplierName { get; set; }

    /// <summary>Supplier's invoice or delivery challan number; receipts only.</summary>
    public string? SupplierReferenceNo { get; set; }

    /// <summary>Free-text notes; required for adjustments and reversals.</summary>
    public string? Remarks { get; set; }

    /// <summary>What moves, in the units entered and in each SKU's base unit.</summary>
    public List<StockDocumentLine> Lines { get; set; } = new();

    /// <summary>When a transfer arrived.</summary>
    public DateTime? ReceivedAt { get; set; }

    /// <summary>Who received a transfer.</summary>
    public Guid? ReceivedBy { get; set; }

    /// <summary>The document this one reverses, if it is a reversal.</summary>
    public Guid? ReversesDocumentId { get; set; }

    /// <summary>The reversal that cancelled this document, if any.</summary>
    public Guid? ReversedByDocumentId { get; set; }
}

/// <summary>
/// One SKU on a stock document. Stored inside the document.
/// </summary>
public class StockDocumentLine
{
    /// <summary>The SKU.</summary>
    public Guid SkuId { get; set; }

    /// <summary>SKU code at the time of posting.</summary>
    public string SkuCode { get; set; } = string.Empty;

    /// <summary>"Product · variant" at the time of posting.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Amount as entered, in <see cref="Uom"/>. Positive, except on an
    /// adjustment where a negative amount takes stock away.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>Unit the amount was entered in (P7).</summary>
    public string Uom { get; set; } = string.Empty;

    /// <summary>The same amount in the SKU's base unit, as converted by the conversion service.</summary>
    public decimal BaseQuantity { get; set; }

    /// <summary>The SKU's base unit.</summary>
    public string BaseUom { get; set; } = string.Empty;
}
