using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Platform.Shared.Dtos.Inventory;
using Platform.Shared.Entities.Inventory;
using Platform.Web.Services.Api;

namespace Platform.Web.Controllers;

/// <summary>
/// Goods receipt screens: stock received from a supplier.
/// </summary>
public sealed class ReceiptsController : StockDocumentController<CreateReceiptRequest>
{
    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="stock">Stock API client.</param>
    /// <param name="validator">Shared receipt validator.</param>
    public ReceiptsController(IStockApiClient stock, IValidator<CreateReceiptRequest> validator)
        : base(stock, validator)
    {
    }

    /// <inheritdoc />
    protected override StockDocumentType Type => StockDocumentType.Receipt;

    /// <inheritdoc />
    protected override string PluralName => "Goods receipts";

    /// <inheritdoc />
    protected override string CreateTitle => "Receive goods";

    /// <inheritdoc />
    protected override string CreateSubtitle => "Record what arrived from a supplier. Stock is added as soon as you post.";

    /// <inheritdoc />
    protected override string SubmitText => "Post receipt";

    /// <inheritdoc />
    protected override bool CanPostIn(StockWarehouseDto warehouse) => warehouse.CanReceive;

    /// <inheritdoc />
    protected override Task<ApiResult<StockDocumentDto>> PostAsync(CreateReceiptRequest form, CancellationToken cancellationToken) =>
        Stock.ReceiveAsync(form, cancellationToken);
}

/// <summary>
/// Transfer screens: send stock to another warehouse, then receive it there.
/// </summary>
public sealed class TransfersController : StockDocumentController<CreateTransferRequest>
{
    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="stock">Stock API client.</param>
    /// <param name="validator">Shared transfer validator.</param>
    public TransfersController(IStockApiClient stock, IValidator<CreateTransferRequest> validator)
        : base(stock, validator)
    {
    }

    /// <inheritdoc />
    protected override StockDocumentType Type => StockDocumentType.Transfer;

    /// <inheritdoc />
    protected override string PluralName => "Transfers";

    /// <inheritdoc />
    protected override string CreateTitle => "Send a transfer";

    /// <inheritdoc />
    protected override string CreateSubtitle => "Stock leaves your warehouse now and is in transit until the other warehouse receives it.";

    /// <inheritdoc />
    protected override string SubmitText => "Send transfer";

    /// <inheritdoc />
    protected override bool CanPostIn(StockWarehouseDto warehouse) => warehouse.CanTransfer;

    /// <inheritdoc />
    protected override Task<ApiResult<StockDocumentDto>> PostAsync(CreateTransferRequest form, CancellationToken cancellationToken) =>
        Stock.SendTransferAsync(form, cancellationToken);

    /// <inheritdoc />
    protected override async Task<IReadOnlyList<SelectListItem>> LoadDestinationsAsync(CancellationToken cancellationToken) =>
        ((await Stock.GetDestinationsAsync(cancellationToken)).Value ?? new List<StockWarehouseDto>())
            .Select(w => new SelectListItem($"{w.Code} — {w.Name}", w.Id.ToString()))
            .ToList();

    /// <summary>
    /// Receives an in-transit transfer into its destination.
    /// </summary>
    /// <param name="id">Transfer id.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>Redirect back to the transfer with a message.</returns>
    [HttpPost]
    public async Task<IActionResult> Receive(Guid id, CancellationToken cancellationToken)
    {
        var result = await Stock.ReceiveTransferAsync(id, cancellationToken);
        if (result.IsUnauthorized)
        {
            return RedirectToLogin();
        }

        if (result.IsSuccess && result.Value is { } transfer)
        {
            FlashSuccess($"{transfer.ReferenceNo} received into {transfer.ToWarehouseCode}.");
        }
        else
        {
            FlashError(result.ErrorMessage ?? "The transfer could not be received.");
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}

/// <summary>
/// Adjustment screens: damage, expiry and count corrections.
/// </summary>
public sealed class AdjustmentsController : StockDocumentController<CreateAdjustmentRequest>
{
    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="stock">Stock API client.</param>
    /// <param name="validator">Shared adjustment validator.</param>
    public AdjustmentsController(IStockApiClient stock, IValidator<CreateAdjustmentRequest> validator)
        : base(stock, validator)
    {
    }

    /// <inheritdoc />
    protected override StockDocumentType Type => StockDocumentType.Adjustment;

    /// <inheritdoc />
    protected override string PluralName => "Adjustments";

    /// <inheritdoc />
    protected override string CreateTitle => "Adjust stock";

    /// <inheritdoc />
    protected override string CreateSubtitle => "Record damage, expiry or a count correction. Every adjustment needs a reason and stays on record.";

    /// <inheritdoc />
    protected override string SubmitText => "Post adjustment";

    /// <inheritdoc />
    protected override string QuantityHint =>
        "Damage / expiry: the amount lost (positive). Count correction: + to add, − to take away.";

    /// <inheritdoc />
    protected override bool CanPostIn(StockWarehouseDto warehouse) => warehouse.CanAdjust;

    /// <summary>
    /// Adjustments also show opening-stock documents (both are managed with stock-adjust access).
    /// </summary>
    /// <param name="type">Kind of document.</param>
    /// <returns>True for adjustments and opening stock.</returns>
    protected override bool Shows(StockDocumentType type) => type is StockDocumentType.Adjustment or StockDocumentType.Opening;

    /// <inheritdoc />
    protected override Task<ApiResult<StockDocumentDto>> PostAsync(CreateAdjustmentRequest form, CancellationToken cancellationToken) =>
        Stock.AdjustAsync(form, cancellationToken);
}
