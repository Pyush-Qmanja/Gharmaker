using Platform.Shared.Entities.Common;

namespace Platform.Shared.Entities.Pricing;

/// <summary>
/// The GST rate for an HSN code from <see cref="ValidFrom"/> onward (P8). A
/// product takes the rate of the longest HSN code that begins its own (8, then
/// 6, then 4 digits). Rows are only inserted; a rate change is a new row.
/// A product with no rate cannot be sold — tax never defaults to zero.
/// </summary>
public class TaxRate : BaseEntity, IOrgScoped
{
    /// <inheritdoc />
    public Guid OrgId { get; set; }

    /// <summary>HSN code (4, 6 or 8 digits) the rate is for.</summary>
    public string HsnCode { get; set; } = string.Empty;

    /// <summary>Total GST rate in percent (split equally into CGST and SGST within a state).</summary>
    public decimal RatePercent { get; set; }

    /// <summary>Compensation cess in percent, 0 when none.</summary>
    public decimal CessPercent { get; set; }

    /// <summary>UTC instant from which the rate applies.</summary>
    public DateTime ValidFrom { get; set; }

    /// <summary>Source of the rate, e.g. the notification number.</summary>
    public string? Remarks { get; set; }
}
