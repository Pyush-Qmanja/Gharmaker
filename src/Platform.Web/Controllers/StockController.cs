using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Inventory;
using Platform.Web.Extensions;
using Platform.Web.Models;
using Platform.Web.Services.Api;

namespace Platform.Web.Controllers;

/// <summary>
/// Stock screens: what is on hand (by warehouse and by SKU), every movement,
/// opening-stock import and the ledger check.
/// </summary>
public sealed class StockController : PlatformControllerBase
{
    /// <summary>Largest upload accepted (matches the API limit).</summary>
    private const long MaxImportBytes = 10 * 1024 * 1024;

    private readonly IStockApiClient _stock;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="stock">Stock API client.</param>
    public StockController(IStockApiClient stock)
    {
        _stock = stock;
    }

    /// <summary>
    /// Shows stock on hand, filtered by warehouse and SKU code, with the actions the user may take.
    /// </summary>
    /// <param name="request">Warehouse, search, in-stock filter and paging.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The stock page.</returns>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] StockBalanceRequest request, CancellationToken cancellationToken)
    {
        var balances = await _stock.GetBalancesAsync(request, cancellationToken);
        if (HandleAccess(balances) is { } denied)
        {
            return denied;
        }

        if (!balances.IsSuccess)
        {
            FlashError(balances.ErrorMessage ?? "Could not load stock.");
        }

        List<StockWarehouseDto> warehouses = (await _stock.GetWarehousesAsync(cancellationToken)).Value ?? new List<StockWarehouseDto>();
        return View(new StockIndexViewModel
        {
            WarehouseOptions = WarehouseOptions(warehouses.Where(w => w.CanView), request.WarehouseId),
            WarehouseId = request.WarehouseId,
            Warehouses = warehouses.Where(w => w.CanView).ToList(),
            InStockOnly = request.InStockOnly,
            Can = StockAbilities.From(warehouses),
            Balances = new ListViewModel<StockBalanceDto>(
                "Stock",
                balances.Value ?? new PagedResult<StockBalanceDto> { Page = request.Page, PageSize = request.PageSize },
                request.Search,
                new Dictionary<string, string?>
                {
                    ["warehouseId"] = request.WarehouseId?.ToString(),
                    ["inStockOnly"] = request.InStockOnly ? "true" : null,
                },
                itemName: "item"),
        });
    }

    /// <summary>
    /// Shows one SKU: stock in each warehouse and its movements.
    /// </summary>
    /// <param name="id">SKU id.</param>
    /// <param name="page">Movement page.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The SKU stock page, or 404.</returns>
    [HttpGet]
    public async Task<IActionResult> Sku(Guid id, int page = 1, CancellationToken cancellationToken = default)
    {
        var stock = await _stock.GetSkuStockAsync(id, cancellationToken);
        if (HandleAccess(stock) is { } denied)
        {
            return denied;
        }

        if (!stock.IsSuccess || stock.Value is null)
        {
            return NotFound();
        }

        var request = new StockLedgerRequest { SkuId = id, Page = Math.Max(page, 1) };
        var movements = await _stock.GetLedgerAsync(request, cancellationToken);
        return View(new SkuStockViewModel(
            stock.Value,
            new ListViewModel<StockLedgerEntryDto>(
                "Movements",
                movements.Value ?? new PagedResult<StockLedgerEntryDto> { Page = request.Page, PageSize = request.PageSize },
                search: null,
                new Dictionary<string, string?> { ["id"] = id.ToString() },
                itemName: "movement",
                pageAction: nameof(Sku))));
    }

    /// <summary>
    /// Shows every movement, newest first, optionally for one warehouse.
    /// </summary>
    /// <param name="request">Warehouse and paging.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The movements page.</returns>
    [HttpGet]
    public async Task<IActionResult> Movements([FromQuery] StockLedgerRequest request, CancellationToken cancellationToken)
    {
        var movements = await _stock.GetLedgerAsync(request, cancellationToken);
        if (HandleAccess(movements) is { } denied)
        {
            return denied;
        }

        List<StockWarehouseDto> warehouses = (await _stock.GetWarehousesAsync(cancellationToken)).Value ?? new List<StockWarehouseDto>();
        return View(new MovementsViewModel(
            WarehouseOptions(warehouses.Where(w => w.CanView), request.WarehouseId),
            request.WarehouseId,
            new ListViewModel<StockLedgerEntryDto>(
                "Movements",
                movements.Value ?? new PagedResult<StockLedgerEntryDto> { Page = request.Page, PageSize = request.PageSize },
                search: null,
                new Dictionary<string, string?> { ["warehouseId"] = request.WarehouseId?.ToString() },
                itemName: "movement",
                pageAction: nameof(Movements))));
    }

