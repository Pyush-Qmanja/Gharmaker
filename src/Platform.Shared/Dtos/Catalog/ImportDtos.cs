namespace Platform.Shared.Dtos.Catalog;

/// <summary>
/// What an import row will do.
/// </summary>
public enum ImportRowStatus
{
    /// <summary>Adds a new SKU.</summary>
    New = 0,

    /// <summary>Updates the SKU with the same code.</summary>
    Update = 1,

    /// <summary>Cannot be imported; see the row's messages.</summary>
    Error = 2,
}

/// <summary>
/// One row of an import file, checked but not yet saved.
/// </summary>
public class ImportRowResultDto
{
    /// <summary>Row number in the file (header is row 1).</summary>
    public int RowNumber { get; set; }

    /// <summary>Category path as written, e.g. "Steel &gt; TMT Bars".</summary>
    public string CategoryPath { get; set; } = string.Empty;

    /// <summary>Brand as written.</summary>
    public string Brand { get; set; } = string.Empty;

    /// <summary>Product as written.</summary>
    public string Product { get; set; } = string.Empty;

    /// <summary>SKU code as written.</summary>
    public string SkuCode { get; set; } = string.Empty;

    /// <summary>Variant as written.</summary>
    public string Variant { get; set; } = string.Empty;

    /// <summary>Base unit as written.</summary>
    public string BaseUnit { get; set; } = string.Empty;

    /// <summary>What saving would do.</summary>
    public ImportRowStatus Status { get; set; }

    /// <summary>Problems (for errors) or notes (for updates).</summary>
    public List<string> Messages { get; set; } = new();
}

/// <summary>
/// Result of checking an import file. Nothing is saved until the preview is
/// committed with <see cref="ImportId"/>, and only when <see cref="CanCommit"/>.
/// </summary>
public class ImportPreviewDto
{
    /// <summary>Id to commit this preview with; valid for a limited time.</summary>
    public Guid ImportId { get; set; }

    /// <summary>Uploaded file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Every data row with its outcome.</summary>
    public List<ImportRowResultDto> Rows { get; set; } = new();

    /// <summary>Rows that would add a SKU.</summary>
    public int NewSkus { get; set; }

    /// <summary>Rows that would update a SKU.</summary>
    public int UpdatedSkus { get; set; }

    /// <summary>Rows with errors.</summary>
    public int ErrorRows { get; set; }

    /// <summary>Products that would be created.</summary>
    public int NewProducts { get; set; }

    /// <summary>Category paths that would be created, e.g. "Steel &gt; TMT Bars".</summary>
    public List<string> NewCategories { get; set; } = new();

    /// <summary>Brands that would be created.</summary>
    public List<string> NewBrands { get; set; } = new();

    /// <summary>True when there is at least one row and no errors.</summary>
    public bool CanCommit { get; set; }
}

/// <summary>
/// What a committed import changed.
/// </summary>
public class ImportResultDto
{
    /// <summary>Categories created.</summary>
    public int CategoriesCreated { get; set; }

    /// <summary>Brands created.</summary>
    public int BrandsCreated { get; set; }

    /// <summary>Products created.</summary>
    public int ProductsCreated { get; set; }

    /// <summary>Products updated.</summary>
    public int ProductsUpdated { get; set; }

    /// <summary>SKUs created.</summary>
    public int SkusCreated { get; set; }

    /// <summary>SKUs updated.</summary>
    public int SkusUpdated { get; set; }
}
