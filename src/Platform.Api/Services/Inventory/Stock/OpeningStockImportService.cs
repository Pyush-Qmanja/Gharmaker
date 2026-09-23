using System.Globalization;
using ClosedXML.Excel;
using Microsoft.Extensions.Caching.Memory;
using Platform.Api.Common;
using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Repositories;
using Platform.Api.Security.Authorization;
using Platform.Api.Services.Catalog;
using Platform.Api.Services.Catalog.Import;
using Platform.Shared.Common;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Catalog;
using Platform.Shared.Dtos.Inventory;
using Platform.Shared.Entities.Catalog;
using Platform.Shared.Entities.Identity;
using Platform.Shared.Entities.Inventory;
using Platform.Shared.Units;

namespace Platform.Api.Services.Inventory.Stock;

/// <summary>
/// Loads stock on hand at go-live from an Excel or CSV file: upload and check
/// (nothing saved), then confirm. Confirming posts one opening-stock document
/// per warehouse (split every <see cref="FieldLengths.StockLines"/> lines), each
/// through the normal posting path, so opening stock is in the ledger like any
/// other movement (reason <c>OpeningBalance</c>).
/// </summary>
public interface IOpeningStockImportService
{
    /// <summary>
    /// Checks a file and keeps it for 30 minutes for confirmation.
    /// </summary>
    /// <param name="content">File content.</param>
    /// <param name="fileName">File name (.xlsx or .csv).</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>What each row will do.</returns>
    Task<OpeningStockPreviewDto> PreviewAsync(Stream content, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts a previewed file, re-checking it first.
    /// </summary>
    /// <param name="importId">Preview id.</param>
    /// <param name="cancellationToken">Cancels the postings.</param>
    /// <returns>The documents posted.</returns>
    Task<OpeningStockResultDto> CommitAsync(Guid importId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the Excel template: the columns, an example row and instructions.
    /// </summary>
    /// <returns>The .xlsx file.</returns>
    byte[] BuildTemplate();
}

/// <summary>
/// Default <see cref="IOpeningStockImportService"/>.
/// </summary>
public sealed class OpeningStockImportService : IOpeningStockImportService
{
    /// <summary>How long a checked file waits for confirmation.</summary>
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(30);

    /// <summary>Firestore's limit on values in one "in" filter.</summary>
    private const int InFilterLimit = 30;

    /// <summary>Stored name of <see cref="Sku.Code"/>.</summary>
    private static readonly string SkuCodeField = FirestoreNaming.Field(nameof(Sku.Code));

    private readonly IRepository<Warehouse> _warehouses;
    private readonly IRepository<Sku> _skus;
    private readonly IRepository<Product> _products;
    private readonly IUomConversionProvider _conversions;
    private readonly IPermissionService _permissions;
    private readonly IStockService _stock;
    private readonly IMemoryCache _cache;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="warehouses">Warehouse data access.</param>
    /// <param name="skus">SKU data access.</param>
    /// <param name="products">Product data access, for names.</param>
    /// <param name="conversions">Unit conversion.</param>
    /// <param name="permissions">Caller's scopes.</param>
    /// <param name="stock">Posts the documents.</param>
    /// <param name="cache">Holds checked files until confirmed.</param>
    /// <param name="currentUser">Caller, so only they can confirm their preview.</param>
    public OpeningStockImportService(
        IRepository<Warehouse> warehouses,
        IRepository<Sku> skus,
        IRepository<Product> products,
        IUomConversionProvider conversions,
        IPermissionService permissions,
        IStockService stock,
        IMemoryCache cache,
        ICurrentUser currentUser)
    {
        _warehouses = warehouses;
        _skus = skus;
        _products = products;
        _conversions = conversions;
        _permissions = permissions;
        _stock = stock;
        _cache = cache;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<OpeningStockPreviewDto> PreviewAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ImportFileRow> rows = Read(content, fileName);
        List<CheckedRow> checkedRows = await CheckAsync(rows, cancellationToken);

        Guid importId = IdGenerator.NewId();
        _cache.Set(CacheKey(importId), new CachedFile(_currentUser.OrgId, _currentUser.UserId, fileName, rows), PreviewLifetime);

        List<OpeningStockRowDto> dtos = checkedRows.Select(r => r.Row).ToList();
        int errors = dtos.Count(r => r.Status == ImportRowStatus.Error);
        return new OpeningStockPreviewDto
        {
            ImportId = importId,
            FileName = fileName,
            Rows = dtos,
            ValidRows = dtos.Count - errors,
            ErrorRows = errors,
            Warehouses = checkedRows.Where(r => r.WarehouseId is not null).Select(r => r.Row.WarehouseCode.ToUpperInvariant()).Distinct().Order().ToList(),
            CanCommit = dtos.Count > 0 && errors == 0,
        };
    }

    /// <inheritdoc />
    public async Task<OpeningStockResultDto> CommitAsync(Guid importId, CancellationToken cancellationToken = default)
    {
        if (!_cache.TryGetValue(CacheKey(importId), out CachedFile? cached)
            || cached is null
            || cached.OrgId != _currentUser.OrgId
            || cached.UserId != _currentUser.UserId)
        {
            throw new NotFoundException("Import preview");
        }

        List<CheckedRow> checkedRows = await CheckAsync(cached.Rows, cancellationToken);
        if (checkedRows.Any(r => r.Row.Status == ImportRowStatus.Error))
        {
            throw new BusinessRuleException("The file no longer checks out (something changed since it was uploaded). Upload it again.");
        }

        // Taken out of the cache first, so a double click cannot post it twice.
        _cache.Remove(CacheKey(importId));

        var result = new OpeningStockResultDto();
        foreach (var byWarehouse in checkedRows.GroupBy(r => r.WarehouseId!.Value))
        {
            foreach (CheckedRow[] chunk in byWarehouse.Chunk(FieldLengths.StockLines))
            {
                StockDocument document = await _stock.PostOpeningAsync(
                    byWarehouse.Key, chunk.Select(r => r.Line!).ToList(), $"Opening stock import: {cached.FileName}", cancellationToken);
                result.ReferenceNos.Add(document.ReferenceNo);
                result.LinesPosted += chunk.Length;
            }
        }

        return result;
    }

    /// <inheritdoc />
    public byte[] BuildTemplate()
    {
        using var workbook = new XLWorkbook();
        IXLWorksheet sheet = workbook.AddWorksheet("Opening stock");
        for (int i = 0; i < OpeningStockColumns.All.Count; i++)
        {
            sheet.Cell(1, i + 1).Value = OpeningStockColumns.All[i].Name;
            sheet.Cell(1, i + 1).Style.Font.Bold = true;
        }

        object[][] examples =
        {
            new object[] { "WH-PUNE-01", "CEM-ULT-OPC53-50KG", 120, "BAG" },
            new object[] { "WH-PUNE-01", "TMT-TISCON-12MM", 2.5, "TONNE" },
        };
        for (int r = 0; r < examples.Length; r++)
        {
            for (int c = 0; c < examples[r].Length; c++)
            {
                sheet.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(examples[r][c]);
            }
        }

        sheet.Columns().AdjustToContents();

        IXLWorksheet help = workbook.AddWorksheet("Instructions");
        help.Cell(1, 1).Value = "Column";
        help.Cell(1, 2).Value = "Required";
        help.Cell(1, 3).Value = "What to enter";
        help.Row(1).Style.Font.Bold = true;
        for (int i = 0; i < OpeningStockColumns.All.Count; i++)
        {
            var (name, required, text) = OpeningStockColumns.All[i];
            help.Cell(i + 2, 1).Value = name;
            help.Cell(i + 2, 2).Value = required ? "Yes" : "No";
            help.Cell(i + 2, 3).Value = text;
        }

        help.Cell(OpeningStockColumns.All.Count + 3, 1).Value =
            "One row per SKU per warehouse. Replace the example rows. Opening stock adds to what is already recorded.";
        help.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Reads the file with the opening-stock columns.
    /// </summary>
    /// <param name="content">File content.</param>
    /// <param name="fileName">File name.</param>
    /// <returns>Data rows.</returns>
    private static IReadOnlyList<ImportFileRow> Read(Stream content, string fileName) =>
        ImportFileReader.Read(
            content,
            fileName,
            OpeningStockColumns.All.Where(c => c.Required).Select(c => c.Name).ToList(),
            OpeningStockColumns.MaxRows);

    /// <summary>
    /// Checks every row: the warehouse exists and the caller may post opening
    /// stock there, the SKU exists and is active, the amount is positive, the
    /// unit applies to the SKU, and no SKU appears twice for a warehouse.
    /// </summary>
    /// <param name="rows">File rows.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Each row with its result and, when valid, what to post.</returns>
    private async Task<List<CheckedRow>> CheckAsync(IReadOnlyList<ImportFileRow> rows, CancellationToken cancellationToken)
    {
        Dictionary<string, Warehouse> warehouses = (await _warehouses.ListAsync(_warehouses.Query(), cancellationToken))
            .GroupBy(w => w.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var skus = new Dictionary<string, Sku>(StringComparer.OrdinalIgnoreCase);
        string[] codes = rows.Select(r => r[OpeningStockColumns.SkuCode].ToUpperInvariant()).Where(c => c.Length > 0).Distinct().ToArray();
        foreach (string[] chunk in codes.Chunk(InFilterLimit))
        {
            foreach (Sku sku in await _skus.ListAsync(_skus.Query().WhereIn(SkuCodeField, chunk), cancellationToken))
            {
                skus[sku.Code] = sku;
            }
        }

        IReadOnlyDictionary<Guid, Product> products = (await _products.GetByIdsAsync(skus.Values.Select(s => s.ProductId), cancellationToken)).ToDictionary(p => p.Id);
        IUomConversionService converter = await _conversions.GetAsync(cancellationToken);
        var allowed = new Dictionary<Guid, bool>();
        var seen = new HashSet<(Guid, Guid)>();
        var result = new List<CheckedRow>(rows.Count);

        foreach (ImportFileRow row in rows)
        {
            var dto = new OpeningStockRowDto
            {
                RowNumber = row.RowNumber,
                WarehouseCode = row[OpeningStockColumns.WarehouseCode],
                SkuCode = row[OpeningStockColumns.SkuCode],
                Quantity = row[OpeningStockColumns.Quantity],
                Uom = row[OpeningStockColumns.Unit].ToUpperInvariant(),
                Status = ImportRowStatus.New,
            };

            Warehouse? warehouse = null;
            if (!warehouses.TryGetValue(dto.WarehouseCode, out warehouse) || !warehouse.IsActive)
            {
                dto.Messages.Add($"No active warehouse with code '{dto.WarehouseCode}'.");
                warehouse = null;
            }
            else
            {
                if (!allowed.TryGetValue(warehouse.Id, out bool may))
                {
                    allowed[warehouse.Id] = may = await _permissions.CoversAsync(Capabilities.StockAdjust, ScopeType.Warehouse, warehouse.Id, cancellationToken);
                }

                if (!may)
                {
                    dto.Messages.Add($"You cannot post opening stock in {warehouse.Code}.");
                }
            }

            if (!skus.TryGetValue(dto.SkuCode, out Sku? sku) || !sku.IsActive)
            {
                dto.Messages.Add($"No active SKU with code '{dto.SkuCode}'.");
                sku = null;
            }
            else
            {
                dto.Name = $"{(products.TryGetValue(sku.ProductId, out Product? p) ? p.Name : sku.Code)} · {sku.VariantLabel}";
                dto.BaseUom = sku.BaseUom;
            }

            bool validAmount = decimal.TryParse(dto.Quantity, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount)
                && amount > 0 && decimal.Round(amount, Quantity.Scale) == amount;
            if (!validAmount)
            {
                dto.Messages.Add($"Quantity '{dto.Quantity}' must be a number greater than zero with at most {Quantity.Scale} decimal places.");
            }

            if (sku is not null && validAmount)
            {
                var units = new SkuUnits(sku.BaseUom, sku.Conversions);
                if (converter.TryConvert(new Quantity(amount, dto.Uom), sku.BaseUom, units, out Quantity inBase))
                {
                    dto.BaseQuantity = inBase.Value;
                }
                else
                {
                    dto.Messages.Add($"{sku.Code} cannot be counted in '{dto.Uom}'. Use one of: {string.Join(", ", converter.ReachableUnits(units).Select(u => u.Uom))}.");
                }
            }

            if (warehouse is not null && sku is not null && !seen.Add((warehouse.Id, sku.Id)))
            {
                dto.Messages.Add($"{sku.Code} is listed twice for {warehouse.Code}; put it on one row.");
            }

            bool ok = dto.Messages.Count == 0;
            dto.Status = ok ? ImportRowStatus.New : ImportRowStatus.Error;
            result.Add(new CheckedRow(
                dto,
                warehouse?.Id,
                ok ? new StockLineRequest { SkuCode = sku!.Code, Quantity = amount, Uom = dto.Uom } : null));
        }

        return result;
    }

    /// <summary>
    /// Cache key of a checked file.
    /// </summary>
    /// <param name="importId">Preview id.</param>
    /// <returns>The key.</returns>
    private static string CacheKey(Guid importId) => $"opening-stock:{importId}";

    /// <summary>
    /// A row as checked, and what to post for it when valid.
    /// </summary>
    /// <param name="Row">Result for display.</param>
    /// <param name="WarehouseId">Warehouse, when found.</param>
    /// <param name="Line">Line to post, when the row is valid.</param>
    private sealed record CheckedRow(OpeningStockRowDto Row, Guid? WarehouseId, StockLineRequest? Line);

    /// <summary>
    /// A checked file waiting for confirmation.
    /// </summary>
    /// <param name="OrgId">Uploader's organisation.</param>
    /// <param name="UserId">Uploader.</param>
    /// <param name="FileName">File name.</param>
    /// <param name="Rows">Rows as read.</param>
    private sealed record CachedFile(Guid? OrgId, Guid? UserId, string FileName, IReadOnlyList<ImportFileRow> Rows);
}
