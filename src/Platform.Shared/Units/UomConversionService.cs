using Platform.Shared.Common;
using Platform.Shared.Entities.Catalog;

namespace Platform.Shared.Units;

/// <summary>
/// How a unit behaves in conversions: its dimension and, for standard units,
/// its size relative to the dimension's reference unit.
/// </summary>
/// <param name="Code">Unit code.</param>
/// <param name="Dimension">What it measures.</param>
/// <param name="BaseFactor">Size in reference units; null for packaging units.</param>
public sealed record UnitDefinition(string Code, UomDimension Dimension, decimal? BaseFactor);

/// <summary>
/// The units one SKU is counted and sold in: its base unit plus its own conversions.
/// </summary>
/// <param name="BaseUom">Unit stock is counted in.</param>
/// <param name="Conversions">"1 unit = factor base units" pairs.</param>
public sealed record SkuUnits(string BaseUom, IReadOnlyList<SkuConversion> Conversions);

/// <summary>
/// The single place quantities are converted between units (P7). Pricing,
/// stock, freight, invoicing and the UI all call this; none of them does its
/// own unit arithmetic.
/// </summary>
public interface IUomConversionService
{
    /// <summary>
    /// Converts a quantity of a SKU into another unit.
    /// </summary>
    /// <param name="quantity">Amount and its unit.</param>
    /// <param name="toUom">Target unit code.</param>
    /// <param name="sku">The SKU's base unit and conversions.</param>
    /// <param name="result">The converted quantity, rounded to <see cref="Quantity.Scale"/> places without trailing zeros.</param>
    /// <returns>False when no conversion path exists between the two units for this SKU.</returns>
    bool TryConvert(Quantity quantity, string toUom, SkuUnits sku, out Quantity result);

    /// <summary>
    /// Lists every unit a SKU can be expressed in, with how many base units one of it holds.
    /// </summary>
    /// <param name="sku">The SKU's base unit and conversions.</param>
    /// <returns>Unit code and base units per one, base unit first.</returns>
    IReadOnlyList<(string Uom, decimal BaseUnitsPerOne)> ReachableUnits(SkuUnits sku);
}

/// <summary>
/// Default <see cref="IUomConversionService"/>. A unit reaches the SKU's base unit by:
/// <list type="number">
/// <item>being the base unit;</item>
/// <item>a direct SKU conversion (1 TONNE = 20 BAG);</item>
/// <item>standard factors within one dimension (KG → TONNE), when both sides have them;</item>
/// <item>standard factors to a unit that has a SKU conversion (KG → TONNE → BAG).</item>
/// </list>
/// </summary>
public sealed class UomConversionService : IUomConversionService
{
    private readonly IReadOnlyDictionary<string, UnitDefinition> _units;

    /// <summary>
    /// Creates the service over a set of unit definitions.
    /// </summary>
    /// <param name="units">Every known unit (usually the organisation's units).</param>
    public UomConversionService(IEnumerable<UnitDefinition> units)
    {
        _units = units.ToDictionary(u => u.Code, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public bool TryConvert(Quantity quantity, string toUom, SkuUnits sku, out Quantity result)
    {
        result = default;
        decimal? fromPerOne = BaseUnitsPerOne(quantity.Uom, sku);
        decimal? toPerOne = BaseUnitsPerOne(toUom, sku);
        if (fromPerOne is null || toPerOne is null || toPerOne == 0)
        {
            return false;
        }

        decimal value = quantity.Value * fromPerOne.Value / toPerOne.Value;
        result = new Quantity(Quantity.Normalise(value), Normalise(toUom));
        return true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Order: the base unit, then the SKU's own conversions as listed, then the
    /// remaining standard units from smallest to largest (ties by code), so the
    /// list is the same every time whatever order the units were loaded in.
    /// </remarks>
    public IReadOnlyList<(string Uom, decimal BaseUnitsPerOne)> ReachableUnits(SkuUnits sku)
    {
        List<string> own = new[] { sku.BaseUom }
            .Concat(sku.Conversions.Select(c => c.Uom))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<(string Uom, decimal BaseUnitsPerOne)> reachable = Reach(own, sku);
        reachable.AddRange(Reach(_units.Keys.Except(own, StringComparer.OrdinalIgnoreCase), sku)
            .OrderBy(u => u.BaseUnitsPerOne)
            .ThenBy(u => u.Uom, StringComparer.Ordinal));
        return reachable;
    }

    /// <summary>
    /// Keeps the units that can reach the SKU's base unit, with their factors.
    /// </summary>
    /// <param name="codes">Candidate unit codes.</param>
    /// <param name="sku">The SKU's units.</param>
    /// <returns>Reachable units in the order given.</returns>
    private List<(string Uom, decimal BaseUnitsPerOne)> Reach(IEnumerable<string> codes, SkuUnits sku)
    {
        var reachable = new List<(string Uom, decimal BaseUnitsPerOne)>();
        foreach (string code in codes)
        {
            if (BaseUnitsPerOne(code, sku) is { } perOne)
            {
                reachable.Add((Normalise(code), Quantity.Normalise(perOne)));
            }
        }

        return reachable;
    }

    /// <summary>
    /// Works out how many base units one of <paramref name="uom"/> holds for this SKU.
    /// </summary>
    /// <param name="uom">Unit code.</param>
    /// <param name="sku">The SKU's units.</param>
    /// <returns>The factor, or null when the unit cannot be reached.</returns>
    private decimal? BaseUnitsPerOne(string uom, SkuUnits sku)
    {
        if (uom.Equals(sku.BaseUom, StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        if (sku.Conversions.FirstOrDefault(c => c.Uom.Equals(uom, StringComparison.OrdinalIgnoreCase)) is { } direct)
        {
            return direct.Factor;
        }

        if (!_units.TryGetValue(uom, out var unit) || unit.BaseFactor is not { } unitFactor)
        {
            return null;
        }

        // Same dimension as the base unit, both standard: pure factor arithmetic.
        if (_units.TryGetValue(sku.BaseUom, out var baseUnit)
            && baseUnit.Dimension == unit.Dimension
            && baseUnit.BaseFactor is { } baseFactor)
        {
            return unitFactor / baseFactor;
        }

        // Through a SKU conversion unit in the same dimension (KG → TONNE → BAG).
        foreach (SkuConversion conversion in sku.Conversions)
        {
            if (_units.TryGetValue(conversion.Uom, out var via)
                && via.Dimension == unit.Dimension
                && via.BaseFactor is { } viaFactor)
            {
                return unitFactor / viaFactor * conversion.Factor;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the unit code as defined (canonical casing), or upper-cased when unknown.
    /// </summary>
    /// <param name="uom">Unit code.</param>
    /// <returns>Canonical code.</returns>
    private string Normalise(string uom) => _units.TryGetValue(uom, out var unit) ? unit.Code : uom.ToUpperInvariant();
}
