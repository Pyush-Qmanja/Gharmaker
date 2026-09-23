using System.Globalization;

namespace Platform.Shared.Common;

/// <summary>
/// An amount together with its unit of measure (P7). No quantity in the
/// platform is ever a bare number; it is always one of these.
/// </summary>
/// <param name="Value">The amount.</param>
/// <param name="Uom">Unit code, e.g. <c>BAG</c>, <c>KG</c>, <c>TONNE</c>.</param>
public readonly record struct Quantity(decimal Value, string Uom)
{
    /// <summary>Decimal places kept for every quantity, matching <c>decimal(18,4)</c>.</summary>
    public const int Scale = 4;

    /// <summary>
    /// Rounds to <see cref="Scale"/> places and drops trailing zeros, so 20.0000
    /// becomes 20 and 266.50 becomes 266.5 wherever the value is shown or sent.
    /// </summary>
    /// <param name="value">Any amount.</param>
    /// <returns>The rounded, trimmed amount.</returns>
    public static decimal Normalise(decimal value) =>
        Math.Round(value, Scale) / 1.000000000000000000000000000000000m;

    /// <summary>
    /// Returns the value rounded to <see cref="Scale"/> places with its unit, e.g. "20 BAG".
    /// </summary>
    /// <returns>The display text.</returns>
    public override string ToString() =>
        $"{Normalise(Value).ToString(CultureInfo.InvariantCulture)} {Uom}";
}
