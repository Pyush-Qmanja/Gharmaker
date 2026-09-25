using Platform.Shared.Common;
using Platform.Shared.Entities.Sales;

namespace Platform.Shared.Commerce;

/// <summary>
/// Works out GST the way a tax invoice shows it. A supply within the seller's
/// state carries CGST and SGST (half the rate each); a supply to another state
/// carries IGST at the full rate. Cess is added on top of either. Every amount
/// is rounded to paise per line, and totals are sums of the rounded lines, so
/// the invoice always adds up.
/// </summary>
public static class GstCalculator
{
    /// <summary>
    /// Computes the tax on one line.
    /// </summary>
    /// <param name="taxableAmount">Value before tax (already rounded to paise).</param>
    /// <param name="ratePercent">GST rate in percent, e.g. 28.</param>
    /// <param name="cessPercent">Cess rate in percent, 0 when none.</param>
    /// <param name="isInterState">True when the place of supply is another state.</param>
    /// <returns>The taxable value, each tax and the line total.</returns>
    public static TaxAmounts Compute(decimal taxableAmount, decimal ratePercent, decimal cessPercent, bool isInterState)
    {
        var tax = new TaxAmounts { TaxableAmount = Money.Round(taxableAmount) };
        if (isInterState)
        {
            tax.IgstAmount = Money.Round(tax.TaxableAmount * ratePercent / 100m);
        }
        else
        {
            // Each half is rounded on its own, exactly as it is printed.
            tax.CgstAmount = Money.Round(tax.TaxableAmount * ratePercent / 200m);
            tax.SgstAmount = tax.CgstAmount;
        }

        tax.CessAmount = Money.Round(tax.TaxableAmount * cessPercent / 100m);
        tax.TotalAmount = Money.Round(tax.TaxableAmount + tax.CgstAmount + tax.SgstAmount + tax.IgstAmount + tax.CessAmount);
        return tax;
    }

    /// <summary>
    /// Adds up the tax of several lines.
    /// </summary>
    /// <param name="lines">Lines already computed with <see cref="Compute"/>.</param>
    /// <returns>Column totals.</returns>
    public static TaxAmounts Sum(IEnumerable<TaxAmounts> lines)
    {
        var total = new TaxAmounts();
        foreach (TaxAmounts line in lines)
        {
            total.TaxableAmount += line.TaxableAmount;
            total.CgstAmount += line.CgstAmount;
            total.SgstAmount += line.SgstAmount;
            total.IgstAmount += line.IgstAmount;
            total.CessAmount += line.CessAmount;
            total.TotalAmount += line.TotalAmount;
        }

        // Sums of paise stay paise; rounding here only drops trailing zeros.
        total.TaxableAmount = Money.Round(total.TaxableAmount);
        total.CgstAmount = Money.Round(total.CgstAmount);
        total.SgstAmount = Money.Round(total.SgstAmount);
        total.IgstAmount = Money.Round(total.IgstAmount);
        total.CessAmount = Money.Round(total.CessAmount);
        total.TotalAmount = Money.Round(total.TotalAmount);
        return total;
    }

    /// <summary>
    /// Copies computed amounts onto an object that carries them (an order line).
    /// </summary>
    /// <param name="source">Computed amounts.</param>
    /// <param name="target">Object to fill.</param>
    public static void CopyTo(TaxAmounts source, TaxAmounts target)
    {
        target.TaxableAmount = source.TaxableAmount;
        target.CgstAmount = source.CgstAmount;
        target.SgstAmount = source.SgstAmount;
        target.IgstAmount = source.IgstAmount;
        target.CessAmount = source.CessAmount;
        target.TotalAmount = source.TotalAmount;
    }
}
