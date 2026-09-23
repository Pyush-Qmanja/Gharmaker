namespace Platform.Shared.Constants;

/// <summary>
/// Column headings of the catalogue import file (Excel or CSV), one row per
/// SKU. The API reads them, the template writes them and the UI documents
/// them — all from this list.
/// </summary>
public static class CatalogImportColumns
{
    /// <summary>Category path, top level first, separated by "&gt;": <c>Steel &gt; TMT Bars</c>.</summary>
    public const string Category = "Category";

    /// <summary>Brand name; created if it does not exist.</summary>
    public const string Brand = "Brand";

    /// <summary>Product name; rows with the same brand and product become one product.</summary>
    public const string Product = "Product";

    /// <summary>GST HSN code, 4, 6 or 8 digits.</summary>
    public const string Hsn = "HSN";

    /// <summary>Unique SKU code; an existing code is updated, a new one is added.</summary>
    public const string SkuCode = "SKU Code";

    /// <summary>What distinguishes this SKU, e.g. "12 mm" or "50 kg bag".</summary>
    public const string Variant = "Variant";

    /// <summary>Unit stock is counted in, e.g. BAG, KG, PCS, CFT, BOX, RMT, LTR.</summary>
    public const string BaseUnit = "Base Unit";

    /// <summary>Other units: <c>TONNE=20; TRUCK=400</c> means 1 TONNE = 20 base units.</summary>
    public const string Conversions = "Conversions";

    /// <summary>Yes/No; blank means Yes.</summary>
    public const string Active = "Active";

    /// <summary>Separator between category levels.</summary>
    public const char CategorySeparator = '>';

    /// <summary>Separator between conversions.</summary>
    public const char ConversionSeparator = ';';

    /// <summary>Most data rows accepted in one file.</summary>
    public const int MaxRows = 5000;

    /// <summary>Every column in template order, with whether it must be present and a short help text.</summary>
    public static readonly IReadOnlyList<(string Name, bool Required, string Help)> All = new[]
    {
        (Category, true, "Top level first, levels separated by >, e.g. Steel > TMT Bars. Missing categories are created."),
        (Brand, true, "Brand name. Created if it does not exist."),
        (Product, true, "Product name. Rows with the same brand and product are one product."),
        (Hsn, true, "GST HSN code: 4, 6 or 8 digits."),
        (SkuCode, true, "Unique code for this variant. An existing code is updated."),
        (Variant, true, "What makes this SKU different, e.g. 12 mm, 50 kg bag, 600x600 Ivory."),
        (BaseUnit, true, "Unit stock is counted in: see the Units sheet."),
        (Conversions, false, "Other units, e.g. TONNE=20; TRUCK=400 (1 TONNE = 20 base units)."),
        (Active, false, "Yes or No. Blank means Yes."),
    };
}
