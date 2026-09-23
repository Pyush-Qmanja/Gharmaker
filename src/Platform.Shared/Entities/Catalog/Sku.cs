using Platform.Shared.Entities.Common;

namespace Platform.Shared.Entities.Catalog;

/// <summary>
/// "1 <see cref="Uom"/> = <see cref="Factor"/> of the SKU's base unit", e.g.
/// for cement in bags: 1 TONNE = 20 BAG; for 12 mm TMT in kg: 1 PCS = 10.66 KG.
/// </summary>
public class SkuConversion
{
    /// <summary>The other unit's code.</summary>
    public string Uom { get; set; } = string.Empty;

    /// <summary>How many base units one <see cref="Uom"/> holds. Always greater than zero.</summary>
    public decimal Factor { get; set; }
}

/// <summary>
/// A stock-keeping unit: one sellable variant of a product, e.g. "12 mm" of a
/// TMT bar or "600x600 Glossy Ivory" of a tile. Stock, prices and orders all
/// refer to SKUs, always with a unit (P7).
/// </summary>
public class Sku : BaseEntity, IOrgScoped, ISoftDeletable
{
    /// <inheritdoc />
    public Guid OrgId { get; set; }

    /// <summary>Product this is a variant of.</summary>
    public Guid ProductId { get; set; }

    /// <summary>Unique code, e.g. <c>TMT-TISCON-12MM</c>. The key used by imports.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>What distinguishes this variant, e.g. "12 mm", "50 kg bag".</summary>
    public string VariantLabel { get; set; } = string.Empty;

    /// <summary>Unit stock is counted in for this SKU, e.g. <c>BAG</c> or <c>KG</c>.</summary>
    public string BaseUom { get; set; } = string.Empty;

    /// <summary>Other units this SKU is sold or measured in, relative to <see cref="BaseUom"/>.</summary>
    public List<SkuConversion> Conversions { get; set; } = new();

    /// <inheritdoc />
    public bool IsActive { get; set; } = true;
}
