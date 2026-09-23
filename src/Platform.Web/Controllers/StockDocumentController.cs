using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Inventory;
using Platform.Shared.Entities.Inventory;
using Platform.Web.Common;
using Platform.Web.Models;
using Platform.Web.Services.Api;

namespace Platform.Web.Controllers;

/// <summary>
/// List, create, view and reverse screens for one kind of stock document
/// (goods receipts, transfers, adjustments). A screen derives from this, names
/// its kind, posts through the API, and supplies one partial (<c>_Header</c>)
/// with its header fields. Lists, forms, lines and details are shared views.
/// </summary>
/// <typeparam name="TCreate">Create request.</typeparam>
public abstract class StockDocumentController<TCreate> : PlatformControllerBase
    where TCreate : class, IStockDocumentRequest, INormalisable, new()
{
    /// <summary>Blank line rows offered on a new form.</summary>
    private const int BlankLines = 5;

    /// <summary>Stock API client.</summary>
    protected readonly IStockApiClient Stock;

    private readonly IValidator<TCreate> _validator;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="stock">Stock API client.</param>
    /// <param name="validator">Shared create validator.</param>
    protected StockDocumentController(IStockApiClient stock, IValidator<TCreate> validator)
    {
        Stock = stock;
        _validator = validator;
    }

    /// <summary>Kind of document this screen handles.</summary>
    protected abstract StockDocumentType Type { get; }

    /// <summary>Plural title, e.g. "Goods receipts".</summary>
    protected abstract string PluralName { get; }

    /// <summary>Heading of the create page, e.g. "Receive goods".</summary>
    protected abstract string CreateTitle { get; }

    /// <summary>Line under the create heading.</summary>
    protected abstract string CreateSubtitle { get; }

    /// <summary>Words on the submit button.</summary>
    protected abstract string SubmitText { get; }

    /// <summary>
    /// Whether the user may post this kind of document in a warehouse.
    /// </summary>
    /// <param name="warehouse">Warehouse with the user's flags.</param>
    /// <returns>True when it can be chosen on the form.</returns>
    protected abstract bool CanPostIn(StockWarehouseDto warehouse);

    /// <summary>
    /// Posts the document through the API.
    /// </summary>
    /// <param name="form">Validated request.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The API result.</returns>
    protected abstract Task<ApiResult<StockDocumentDto>> PostAsync(TCreate form, CancellationToken cancellationToken);

    /// <summary>
    /// Whether this screen shows documents of a kind. Default: only its own.
    /// </summary>
    /// <param name="type">Kind of document.</param>
    /// <returns>True when this screen displays it.</returns>
    protected virtual bool Shows(StockDocumentType type) => type == Type;

    /// <summary>
    /// Help for the amount column. Default: positive amounts.
    /// </summary>
    protected virtual string QuantityHint => "Amount in the unit you choose; it is converted to the SKU's base unit.";

    /// <summary>
    /// Loads extra pickers (e.g. destinations). Default: none.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>Destination options (empty when not needed).</returns>
    protected virtual Task<IReadOnlyList<SelectListItem>> LoadDestinationsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SelectListItem>>(Array.Empty<SelectListItem>());

    /// <summary>
    /// Shows one page of documents of this kind, newest first.
    /// </summary>
    /// <param name="request">Warehouse filter and paging.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The list page.</returns>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] StockDocumentListRequest request, CancellationToken cancellationToken)
    {
        request.Type = Type;
        var result = await Stock.GetDocumentsAsync(request, cancellationToken);
        if (HandleAccess(result) is { } denied)
        {
            return denied;
        }

        if (!result.IsSuccess)
        {
            FlashError(result.ErrorMessage ?? $"Could not load {PluralName.ToLowerInvariant()}.");
        }

        IReadOnlyList<StockWarehouseDto> warehouses = (await Stock.GetWarehousesAsync(cancellationToken)).Value ?? new List<StockWarehouseDto>();
        ViewData[ViewDataKeys.CanCreate] = warehouses.Any(CanPostIn);
        ViewData[ViewDataKeys.WarehouseOptions] = warehouses
            .Select(w => new SelectListItem($"{w.Code} — {w.Name}", w.Id.ToString(), w.Id == request.WarehouseId))
            .ToList();

        var page = result.Value ?? new PagedResult<StockDocumentDto> { Page = request.Page, PageSize = request.PageSize };
        return View("StockDocumentIndex", new ListViewModel<StockDocumentDto>(
            PluralName,
            page,
            search: null,
            new Dictionary<string, string?> { ["warehouseId"] = request.WarehouseId?.ToString() },
            itemName: Type == StockDocumentType.Receipt ? "receipt" : Type.ToString().ToLowerInvariant()));
    }

    /// <summary>
    /// Shows an empty form with blank lines.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The form page.</returns>
    [HttpGet]
    public Task<IActionResult> Create(CancellationToken cancellationToken) => FormAsync(new TCreate(), cancellationToken);

    /// <summary>
    /// Validates and posts the document, then shows it.
    /// </summary>
    /// <param name="form">Posted fields and lines.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>Redirect to the posted document, or the form with errors.</returns>
    [HttpPost]
    public async Task<IActionResult> Create(TCreate form, CancellationToken cancellationToken)
    {
        // Blank line rows post empty amounts, which the model binder flags;
        // the shared validator below checks everything that matters.
        ModelState.Clear();
        form.Normalise();
        if (!await ValidateAsync(_validator, form, cancellationToken))
        {
            return await FormAsync(form, cancellationToken);
        }

        var result = await PostAsync(form, cancellationToken);
        if (result.IsUnauthorized)
        {
            return RedirectToLogin();
        }

        if (!result.IsSuccess || result.Value is null)
        {
            AddApiErrors(result);
            return await FormAsync(form, cancellationToken);
        }

        FlashSuccess($"{result.Value.ReferenceNo} posted.");
        return RedirectToAction(nameof(Details), new { id = result.Value.Id });
    }

    /// <summary>
    /// Shows one document with its lines and what can be done to it.
    /// </summary>
    /// <param name="id">Document id.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The details page, or 404.</returns>
    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await Stock.GetDocumentAsync(id, cancellationToken);
        if (HandleAccess(result) is { } denied)
        {
            return denied;
        }

        return result.IsSuccess && result.Value is { } document && Shows(document.Type)
            ? View("StockDocumentDetails", document)
            : NotFound();
    }

    /// <summary>
    /// Cancels a document by posting a reversal.
    /// </summary>
    /// <param name="id">Document id.</param>
    /// <param name="request">Why.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>Redirect to the reversal, or back with a message.</returns>
    [HttpPost]
    public async Task<IActionResult> Reverse(Guid id, ReverseStockDocumentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Remarks))
        {
            FlashError("Say why the document is being reversed.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var result = await Stock.ReverseAsync(id, request, cancellationToken);
        if (result.IsUnauthorized)
        {
            return RedirectToLogin();
        }

        if (!result.IsSuccess || result.Value is null)
        {
            FlashError(result.ErrorMessage ?? "The document could not be reversed.");
            return RedirectToAction(nameof(Details), new { id });
        }

        FlashSuccess($"Reversed by {result.Value.ReferenceNo}.");
        return RedirectToAction(nameof(Details), new { id = result.Value.Id });
    }

    /// <summary>
    /// Renders the shared form with its pickers; blank rows are added after the lines entered.
    /// </summary>
    /// <param name="form">Request being filled in.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The form view.</returns>
    private async Task<IActionResult> FormAsync(TCreate form, CancellationToken cancellationToken)
    {
        var warehousesResult = await Stock.GetWarehousesAsync(cancellationToken);
        if (HandleAccess(warehousesResult) is { } denied)
        {
            return denied;
        }

        List<StockWarehouseDto> allowed = (warehousesResult.Value ?? new List<StockWarehouseDto>()).Where(CanPostIn).ToList();
        if (allowed.Count == 0)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View("Forbidden");
        }

        List<StockSkuOptionDto> skus = (await Stock.GetSkuOptionsAsync(cancellationToken)).Value ?? new List<StockSkuOptionDto>();
        var lines = form.Lines.ToList();
        lines.AddRange(Enumerable.Range(0, Math.Max(BlankLines - lines.Count, 1)).Select(_ => new StockLineRequest()));

        return View("StockDocumentForm", new StockDocumentFormViewModel
        {
            Title = CreateTitle,
            Subtitle = CreateSubtitle,
            Form = form,
            Lines = lines,
            WarehouseOptions = allowed.Select(w => new SelectListItem($"{w.Code} — {w.Name}", w.Id.ToString())).ToList(),
            DestinationOptions = await LoadDestinationsAsync(cancellationToken),
            SkuOptions = skus,
            UnitCodes = skus.SelectMany(s => s.Units).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList(),
            SubmitText = SubmitText,
            QuantityHint = QuantityHint,
        });
    }
}
