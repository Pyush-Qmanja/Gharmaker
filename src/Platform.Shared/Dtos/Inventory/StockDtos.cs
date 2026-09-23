using Platform.Shared.Dtos.Common;
using Platform.Shared.Entities.Inventory;

namespace Platform.Shared.Dtos.Inventory;

/// <summary>
/// A warehouse the caller can work with in stock screens, and what they may do there.
/// </summary>
public class StockWarehouseDto
{
    /// <summary>Warehouse id.</summary>
    public Guid Id { get; set; }

    /// <summary>Warehouse code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Warehouse name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>May see stock here.</summary>
    public bool CanView { get; set; }

    /// <summary>May post goods receipts here.</summary>
    public bool CanReceive { get; set; }

    /// <summary>May send transfers from, and receive them into, here.</summary>
    public bool CanTransfer { get; set; }

    /// <summary>May post adjustments and opening stock here.</summary>
    public bool CanAdjust { get; set; }
}

/// <summary>
/// A SKU offered in the line pickers of stock forms.
/// </summary>
public class StockSkuOptionDto
{
    /// <summary>SKU id.</summary>
    public Guid SkuId { get; set; }

    /// <summary>SKU code (what is typed on a line).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>"Product · variant".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Base unit.</summary>
    public string BaseUom { get; set; } = string.Empty;

    /// <summary>Every unit the SKU can be entered in, base unit first.</summary>
    public List<string> Units { get; set; } = new();
}

/// <summary>
/// Filter for the stock list.
/// </summary>
public class StockBalanceRequest : PagedRequest
{
    /// <summary>Only this warehouse; all the caller can see when empty.</summary>
    public Guid? WarehouseId { get; set; }

    /// <summary>True to hide rows with nothing on hand.</summary>
    public bool InStockOnly { get; set; }
}

/// <summary>
/// Stock of one SKU in one warehouse.
/// </summary>
public class StockBalanceDto
{
    /// <summary>Warehouse id.</summary>
    public Guid WarehouseId { get; set; }

    /// <summary>Warehouse code.</summary>
    public string WarehouseCode { get; set; } = string.Empty;

    /// <summary>SKU id.</summary>
    public Guid SkuId { get; set; }

    /// <summary>SKU code.</summary>
    public string SkuCode { get; set; } = string.Empty;

    /// <summary>"Product · variant".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Physically in the warehouse.</summary>
    public decimal OnHand { get; set; }

    /// <summary>Promised to orders, not yet dispatched.</summary>
    public decimal Reserved { get; set; }

    /// <summary>On hand minus reserved.</summary>
    public decimal Available { get; set; }

    /// <summary>Unit of the three quantities (the SKU's base unit).</summary>
    public string Uom { get; set; } = string.Empty;

    /// <summary>Last movement.</summary>
    public DateTime? LastMovedAt { get; set; }
}

/// <summary>
/// Filter for the movement history.
/// </summary>
public class StockLedgerRequest : PagedRequest
{
    /// <summary>Only this warehouse.</summary>
    public Guid? WarehouseId { get; set; }

    /// <summary>Only this SKU.</summary>
    public Guid? SkuId { get; set; }
}

/// <summary>
/// One ledger entry for display.
/// </summary>
public class StockLedgerEntryDto : EntityDto
{
    /// <summary>Warehouse id.</summary>
    public Guid WarehouseId { get; set; }

    /// <summary>Warehouse code.</summary>
    public string WarehouseCode { get; set; } = string.Empty;

    /// <summary>SKU id.</summary>
    public Guid SkuId { get; set; }

    /// <summary>SKU code.</summary>
    public string SkuCode { get; set; } = string.Empty;

    /// <summary>Amount moved (positive).</summary>
    public decimal Quantity { get; set; }

    /// <summary>Unit of the amount.</summary>
    public string Uom { get; set; } = string.Empty;

    /// <summary>In or out.</summary>
    public StockDirection Direction { get; set; }

    /// <summary>Why it moved.</summary>
    public StockReason Reason { get; set; }

    /// <summary>Source document id.</summary>
    public Guid RefId { get; set; }

    /// <summary>Source document reference.</summary>
    public string ReferenceNo { get; set; } = string.Empty;

    /// <summary>The entry this one reverses, if any.</summary>
    public Guid? ReversesEntryId { get; set; }

    /// <summary>On hand right after this entry.</summary>
    public decimal BalanceAfter { get; set; }

    /// <summary>Name of the person who moved it.</summary>
    public string? CreatedByName { get; set; }
}

