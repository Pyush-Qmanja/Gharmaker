using System.Globalization;
using Platform.Shared.Common;
using Platform.Shared.Entities.Inventory;

namespace Platform.Web.Extensions;

/// <summary>
/// Words and formats for stock on screen: reasons, statuses, document kinds
/// and quantities. The one place these are spelled out for people.
/// </summary>
public static class StockTextExtensions
{
    /// <summary>
    /// A quantity for display: no trailing zeros, thousands grouped, e.g. 1,066.5.
    /// </summary>
    /// <param name="value">Amount.</param>
    /// <returns>The text.</returns>
    public static string ToQuantityText(this decimal value) =>
        Quantity.Normalise(value).ToString("#,0.####", CultureInfo.InvariantCulture);

    /// <summary>
    /// A signed quantity for movement lists, e.g. +40 or −10.
    /// </summary>
    /// <param name="value">Amount.</param>
    /// <param name="direction">In or out.</param>
    /// <returns>The text with its sign.</returns>
    public static string ToSignedText(this decimal value, StockDirection direction) =>
        (direction == StockDirection.In ? "+" : "−") + value.ToQuantityText();

    /// <summary>
    /// Why stock moved, in words.
    /// </summary>
    /// <param name="reason">Reason code.</param>
    /// <returns>The words.</returns>
    public static string ToText(this StockReason reason) => reason switch
    {
        StockReason.GrnReceipt => "Goods receipt",
        StockReason.TransferOut => "Transfer out",
        StockReason.TransferIn => "Transfer in",
        StockReason.SaleDispatch => "Sale dispatch",
        StockReason.SaleReturn => "Sale return",
        StockReason.Damage => "Damage",
        StockReason.ExpiryWriteOff => "Expiry write-off",
        StockReason.AuditCorrection => "Count correction",
        StockReason.SiteIssue => "Issued to site",
        StockReason.OpeningBalance => "Opening stock",
        _ => reason.ToString(),
    };

    /// <summary>
    /// A document's status in words.
    /// </summary>
    /// <param name="status">Status.</param>
    /// <returns>The words.</returns>
    public static string ToText(this StockDocumentStatus status) => status switch
    {
        StockDocumentStatus.InTransit => "In transit",
        _ => status.ToString(),
    };

    /// <summary>
    /// The status badge modifier for a document status.
    /// </summary>
    /// <param name="status">Status.</param>
    /// <returns>A <c>status-badge--*</c> modifier.</returns>
    public static string ToBadge(this StockDocumentStatus status) => status switch
    {
        StockDocumentStatus.InTransit => "update",
        StockDocumentStatus.Reversed => "inactive",
        _ => "active",
    };

    /// <summary>
    /// A kind of document in words.
    /// </summary>
    /// <param name="type">Document type.</param>
    /// <returns>The words.</returns>
    public static string ToText(this StockDocumentType type) => type switch
    {
        StockDocumentType.Receipt => "Goods receipt",
        StockDocumentType.Transfer => "Transfer",
        StockDocumentType.Adjustment => "Adjustment",
        StockDocumentType.Opening => "Opening stock",
        _ => type.ToString(),
    };

    /// <summary>
    /// The screen (controller) that shows a kind of document.
    /// </summary>
    /// <param name="type">Document type.</param>
    /// <returns>Controller name.</returns>
    public static string ToController(this StockDocumentType type) => type switch
    {
        StockDocumentType.Receipt => "Receipts",
        StockDocumentType.Transfer => "Transfers",
        _ => "Adjustments",
    };
}
