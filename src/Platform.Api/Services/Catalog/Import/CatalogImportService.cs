using ClosedXML.Excel;
using Microsoft.Extensions.Caching.Memory;
using Platform.Api.Common;
using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Repositories;
using Platform.Shared.Common;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Catalog;
using Platform.Shared.Entities.Catalog;

namespace Platform.Api.Services.Catalog.Import;

/// <summary>
/// Bulk-loads the catalogue from an Excel or CSV file, one row per SKU.
/// Always two steps for people — preview, then commit — so nothing is saved
/// until the user has seen exactly what will change.
/// </summary>
public interface ICatalogImportService
{
    /// <summary>
    /// Reads and checks a file without saving anything.
    /// </summary>
    /// <param name="content">File content.</param>
    /// <param name="fileName">Original file name (.xlsx or .csv).</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Per-row outcome, totals, and an id to commit with.</returns>
    /// <exception cref="FieldValidationException">The file cannot be read or lacks columns.</exception>
    Task<ImportPreviewDto> PreviewAsync(Stream content, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a previewed file. The rows are checked again against current data first.
    /// </summary>
    /// <param name="importId">Id from the preview.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>What was created and updated.</returns>
    /// <exception cref="NotFoundException">Unknown or expired preview, or another user's.</exception>
    /// <exception cref="BusinessRuleException">The rows no longer pass (data changed since the preview).</exception>
    Task<ImportResultDto> CommitAsync(Guid importId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks and saves a file in one step. For system use (starter data), not for people.
    /// </summary>
    /// <param name="content">File content.</param>
    /// <param name="fileName">Original file name.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>What was created and updated.</returns>
    /// <exception cref="BusinessRuleException">Any row has an error.</exception>
    Task<ImportResultDto> ImportAsync(Stream content, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the Excel template: the import sheet with example rows, the valid units, and instructions.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read of units.</param>
    /// <returns>The .xlsx file content.</returns>
    Task<byte[]> BuildTemplateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="ICatalogImportService"/>.
/// </summary>
public sealed class CatalogImportService : ICatalogImportService
{
    /// <summary>How long a preview can be committed.</summary>
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(30);

    /// <summary>Entities staged per save; Firestore allows 500 writes per batch.</summary>
    private const int SaveEvery = 400;

    /// <summary>Firestore limit on values in one "in" filter.</summary>
    private const int InFilterLimit = 30;

    private readonly IRepository<Category> _categories;
    private readonly IRepository<Brand> _brands;
    private readonly IRepository<Product> _products;
    private readonly IRepository<Sku> _skus;
    private readonly IRepository<Uom> _uoms;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemoryCache _cache;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="categories">Category data access.</param>
    /// <param name="brands">Brand data access.</param>
    /// <param name="products">Product data access.</param>
    /// <param name="skus">SKU data access.</param>
    /// <param name="uoms">Unit data access.</param>
    /// <param name="unitOfWork">Commits staged changes.</param>
    /// <param name="cache">Holds previews until they are committed.</param>
    /// <param name="currentUser">Ties a preview to the user and organisation that made it.</param>
    public CatalogImportService(
        IRepository<Category> categories,
        IRepository<Brand> brands,
        IRepository<Product> products,
        IRepository<Sku> skus,
        IRepository<Uom> uoms,
        IUnitOfWork unitOfWork,
        IMemoryCache cache,
        ICurrentUser currentUser)
    {
        _categories = categories;
        _brands = brands;
        _products = products;
        _skus = skus;
        _uoms = uoms;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<ImportPreviewDto> PreviewAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ImportFileRow> rows = ImportFileReader.Read(content, fileName);
        ImportPlan plan = CatalogImportPlanner.Plan(rows, await LoadStateAsync(rows, cancellationToken));

        var importId = IdGenerator.NewId();
        _cache.Set(CacheKey(importId), new CachedImport(_currentUser.OrgId, _currentUser.UserId, fileName, rows), PreviewLifetime);

        return new ImportPreviewDto
        {
            ImportId = importId,
            FileName = fileName,
            Rows = plan.Rows,
            NewSkus = plan.NewSkus.Count,
            UpdatedSkus = plan.UpdatedSkus.Count,
            ErrorRows = plan.Rows.Count(r => r.Status == ImportRowStatus.Error),
            NewProducts = plan.NewProducts.Count,
            NewCategories = plan.NewCategoryPaths,
            NewBrands = plan.NewBrands.Select(b => b.Name).ToList(),
            CanCommit = plan.Rows.Count > 0 && !plan.HasErrors,
        };
    }

    /// <inheritdoc />
    public async Task<ImportResultDto> CommitAsync(Guid importId, CancellationToken cancellationToken = default)
    {
        if (!_cache.TryGetValue(CacheKey(importId), out CachedImport? cached)
            || cached is null
            || cached.OrgId != _currentUser.OrgId
            || cached.UserId != _currentUser.UserId)
        {
            throw new NotFoundException("Import preview");
        }

        ImportResultDto result = await PlanAndApplyAsync(cached.Rows, cancellationToken);
        _cache.Remove(CacheKey(importId));
        return result;
    }

    /// <inheritdoc />
    public Task<ImportResultDto> ImportAsync(Stream content, string fileName, CancellationToken cancellationToken = default) =>
        PlanAndApplyAsync(ImportFileReader.Read(content, fileName), cancellationToken);

    /// <inheritdoc />
    public async Task<byte[]> BuildTemplateAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Uom> units = (await _uoms.ListAsync(_uoms.Query(), cancellationToken))
            .Where(u => u.IsActive)
            .OrderBy(u => u.Dimension).ThenBy(u => u.Code, StringComparer.Ordinal)
            .ToList();

        using var workbook = new XLWorkbook();

        IXLWorksheet sheet = workbook.Worksheets.Add("Catalogue");
        for (int i = 0; i < CatalogImportColumns.All.Count; i++)
        {
            sheet.Cell(1, i + 1).Value = CatalogImportColumns.All[i].Name;
        }

        string[][] examples =
        {
            new[] { "Cement > OPC", "UltraTech", "UltraTech OPC 53 Grade", "2523", "CEM-ULT-OPC53-50KG", "50 kg bag", "BAG", "TONNE=20; TRUCK=400", "Yes" },
            new[] { "Steel > TMT Bars", "Tata Tiscon", "Tata Tiscon 550SD", "7214", "TMT-TISCON-12MM", "12 mm", "KG", "PCS=10.66; BUNDLE=53.3", "Yes" },
            new[] { "Tiles > Vitrified", "Kajaria", "Kajaria Eternity 600x600", "6907", "TILE-KAJ-ETR-IVORY", "600x600 Glossy Ivory", "BOX", "SQFT=0.0645; PCS=0.25", "Yes" },
        };
        for (int r = 0; r < examples.Length; r++)
        {
            for (int c = 0; c < examples[r].Length; c++)
            {
                sheet.Cell(r + 2, c + 1).Value = examples[r][c];
            }
        }

        sheet.Column(4).Style.NumberFormat.Format = "@";
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF4");
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();

        IXLWorksheet unitSheet = workbook.Worksheets.Add("Units");
        unitSheet.Cell(1, 1).Value = "Code";
        unitSheet.Cell(1, 2).Value = "Name";
        unitSheet.Cell(1, 3).Value = "Measures";
        for (int i = 0; i < units.Count; i++)
        {
            unitSheet.Cell(i + 2, 1).Value = units[i].Code;
            unitSheet.Cell(i + 2, 2).Value = units[i].Name;
            unitSheet.Cell(i + 2, 3).Value = units[i].Dimension.ToString();
        }

        unitSheet.Row(1).Style.Font.Bold = true;
        unitSheet.Columns().AdjustToContents();

        IXLWorksheet help = workbook.Worksheets.Add("How to");
        help.Cell(1, 1).Value = "Column";
        help.Cell(1, 2).Value = "Required";
        help.Cell(1, 3).Value = "What to enter";
        for (int i = 0; i < CatalogImportColumns.All.Count; i++)
        {
            var (name, required, text) = CatalogImportColumns.All[i];
            help.Cell(i + 2, 1).Value = name;
            help.Cell(i + 2, 2).Value = required ? "Yes" : "No";
            help.Cell(i + 2, 3).Value = text;
        }

        int note = CatalogImportColumns.All.Count + 3;
        help.Cell(note, 1).Value = "One row per SKU. Replace the example rows with your own. Importing the same SKU Code again updates it.";
        help.Cell(note + 1, 1).Value = $"Up to {CatalogImportColumns.MaxRows} rows per file. Nothing is saved until you confirm the preview.";
        help.Row(1).Style.Font.Bold = true;
        help.Columns(1, 3).AdjustToContents();

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    /// <summary>
    /// Plans against current data and, when every row is valid, saves the changes.
    /// </summary>
    /// <param name="rows">Rows to import.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>What was created and updated.</returns>
    /// <exception cref="BusinessRuleException">Some rows are invalid.</exception>
    private async Task<ImportResultDto> PlanAndApplyAsync(IReadOnlyList<ImportFileRow> rows, CancellationToken cancellationToken)
    {
        ImportPlan plan = CatalogImportPlanner.Plan(rows, await LoadStateAsync(rows, cancellationToken));
        if (plan.HasErrors)
        {
            string first = string.Join(" ", plan.Rows.Where(r => r.Status == ImportRowStatus.Error).Take(3)
                .Select(r => $"Row {r.RowNumber}: {string.Join(" ", r.Messages)}"));
            throw new BusinessRuleException($"The import has errors and nothing was saved. {first}");
        }

        int staged = 0;
        async Task StageAsync(Action stage)
        {
            stage();
            if (++staged >= SaveEvery)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                staged = 0;
            }
        }

        foreach (Category category in plan.NewCategories) await StageAsync(() => _categories.Add(category));
        foreach (Brand brand in plan.NewBrands) await StageAsync(() => _brands.Add(brand));
        foreach (Product product in plan.NewProducts) await StageAsync(() => _products.Add(product));
        foreach (Product product in plan.UpdatedProducts) await StageAsync(() => _products.Update(product));
        foreach (Sku sku in plan.NewSkus) await StageAsync(() => _skus.Add(sku));
        foreach (Sku sku in plan.UpdatedSkus) await StageAsync(() => _skus.Update(sku));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ImportResultDto
        {
            CategoriesCreated = plan.NewCategories.Count,
            BrandsCreated = plan.NewBrands.Count,
            ProductsCreated = plan.NewProducts.Count,
            ProductsUpdated = plan.UpdatedProducts.Count,
            SkusCreated = plan.NewSkus.Count,
            SkusUpdated = plan.UpdatedSkus.Count,
        };
    }

    /// <summary>
    /// Loads the existing data the rows refer to: all categories, brands and
    /// units, plus only the products and SKUs named in the file.
    /// </summary>
    /// <param name="rows">Rows being imported.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The state to plan against.</returns>
    private async Task<ImportState> LoadStateAsync(IReadOnlyList<ImportFileRow> rows, CancellationToken cancellationToken)
    {
        IReadOnlyList<Category> categories = await _categories.ListAsync(_categories.Query(), cancellationToken);
        IReadOnlyList<Brand> brands = await _brands.ListAsync(_brands.Query(), cancellationToken);
        var unitCodes = (await _uoms.ListAsync(_uoms.Query(), cancellationToken))
            .Where(u => u.IsActive)
            .Select(u => u.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string[] codes = rows.Select(r => r[CatalogImportColumns.SkuCode].ToUpperInvariant()).Where(c => c.Length > 0).Distinct().ToArray();
        var skus = new Dictionary<string, Sku>(StringComparer.OrdinalIgnoreCase);
        foreach (string[] chunk in codes.Chunk(InFilterLimit))
        {
            foreach (Sku sku in await _skus.ListAsync(_skus.Query().WhereIn(FirestoreNaming.Field(nameof(Sku.Code)), chunk), cancellationToken))
            {
                skus[sku.Code] = sku;
            }
        }

        var productIds = skus.Values.Select(s => s.ProductId).Distinct().ToList();
        var products = new Dictionary<string, Product>(StringComparer.Ordinal);
        foreach (Product product in await _products.GetByIdsAsync(productIds, cancellationToken))
        {
            products[product.Slug] = product;
        }

        string[] slugs = rows
            .Select(r => CatalogImportPlanner.ProductSlug(Slug.From(r[CatalogImportColumns.Brand]), r[CatalogImportColumns.Product]))
            .Where(s => s.Length > 0 && !products.ContainsKey(s))
            .Distinct()
            .ToArray();
        foreach (string[] chunk in slugs.Chunk(InFilterLimit))
        {
            foreach (Product product in await _products.ListAsync(_products.Query().WhereIn(FirestoreNaming.Field(nameof(Product.Slug)), chunk), cancellationToken))
            {
                products[product.Slug] = product;
            }
        }

        return new ImportState(categories, brands, unitCodes, products, skus);
    }

    /// <summary>
    /// Cache key of a preview.
    /// </summary>
    /// <param name="importId">Preview id.</param>
    /// <returns>The key.</returns>
    private static string CacheKey(Guid importId) => $"catalog-import:{importId}";

    /// <summary>
    /// A preview waiting to be committed.
    /// </summary>
    /// <param name="OrgId">Organisation that made it.</param>
    /// <param name="UserId">User who made it.</param>
    /// <param name="FileName">Uploaded file name.</param>
    /// <param name="Rows">Parsed rows.</param>
    private sealed record CachedImport(Guid? OrgId, Guid? UserId, string FileName, IReadOnlyList<ImportFileRow> Rows);
}