/// <summary>
/// One line of a stock document request: which SKU, how much, in which unit.
/// </summary>
public class StockLineRequest
{
    /// <summary>SKU code, as on the product page.</summary>
    public string SkuCode { get; set; } = string.Empty;

    /// <summary>Amount. Positive; on an adjustment, negative takes stock away.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Any unit the SKU can be expressed in (e.g. BAG, TONNE, PCS).</summary>
    public string Uom { get; set; } = string.Empty;

    /// <summary>
    /// Canonical lines: drops rows with no SKU code (a form's unused rows) and
    /// trims and upper-cases codes and units.
    /// </summary>
    /// <param name="lines">Lines as posted.</param>
    /// <returns>The lines to validate and post.</returns>
    public static List<StockLineRequest> Normalise(IEnumerable<StockLineRequest>? lines) =>
        (lines ?? Enumerable.Empty<StockLineRequest>())
            .Where(l => !string.IsNullOrWhiteSpace(l.SkuCode))
            .Select(l => new StockLineRequest
            {
                SkuCode = l.SkuCode.Trim().ToUpperInvariant(),
                Quantity = l.Quantity,
                Uom = (l.Uom ?? string.Empty).Trim().ToUpperInvariant(),
            })
            .ToList();
}

/// <summary>
/// Fields every stock document request has.
/// </summary>
public interface IStockDocumentRequest
{
    /// <summary>What moves; blank lines (no SKU code) are ignored.</summary>
    List<StockLineRequest> Lines { get; }

    /// <summary>Free-text notes.</summary>
    string? Remarks { get; }
}

/// <summary>
/// Body of <c>POST /api/stock/receipts</c>: goods received from a supplier.
/// </summary>
public class CreateReceiptRequest : IStockDocumentRequest, INormalisable
{
    /// <inheritdoc />
    public void Normalise() => Lines = StockLineRequest.Normalise(Lines);

    /// <summary>Warehouse receiving the goods.</summary>
    public Guid WarehouseId { get; set; }

    /// <summary>Supplier's name.</summary>
    public string SupplierName { get; set; } = string.Empty;

    /// <summary>Supplier's invoice or delivery challan number.</summary>
    public string? SupplierReferenceNo { get; set; }

    /// <inheritdoc />
    public string? Remarks { get; set; }

    /// <inheritdoc />
    public List<StockLineRequest> Lines { get; set; } = new();
}

/// <summary>
/// Body of <c>POST /api/stock/transfers</c>: send stock to another warehouse.
/// It leaves the source now and arrives when the destination receives it.
/// </summary>
public class CreateTransferRequest : IStockDocumentRequest, INormalisable
{
    /// <inheritdoc />
    public void Normalise() => Lines = StockLineRequest.Normalise(Lines);

    /// <summary>Warehouse sending the stock.</summary>
    public Guid WarehouseId { get; set; }

    /// <summary>Warehouse receiving the stock.</summary>
    public Guid ToWarehouseId { get; set; }

    /// <inheritdoc />
    public string? Remarks { get; set; }

    /// <inheritdoc />
    public List<StockLineRequest> Lines { get; set; } = new();
}

/// <summary>
/// Body of <c>POST /api/stock/adjustments</c>: damage, expiry or count correction.
/// </summary>
public class CreateAdjustmentRequest : IStockDocumentRequest, INormalisable
{
    /// <inheritdoc />
    public void Normalise() => Lines = StockLineRequest.Normalise(Lines);

    /// <summary>Warehouse adjusted.</summary>
    public Guid WarehouseId { get; set; }

    /// <summary><c>Damage</c>, <c>ExpiryWriteOff</c> or <c>AuditCorrection</c>.</summary>
    public StockReason Reason { get; set; } = StockReason.AuditCorrection;

    /// <summary>Why; required.</summary>
    public string? Remarks { get; set; }

    /// <inheritdoc />
    public List<StockLineRequest> Lines { get; set; } = new();
}

/// <summary>
/// Body of <c>POST /api/stock/documents/{id}/reverse</c>.
/// </summary>
public class ReverseStockDocumentRequest
{
    /// <summary>Why the document is being cancelled; required.</summary>
    public string Remarks { get; set; } = string.Empty;
}

/// <summary>
/// Filter for the document list.
/// </summary>
public class StockDocumentListRequest : PagedRequest
{
    /// <summary>Only this kind.</summary>
    public StockDocumentType? Type { get; set; }

    /// <summary>Only documents touching this warehouse (as source or destination).</summary>
    public Guid? WarehouseId { get; set; }
}

