namespace Platform.Shared.Common;

/// <summary>
/// The one place money is rounded and its currency named. Amounts are
/// <see cref="decimal"/> everywhere (never <c>double</c>); unit prices keep four
/// decimals, while every amount that reaches a customer or an invoice line is
/// rounded to paise here, half away from zero as GST practice expects.
/// </summary>
public static class Money
{
    /// <summary>Indian rupee, the only currency the platform sells in today (ISO 4217).</summary>
    public const string Inr = "INR";

    /// <summary>Decimals kept on an amount (paise).</summary>
    public const int Scale = 2;

    /// <summary>Decimals kept on a unit price.</summary>
    public const int UnitPriceScale = 4;

    /// <summary>
    /// Rounds an amount to paise.
    /// </summary>
    /// <param name="amount">Unrounded amount.</param>
    /// <returns>The amount with at most two decimals.</returns>
    public static decimal Round(decimal amount) => Normalise(Math.Round(amount, Scale, MidpointRounding.AwayFromZero));

    /// <summary>
    /// Rounds a unit price to its stored precision.
    /// </summary>
    /// <param name="unitPrice">Unrounded price for one unit.</param>
    /// <returns>The price with at most four decimals.</returns>
    public static decimal RoundUnitPrice(decimal unitPrice) =>
        Normalise(Math.Round(unitPrice, UnitPriceScale, MidpointRounding.AwayFromZero));

    /// <summary>
    /// Drops trailing zeros (97500.00 becomes 97500) so the same amount always
    /// looks the same in JSON; display formatting adds the paise back.
    /// </summary>
    /// <param name="value">Amount.</param>
    /// <returns>The same amount without trailing zeros.</returns>
    private static decimal Normalise(decimal value) => value / 1.0000000000000000000000000000m;
}
