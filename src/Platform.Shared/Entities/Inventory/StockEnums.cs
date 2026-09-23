namespace Platform.Shared.Entities.Inventory;

/// <summary>
/// Why stock moved. Every ledger entry carries one (ledgers.md); never a
/// free-text string. Codes are permanent once shipped.
/// </summary>
public enum StockReason
{
    /// <summary>Received from a supplier against a goods receipt (GRN).</summary>
    GrnReceipt = 0,

    /// <summary>Sent out of a warehouse on a transfer.</summary>
    TransferOut = 1,

    /// <summary>Received into a warehouse from a transfer.</summary>
    TransferIn = 2,

    /// <summary>Dispatched against a customer order (Phase 4).</summary>
    SaleDispatch = 3,

    /// <summary>Returned by a customer (Phase 4).</summary>
    SaleReturn = 4,

    /// <summary>Written off as damaged.</summary>
    Damage = 5,

    /// <summary>Written off as expired.</summary>
    ExpiryWriteOff = 6,

    /// <summary>Corrected after a physical count.</summary>
    AuditCorrection = 7,

    /// <summary>Issued to a construction site (Phase 6).</summary>
    SiteIssue = 8,

    /// <summary>Stock on hand when the platform went live. Migration only.</summary>
    OpeningBalance = 9,
}

/// <summary>
/// Whether a ledger entry adds to or takes from the balance.
/// </summary>
public enum StockDirection
{
    /// <summary>Adds to on hand.</summary>
    In = 0,

    /// <summary>Takes from on hand.</summary>
    Out = 1,
}

/// <summary>
/// Kinds of stock document. Each posts ledger entries; none is edited after posting (P9).
/// </summary>
public enum StockDocumentType
{
    /// <summary>Goods received from a supplier (GRN).</summary>
    Receipt = 0,

    /// <summary>Stock sent from one warehouse to another; received in a second step.</summary>
    Transfer = 1,

    /// <summary>Damage, expiry or count correction.</summary>
    Adjustment = 2,

    /// <summary>Stock on hand at go-live, usually from an Excel import.</summary>
    Opening = 3,
}

/// <summary>
/// Where a stock document is in its life.
/// </summary>
public enum StockDocumentStatus
{
    /// <summary>Posted: its ledger entries are written.</summary>
    Posted = 0,

    /// <summary>A transfer that has left its warehouse but not arrived.</summary>
    InTransit = 1,

    /// <summary>A transfer that has arrived at its destination.</summary>
    Received = 2,

    /// <summary>Cancelled by a reversing document; its effect is undone.</summary>
    Reversed = 3,
}
