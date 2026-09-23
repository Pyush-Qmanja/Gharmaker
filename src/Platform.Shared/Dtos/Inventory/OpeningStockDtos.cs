using Platform.Shared.Dtos.Catalog;

namespace Platform.Shared.Dtos.Inventory;

/// <summary>
/// One row of an opening-stock file as checked.
/// </summary>
public class OpeningStockRowDto
{
    /// <summary>Row number in the file (header is row 1).</summary>
    public int RowNumber { get; set; }

    /// <summary>Warehouse code as written.</summary>
    public string WarehouseCode { get; set; } = string.Empty;

    /// <summary>SKU code as written.</summary>
    public string SkuCode { get; set; } = string.Empty;

    /// <summary>"Product · variant", when the SKU was found.</summary>
    public string? Name { get; set; }

    /// <summary>Amount as written.</summary>
    public string Quantity { get; set; } = string.Empty;

    /// <summary>Unit as written.</summary>
    public string Uom { get; set; } = string.Empty;

    /// <summary>Amount in the SKU's base unit, when valid.</summary>
    public decimal? BaseQuantity { get; set; }

    /// <summary>The SKU's base unit, when found.</summary>
    public string? BaseUom { get; set; }

    /// <summary><c>New</c> when the row will be posted, <c>Error</c> otherwise.</summary>
    public ImportRowStatus Status { get; set; }

    /// <summary>What is wrong with the row.</summary>
    public List<string> Messages { get; set; } = new();
}

/// <summary>
/// Result of checking an opening-stock file. Nothing is saved yet.
/// </summary>
public class OpeningStockPreviewDto
{
    /// <summary>Id to confirm with.</summary>
    public Guid ImportId { get; set; }

    /// <summary>Uploaded file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Every row.</summary>
    public List<OpeningStockRowDto> Rows { get; set; } = new();

    /// <summary>Rows that will be posted.</summary>
    public int ValidRows { get; set; }

    /// <summary>Rows with problems.</summary>
    public int ErrorRows { get; set; }

    /// <summary>Warehouses that will receive opening stock.</summary>
    public List<string> Warehouses { get; set; } = new();

    /// <summary>True when there is something to post and no errors.</summary>
    public bool CanCommit { get; set; }
}

/// <summary>
/// Result of posting an opening-stock file.
/// </summary>
public class OpeningStockResultDto
{
    /// <summary>References of the documents posted (one or more per warehouse).</summary>
    public List<string> ReferenceNos { get; set; } = new();

    /// <summary>Lines posted.</summary>
    public int LinesPosted { get; set; }
}