/// <summary>
/// One line of a stock document for display.
/// </summary>
public class StockDocumentLineDto
{
    /// <summary>SKU id.</summary>
    public Guid SkuId { get; set; }

    /// <summary>SKU code.</summary>
    public string SkuCode { get; set; } = string.Empty;

    /// <summary>"Product · variant".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Amount as entered.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Unit as entered.</summary>
    public string Uom { get; set; } = string.Empty;

    /// <summary>Amount in the SKU's base unit.</summary>
    public decimal BaseQuantity { get; set; }

    /// <summary>The SKU's base unit.</summary>
    public string BaseUom { get; set; } = string.Empty;
}

/// <summary>
/// A stock document for display.
/// </summary>
public class StockDocumentDto : EntityDto
{
    /// <summary>Human reference.</summary>
    public string ReferenceNo { get; set; } = string.Empty;

    /// <summary>Kind of document.</summary>
    public StockDocumentType Type { get; set; }

    /// <summary>Where it is in its life.</summary>
    public StockDocumentStatus Status { get; set; }

    /// <summary>Why stock moves.</summary>
    public StockReason Reason { get; set; }

    /// <summary>Warehouse (receiving, adjusted, or sending).</summary>
    public Guid WarehouseId { get; set; }

    /// <summary>Warehouse code.</summary>
    public string WarehouseCode { get; set; } = string.Empty;

    /// <summary>Destination warehouse (transfers).</summary>
    public Guid? ToWarehouseId { get; set; }

    /// <summary>Destination warehouse code (transfers).</summary>
    public string? ToWarehouseCode { get; set; }

    /// <summary>Supplier's name (receipts).</summary>
    public string? SupplierName { get; set; }

    /// <summary>Supplier's reference (receipts).</summary>
    public string? SupplierReferenceNo { get; set; }

    /// <summary>Notes.</summary>
    public string? Remarks { get; set; }

    /// <summary>Lines.</summary>
    public List<StockDocumentLineDto> Lines { get; set; } = new();

    /// <summary>When a transfer arrived.</summary>
    public DateTime? ReceivedAt { get; set; }

    /// <summary>Reversal this document cancels.</summary>
    public Guid? ReversesDocumentId { get; set; }

    /// <summary>Reversal that cancelled this document.</summary>
    public Guid? ReversedByDocumentId { get; set; }

    /// <summary>Name of the person who posted it.</summary>
    public string? CreatedByName { get; set; }

    /// <summary>The caller may receive this transfer now.</summary>
    public bool CanReceive { get; set; }

    /// <summary>The caller may reverse this document now.</summary>
    public bool CanReverse { get; set; }
}

/// <summary>
/// Everything about one SKU's stock: where it is, and its latest movements.
/// </summary>
public class SkuStockDto
{
    /// <summary>SKU id.</summary>
    public Guid SkuId { get; set; }

    /// <summary>SKU code.</summary>
    public string SkuCode { get; set; } = string.Empty;

    /// <summary>"Product · variant".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Base unit.</summary>
    public string Uom { get; set; } = string.Empty;

    /// <summary>Total on hand across the warehouses the caller can see.</summary>
    public decimal TotalOnHand { get; set; }

    /// <summary>Per warehouse.</summary>
    public List<StockBalanceDto> Balances { get; set; } = new();
}

/// <summary>
/// One balance that disagrees with its ledger.
/// </summary>
public class StockMismatchDto
{
    /// <summary>Warehouse code.</summary>
    public string WarehouseCode { get; set; } = string.Empty;

    /// <summary>SKU code.</summary>
    public string SkuCode { get; set; } = string.Empty;

    /// <summary>What the balance says.</summary>
    public decimal BalanceOnHand { get; set; }

    /// <summary>What the ledger adds up to (the truth).</summary>
    public decimal LedgerOnHand { get; set; }

    /// <summary>Unit.</summary>
    public string Uom { get; set; } = string.Empty;
}

/// <summary>
/// Result of checking (or rebuilding) every balance against the ledger.
/// </summary>
public class StockReconcileResultDto
{
    /// <summary>Ledger entries read.</summary>
    public int EntriesChecked { get; set; }

    /// <summary>Balances compared.</summary>
    public int BalancesChecked { get; set; }

    /// <summary>Balances that disagree (empty when all is well).</summary>
    public List<StockMismatchDto> Mismatches { get; set; } = new();

    /// <summary>Balances rewritten from the ledger (rebuild only).</summary>
    public int BalancesRebuilt { get; set; }

    /// <summary>When the check ran.</summary>
    public DateTime CheckedAt { get; set; }
}
