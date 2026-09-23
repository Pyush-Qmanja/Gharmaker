using Platform.Shared.Entities.Common;

namespace Platform.Shared.Entities.Catalog;

/// <summary>
/// What a unit measures. Units in the same dimension that carry a
/// <see cref="Uom.BaseFactor"/> convert to each other globally (kg ↔ tonne);
/// packaging units (bag, box, bundle) have none and convert only through a
/// SKU's own conversions.
/// </summary>
public enum UomDimension
{
    /// <summary>Things counted one by one or by pack: piece, bag, box, bundle, truck.</summary>
    Count = 0,

    /// <summary>Mass: kilogram, tonne.</summary>
    Mass = 1,

    /// <summary>Volume: cubic foot, cubic metre, litre.</summary>
    Volume = 2,

    /// <summary>Area: square foot, square metre.</summary>
    Area = 3,

    /// <summary>Length: metre, foot, running metre.</summary>
    Length = 4,
}

/// <summary>
/// A unit of measure (P7). Every quantity in the platform names one of these by <see cref="Code"/>.
/// </summary>
public class Uom : BaseEntity, IOrgScoped, ISoftDeletable
{
    /// <inheritdoc />
    public Guid OrgId { get; set; }

    /// <summary>Short unique code used in every quantity, e.g. <c>BAG</c>, <c>KG</c>.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name, e.g. "Bag (50 kg)".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>What the unit measures.</summary>
    public UomDimension Dimension { get; set; }

    /// <summary>
    /// How many of the dimension's reference unit one of this unit is
    /// (reference units: KG, CFT, SQFT, M, and PCS for counts). Null for
    /// packaging units whose size depends on the product (bag, box, bundle).
    /// </summary>
    public decimal? BaseFactor { get; set; }

    /// <inheritdoc />
    public bool IsActive { get; set; } = true;
}
