namespace Platform.Shared.Constants;

/// <summary>
/// Columns of the opening-stock import file. The template, the importer and
/// the help on the import page all read this list, so they always agree.
/// </summary>
public static class OpeningStockColumns
{
    /// <summary>Warehouse code, e.g. WH-PUNE-01.</summary>
    public const string WarehouseCode = "Warehouse Code";

    /// <summary>SKU code, e.g. TMT-TISCON-12MM.</summary>
    public const string SkuCode = "SKU Code";

    /// <summary>Amount on hand.</summary>
    public const string Quantity = "Quantity";

    /// <summary>Unit of the amount.</summary>
    public const string Unit = "Unit";

    /// <summary>Most rows in one file.</summary>
    public const int MaxRows = 2000;

    /// <summary>Every column, with whether it is required and what to enter.</summary>
    public static readonly IReadOnlyList<(string Name, bool Required, string Help)> All = new[]
    {
        (WarehouseCode, true, "Code of a warehouse where you manage stock, e.g. WH-PUNE-01."),
        (SkuCode, true, "SKU code exactly as in the catalogue."),
        (Quantity, true, "Amount physically on hand, greater than zero. Up to 4 decimal places."),
        (Unit, true, "Any unit the SKU can be counted in (e.g. BAG, TONNE, PCS); converted to its base unit."),
    };
}