    /// <summary>
    /// Checks every balance against the ledger and shows the result.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The ledger check page.</returns>
    [HttpGet]
    public async Task<IActionResult> Reconcile(CancellationToken cancellationToken)
    {
        var result = await _stock.ReconcileAsync(cancellationToken);
        return HandleAccess(result) ?? (result.Value is { } check ? View(check) : Failed(result, "The ledger could not be checked."));
    }

    /// <summary>
    /// Rewrites the balances that disagree with the ledger, then shows the fresh check.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The ledger check page.</returns>
    [HttpPost]
    public async Task<IActionResult> Rebuild(CancellationToken cancellationToken)
    {
        var result = await _stock.RebuildAsync(cancellationToken);
        if (HandleAccess(result) is { } denied)
        {
            return denied;
        }

        if (result.Value is not { } check)
        {
            return Failed(result, "The balances could not be rebuilt.");
        }

        FlashSuccess($"{check.BalancesRebuilt.Counted("balance")} rebuilt from the ledger.");
        return View(nameof(Reconcile), check);
    }

    /// <summary>
    /// Shows the opening-stock import page.
    /// </summary>
    /// <returns>The import page.</returns>
    [HttpGet]
    public IActionResult Opening() => View();

    /// <summary>
    /// Downloads the opening-stock template.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The .xlsx file.</returns>
    [HttpGet]
    public async Task<IActionResult> OpeningTemplate(CancellationToken cancellationToken)
    {
        var file = await _stock.GetOpeningTemplateAsync(cancellationToken);
        if (HandleAccess(file) is { } denied)
        {
            return denied;
        }

        if (file.Value is null)
        {
            FlashError(file.ErrorMessage ?? "Could not download the template.");
            return RedirectToAction(nameof(Opening));
        }

        return File(file.Value.Content, file.Value.ContentType, file.Value.FileName);
    }

    /// <summary>
    /// Uploads an opening-stock file for checking and shows the row-by-row result. Nothing is saved yet.
    /// </summary>
    /// <param name="file">Excel or CSV file.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The preview, or the import page with an error.</returns>
    [HttpPost]
    [RequestSizeLimit(MaxImportBytes)]
    public async Task<IActionResult> Opening(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError("file", "Choose an Excel (.xlsx) or CSV (.csv) file.");
            return View();
        }

        await using Stream content = file.OpenReadStream();
        var preview = await _stock.PreviewOpeningAsync(content, file.FileName, cancellationToken);
        if (HandleAccess(preview) is { } denied)
        {
            return denied;
        }

        if (preview.Value is null)
        {
            ModelState.AddModelError("file", preview.FieldErrors.Values.SelectMany(v => v).FirstOrDefault() ?? preview.ErrorMessage ?? "The file could not be checked.");
            return View();
        }

        return View("OpeningPreview", preview.Value);
    }

    /// <summary>
    /// Posts a checked opening-stock file and shows the stock.
    /// </summary>
    /// <param name="importId">Preview id.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>Redirect to the stock list, or back to the import page.</returns>
    [HttpPost]
    public async Task<IActionResult> OpeningCommit(Guid importId, CancellationToken cancellationToken)
    {
        var result = await _stock.CommitOpeningAsync(importId, cancellationToken);
        if (HandleAccess(result) is { } denied)
        {
            return denied;
        }

        if (result.Value is not { } done)
        {
            FlashError(result.IsNotFound ? "That preview has expired. Upload the file again." : result.ErrorMessage ?? "The opening stock could not be posted.");
            return RedirectToAction(nameof(Opening));
        }

        FlashSuccess($"Opening stock posted: {done.LinesPosted.Counted("line")} in {string.Join(", ", done.ReferenceNos)}.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Warehouse filter options, with the chosen one selected.
    /// </summary>
    /// <param name="warehouses">Warehouses to offer.</param>
    /// <param name="selected">Chosen warehouse.</param>
    /// <returns>The options.</returns>
    private static IReadOnlyList<SelectListItem> WarehouseOptions(IEnumerable<StockWarehouseDto> warehouses, Guid? selected) =>
        warehouses.Select(w => new SelectListItem($"{w.Code} — {w.Name}", w.Id.ToString(), w.Id == selected)).ToList();

    /// <summary>
    /// Returns to the stock list with an error.
    /// </summary>
    /// <param name="result">Failed API result.</param>
    /// <param name="fallback">Message when the API gave none.</param>
    /// <returns>A redirect.</returns>
    private IActionResult Failed(ApiResult result, string fallback)
    {
        FlashError(result.ErrorMessage ?? fallback);
        return RedirectToAction(nameof(Index));
    }
}
