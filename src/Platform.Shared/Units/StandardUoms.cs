using Platform.Shared.Entities.Catalog;

namespace Platform.Shared.Units;

/// <summary>
/// The units a construction-materials business starts with. Seeded into every
/// new organisation; organisations may add their own afterwards.
/// </summary>
/// <remarks>
/// Reference units per dimension: PCS (count), KG (mass), CFT (volume), SQFT
/// (area), M (length). Packaging units (BAG, BOX, BUNDLE, TRUCK, CAN, BUCKET)
/// have no fixed size — each SKU says what one holds.
/// </remarks>
public static class StandardUoms
{
    /// <summary>Every standard unit: code, name, dimension, size in reference units.</summary>
    public static readonly IReadOnlyList<(string Code, string Name, UomDimension Dimension, decimal? BaseFactor)> All = new[]
    {
        ("PCS", "Piece", UomDimension.Count, 1m),
        ("THOUSAND", "Thousand pieces", UomDimension.Count, 1000m),
        ("BAG", "Bag", UomDimension.Count, (decimal?)null),
        ("BOX", "Box", UomDimension.Count, null),
        ("BUNDLE", "Bundle", UomDimension.Count, null),
        ("CAN", "Can", UomDimension.Count, null),
        ("BUCKET", "Bucket", UomDimension.Count, null),
        ("TRUCK", "Truck load", UomDimension.Count, null),
        ("KG", "Kilogram", UomDimension.Mass, 1m),
        ("QUINTAL", "Quintal (100 kg)", UomDimension.Mass, 100m),
        ("TONNE", "Tonne", UomDimension.Mass, 1000m),
        ("CFT", "Cubic foot", UomDimension.Volume, 1m),
        ("BRASS", "Brass (100 cft)", UomDimension.Volume, 100m),
        ("CUM", "Cubic metre", UomDimension.Volume, 35.3147m),
        ("LTR", "Litre", UomDimension.Volume, 0.0353147m),
        ("SQFT", "Square foot", UomDimension.Area, 1m),
        ("SQM", "Square metre", UomDimension.Area, 10.7639m),
        ("M", "Metre", UomDimension.Length, 1m),
        ("RMT", "Running metre", UomDimension.Length, 1m),
        ("FT", "Foot", UomDimension.Length, 0.3048m),
    };
}
