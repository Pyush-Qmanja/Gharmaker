using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.Security.Authorization;
using Platform.Api.Services.Inventory.Stock;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Inventory;
using Platform.Shared.Entities.Inventory;

namespace Platform.Api.Controllers.Inventory;

/// <summary>
/// Stock: <c>/api/stock</c>. Balances, movement history and documents are
/// read here; receipts, transfers, adjustments, opening stock and reversals
/// are posted here. Every action checks the caller's access in the
/// warehouses involved, in the service (P6). Internal only (P1).
/// </summary>
[ApiController]
[Authorize]
[Route(ApiRoutes.Stock)]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class StockController : ControllerBase
{
    /// <summary>Content type of the Excel template.</summary>
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Largest import file accepted (10 MB).</summary>
    private const long MaxImportBytes = 10 * 1024 * 1024;

    private readonly IStockService _stock;
    private readonly IStockQueryService _queries;
    private readonly IStockReconcileService _reconcile;
    private readonly IOpeningStockImportService _opening;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="stock">Posts stock documents.</param>
    /// <param name="queries">Reads stock.</param>
    /// <param name="reconcile">Checks and rebuilds balances from the ledger.</param>
    /// <param name="opening">Opening-stock import.</param>
    public StockController(IStockService stock, IStockQueryService queries, IStockReconcileService reconcile, IOpeningStockImportService opening)
    {
        _stock = stock;
        _queries = queries;
        _reconcile = reconcile;
        _opening = opening;
    }

    /// <summary>
    /// Lists the warehouses where the caller can work with stock, and what they may do in each.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with the warehouses (possibly none).</returns>
    [HttpGet("warehouses")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<StockWarehouseDto>>> GetWarehouses(CancellationToken cancellationToken) =>
        Ok(await _queries.GetWarehousesAsync(cancellationToken));

    /// <summary>
    /// Lists every active warehouse a transfer can be sent to.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with id, code and name of each.</returns>
    [HttpGet("destinations")]
    [RequiresCapability(Capabilities.TransfersManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<StockWarehouseDto>>> GetDestinations(CancellationToken cancellationToken) =>
        Ok(await _queries.GetDestinationsAsync(cancellationToken));

    /// <summary>
    /// Lists every active SKU with the units it can be entered in, for the line pickers on stock forms.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with the SKUs, by code.</returns>
    [HttpGet("sku-options")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<StockSkuOptionDto>>> GetSkuOptions(CancellationToken cancellationToken) =>
        Ok(await _queries.GetSkuOptionsAsync(cancellationToken));

    /// <summary>
    /// Lists stock balances in the caller's warehouses.
    /// </summary>
    /// <param name="request">Warehouse, SKU code prefix, in-stock filter, paging.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with one page, by SKU code; 404 for a warehouse outside the caller's scope.</returns>
    [HttpGet("balances")]
    [RequiresCapability(Capabilities.StockView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<StockBalanceDto>>> GetBalances([FromQuery] StockBalanceRequest request, CancellationToken cancellationToken) =>
        Ok(await _queries.GetBalancesAsync(request, cancellationToken));

    /// <summary>
    /// Shows one SKU's stock in each of the caller's warehouses.
    /// </summary>
    /// <param name="skuId">SKU.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with the SKU's stock, or 404.</returns>
    [HttpGet("skus/{skuId:guid}")]
    [RequiresCapability(Capabilities.StockView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SkuStockDto>> GetSkuStock(Guid skuId, CancellationToken cancellationToken) =>
        Ok(await _queries.GetSkuStockAsync(skuId, cancellationToken));

    /// <summary>
    /// Lists ledger entries (movements), newest first.
    /// </summary>
    /// <param name="request">Warehouse and SKU filters, paging.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with one page.</returns>
    [HttpGet("ledger")]
    [RequiresCapability(Capabilities.StockView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<StockLedgerEntryDto>>> GetLedger([FromQuery] StockLedgerRequest request, CancellationToken cancellationToken) =>
        Ok(await _queries.GetLedgerAsync(request, cancellationToken));

    /// <summary>
    /// Lists documents of one kind, newest first.
    /// </summary>
    /// <param name="request">Type (required), warehouse, paging.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with one page.</returns>
    [HttpGet("documents")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<StockDocumentDto>>> GetDocuments([FromQuery] StockDocumentListRequest request, CancellationToken cancellationToken) =>
        Ok(await _queries.GetDocumentsAsync(request, cancellationToken));

    /// <summary>
    /// Shows one document.
    /// </summary>
    /// <param name="id">Document id.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with the document, or 404.</returns>
    [HttpGet("documents/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockDocumentDto>> GetDocument(Guid id, CancellationToken cancellationToken) =>
        Ok(await _queries.GetDocumentAsync(id, cancellationToken));

    /// <summary>
    /// Posts a goods receipt.
    /// </summary>
    /// <param name="request">Receipt.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>201 with the receipt.</returns>
    [HttpPost("receipts")]
    [RequiresCapability(Capabilities.ReceiptsManage)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<ActionResult<StockDocumentDto>> Receive([FromBody] CreateReceiptRequest request, CancellationToken cancellationToken) =>
        CreatedAsync(_stock.ReceiveAsync(request, cancellationToken), cancellationToken);

    /// <summary>
    /// Sends a transfer: stock leaves the source now and is in transit.
    /// </summary>
    /// <param name="request">Transfer.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>201 with the transfer; 422 when there is not enough stock.</returns>
    [HttpPost("transfers")]
    [RequiresCapability(Capabilities.TransfersManage)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<ActionResult<StockDocumentDto>> SendTransfer([FromBody] CreateTransferRequest request, CancellationToken cancellationToken) =>
        CreatedAsync(_stock.SendTransferAsync(request, cancellationToken), cancellationToken);

    /// <summary>
    /// Receives an in-transit transfer into its destination.
    /// </summary>
    /// <param name="id">Transfer id.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with the transfer, received; 422 if it is not in transit.</returns>
    [HttpPost("transfers/{id:guid}/receive")]
    [RequiresCapability(Capabilities.TransfersManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<StockDocumentDto>> ReceiveTransfer(Guid id, CancellationToken cancellationToken)
    {
        StockDocument document = await _stock.ReceiveTransferAsync(id, cancellationToken);
        return Ok(await _queries.GetDocumentAsync(document.Id, cancellationToken));
    }

    /// <summary>
    /// Posts an adjustment (damage, expiry or count correction).
    /// </summary>
    /// <param name="request">Adjustment.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>201 with the adjustment; 422 when it would take stock below zero.</returns>
    [HttpPost("adjustments")]
    [RequiresCapability(Capabilities.StockAdjust)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<ActionResult<StockDocumentDto>> Adjust([FromBody] CreateAdjustmentRequest request, CancellationToken cancellationToken) =>
        CreatedAsync(_stock.AdjustAsync(request, cancellationToken), cancellationToken);

    /// <summary>
    /// Cancels a document by posting a reversal of its movements.
    /// </summary>
    /// <param name="id">Document id.</param>
    /// <param name="request">Why.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>201 with the reversing document.</returns>
    [HttpPost("documents/{id:guid}/reverse")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<ActionResult<StockDocumentDto>> Reverse(Guid id, [FromBody] ReverseStockDocumentRequest request, CancellationToken cancellationToken) =>
        CreatedAsync(_stock.ReverseAsync(id, request, cancellationToken), cancellationToken);

    /// <summary>
    /// Compares every balance with the ledger (changes nothing).
    /// </summary>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with what was checked and any mismatches.</returns>
    [HttpGet("reconcile")]
    [RequiresCapability(Capabilities.StockAdjust)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<StockReconcileResultDto>> Reconcile(CancellationToken cancellationToken) =>
        Ok(await _reconcile.CheckAsync(cancellationToken));

    /// <summary>
    /// Rewrites every balance that disagrees with the ledger.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with the check after rebuilding.</returns>
    [HttpPost("reconcile/rebuild")]
    [RequiresCapability(Capabilities.StockAdjust)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<StockReconcileResultDto>> Rebuild(CancellationToken cancellationToken) =>
        Ok(await _reconcile.RebuildAsync(cancellationToken));

    /// <summary>
    /// Downloads the opening-stock Excel template.
    /// </summary>
    /// <returns>The .xlsx file.</returns>
    [HttpGet("opening/template")]
    [RequiresCapability(Capabilities.StockAdjust)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult OpeningTemplate() =>
        File(_opening.BuildTemplate(), ExcelContentType, "opening-stock-template.xlsx");

    /// <summary>
    /// Checks an opening-stock file. Saves nothing.
    /// </summary>
    /// <param name="file">Excel (.xlsx) or CSV file.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with the per-row preview and an import id; 400 when the file cannot be read.</returns>
    [HttpPost("opening/preview")]
    [RequiresCapability(Capabilities.StockAdjust)]
    [RequestSizeLimit(MaxImportBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OpeningStockPreviewDto>> PreviewOpening(IFormFile file, CancellationToken cancellationToken)
    {
        await using Stream content = file.OpenReadStream();
        return Ok(await _opening.PreviewAsync(content, file.FileName, cancellationToken));
    }

    /// <summary>
    /// Posts a previewed opening-stock file.
    /// </summary>
    /// <param name="importId">Id returned by the preview.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>200 with the documents posted; 404 when the preview expired.</returns>
    [HttpPost("opening/{importId:guid}/commit")]
    [RequiresCapability(Capabilities.StockAdjust)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OpeningStockResultDto>> CommitOpening(Guid importId, CancellationToken cancellationToken) =>
        Ok(await _opening.CommitAsync(importId, cancellationToken));

    /// <summary>
    /// Returns a posted document as 201 with its read model.
    /// </summary>
    /// <param name="posting">The posting.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>201 with the document.</returns>
    private async Task<ActionResult<StockDocumentDto>> CreatedAsync(Task<StockDocument> posting, CancellationToken cancellationToken)
    {
        StockDocument document = await posting;
        StockDocumentDto dto = await _queries.GetDocumentAsync(document.Id, cancellationToken);
        return CreatedAtAction(nameof(GetDocument), new { id = dto.Id }, dto);
    }
}
