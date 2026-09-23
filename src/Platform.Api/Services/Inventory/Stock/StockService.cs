using Platform.Api.Common;
using Platform.Api.Common.Exceptions;
using Platform.Api.Repositories;
using Platform.Api.Security.Authorization;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Inventory;
using Platform.Shared.Entities.Identity;
using Platform.Shared.Entities.Inventory;

namespace Platform.Api.Services.Inventory.Stock;

/// <summary>
/// Everything that moves stock: goods receipts, transfers (send, then receive),
/// adjustments, opening stock and reversals. Each checks the caller's access
/// for the warehouses involved (P6), converts units (P7) and posts through
/// <see cref="IStockPoster"/> (P2). Posted documents are never edited (P9).
/// </summary>
public interface IStockService
{
    /// <summary>
    /// Posts goods received from a supplier.
    /// </summary>
    /// <param name="request">Validated receipt.</param>
    /// <param name="cancellationToken">Cancels the posting.</param>
    /// <returns>The posted receipt.</returns>
    Task<StockDocument> ReceiveAsync(CreateReceiptRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends stock to another warehouse: it leaves the source now and is in transit until received.
    /// </summary>
    /// <param name="request">Validated transfer.</param>
    /// <param name="cancellationToken">Cancels the posting.</param>
    /// <returns>The transfer, in transit.</returns>
    Task<StockDocument> SendTransferAsync(CreateTransferRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Receives an in-transit transfer into its destination.
    /// </summary>
    /// <param name="documentId">Transfer id.</param>
    /// <param name="cancellationToken">Cancels the posting.</param>
    /// <returns>The transfer, received.</returns>
    Task<StockDocument> ReceiveTransferAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts a damage, expiry or count-correction adjustment.
    /// </summary>
    /// <param name="request">Validated adjustment.</param>
    /// <param name="cancellationToken">Cancels the posting.</param>
    /// <returns>The posted adjustment.</returns>
    Task<StockDocument> AdjustAsync(CreateAdjustmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts opening stock for one warehouse (go-live migration).
    /// </summary>
    /// <param name="warehouseId">Warehouse.</param>
    /// <param name="lines">Normalised, validated lines (positive amounts).</param>
    /// <param name="remarks">Notes, e.g. the imported file's name.</param>
    /// <param name="cancellationToken">Cancels the posting.</param>
    /// <returns>The posted opening-stock document.</returns>
    Task<StockDocument> PostOpeningAsync(Guid warehouseId, IReadOnlyList<StockLineRequest> lines, string remarks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a posted document with a new document that undoes its ledger entries.
    /// </summary>
    /// <param name="documentId">Document to cancel.</param>
    /// <param name="request">Why.</param>
    /// <param name="cancellationToken">Cancels the posting.</param>
    /// <returns>The reversing document.</returns>
    Task<StockDocument> ReverseAsync(Guid documentId, ReverseStockDocumentRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IStockService"/>.
/// </summary>
public sealed class StockService : IStockService
{
    private readonly IRepository<StockDocument> _documents;
    private readonly IRepository<Warehouse> _warehouses;
    private readonly IPermissionService _permissions;
    private readonly IStockLineResolver _lines;
    private readonly IStockPoster _poster;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="documents">Stock document data access.</param>
    /// <param name="warehouses">Warehouse data access.</param>
    /// <param name="permissions">Caller's capabilities and scopes.</param>
    /// <param name="lines">Resolves SKU codes and converts units.</param>
    /// <param name="poster">Writes stock.</param>
    /// <param name="currentUser">Caller, for who received a transfer.</param>
    /// <param name="timeProvider">Clock, for when a transfer was received.</param>
    public StockService(
        IRepository<StockDocument> documents,
        IRepository<Warehouse> warehouses,
        IPermissionService permissions,
        IStockLineResolver lines,
        IStockPoster poster,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _documents = documents;
        _warehouses = warehouses;
        _permissions = permissions;
        _lines = lines;
        _poster = poster;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<StockDocument> ReceiveAsync(CreateReceiptRequest request, CancellationToken cancellationToken = default)
    {
        Warehouse warehouse = await RequireWarehouseAsync(request.WarehouseId, Capabilities.ReceiptsManage, "receive goods into", nameof(request.WarehouseId), cancellationToken);
        List<StockDocumentLine> lines = await _lines.ResolveAsync(request.Lines, cancellationToken);

        var document = new StockDocument
        {
            Type = StockDocumentType.Receipt,
            Status = StockDocumentStatus.Posted,
            Reason = StockReason.GrnReceipt,
            WarehouseId = warehouse.Id,
            WarehouseIds = new List<Guid> { warehouse.Id },
            SupplierName = request.SupplierName.Trim(),
            SupplierReferenceNo = Clean(request.SupplierReferenceNo),
            Remarks = Clean(request.Remarks),
            Lines = lines,
        };

        return await PostNewAsync(document, "GRN", lines.Select((line, i) =>
            new StockMovement(StockPoster.EntryId(document.Id, i, "post"), warehouse.Id, warehouse.Code, line, line.BaseQuantity, StockReason.GrnReceipt)), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<StockDocument> SendTransferAsync(CreateTransferRequest request, CancellationToken cancellationToken = default)
    {
        Warehouse source = await RequireWarehouseAsync(request.WarehouseId, Capabilities.TransfersManage, "send transfers from", nameof(request.WarehouseId), cancellationToken);
        Warehouse destination = await _warehouses.GetByIdAsync(request.ToWarehouseId, cancellationToken) is { IsActive: true } found
            ? found
            : throw new FieldValidationException(nameof(request.ToWarehouseId), "Choose an active warehouse to send to.");
        if (destination.Id == source.Id)
        {
            throw new FieldValidationException(nameof(request.ToWarehouseId), "Send to a different warehouse.");
        }

        List<StockDocumentLine> lines = await _lines.ResolveAsync(request.Lines, cancellationToken);
        var document = new StockDocument
        {
            Type = StockDocumentType.Transfer,
            Status = StockDocumentStatus.InTransit,
            Reason = StockReason.TransferOut,
            WarehouseId = source.Id,
            ToWarehouseId = destination.Id,
            WarehouseIds = new List<Guid> { source.Id, destination.Id },
            Remarks = Clean(request.Remarks),
            Lines = lines,
        };

        return await PostNewAsync(document, "TRF", lines.Select((line, i) =>
            new StockMovement(StockPoster.EntryId(document.Id, i, "out"), source.Id, source.Code, line, -line.BaseQuantity, StockReason.TransferOut)), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<StockDocument> ReceiveTransferAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        StockDocument transfer = await LoadVisibleAsync(documentId, cancellationToken);
        if (transfer.Type != StockDocumentType.Transfer || transfer.ToWarehouseId is not { } destinationId)
        {
            throw new BusinessRuleException("Only a transfer can be received.");
        }

        if (!await _permissions.CoversAsync(Capabilities.TransfersManage, ScopeType.Warehouse, destinationId, cancellationToken))
        {
            throw new ForbiddenException("You cannot receive transfers into this warehouse.");
        }

        Warehouse destination = await _warehouses.GetByIdAsync(destinationId, cancellationToken)
            ?? throw new BusinessRuleException("The destination warehouse no longer exists.");

        return await _poster.PostAsync(async session =>
        {
            // Re-read inside the transaction so two people cannot receive it twice.
            StockDocument current = await session.GetAsync<StockDocument>(documentId) ?? throw new NotFoundException("Transfer");
            if (current.Status != StockDocumentStatus.InTransit)
            {
                throw new BusinessRuleException($"{current.ReferenceNo} is {Describe(current.Status)}, not in transit.");
            }

            current.Status = StockDocumentStatus.Received;
            current.ReceivedAt = _timeProvider.GetUtcNow().UtcDateTime;
            current.ReceivedBy = _currentUser.UserId;

            var posting = new StockPosting { Document = current };
            posting.Movements.AddRange(current.Lines.Select((line, i) =>
                new StockMovement(StockPoster.EntryId(current.Id, i, "in"), destination.Id, destination.Code, line, line.BaseQuantity, StockReason.TransferIn)));
            return posting;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<StockDocument> AdjustAsync(CreateAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        Warehouse warehouse = await RequireWarehouseAsync(request.WarehouseId, Capabilities.StockAdjust, "adjust stock in", nameof(request.WarehouseId), cancellationToken);
        List<StockDocumentLine> lines = await _lines.ResolveAsync(request.Lines, cancellationToken);

        // Damage and expiry are entered as the amount lost and always take stock away.
        bool takesAway = request.Reason is StockReason.Damage or StockReason.ExpiryWriteOff;
        if (takesAway)
        {
            foreach (StockDocumentLine line in lines)
            {
                line.Quantity = -line.Quantity;
                line.BaseQuantity = -line.BaseQuantity;
            }
        }

        var document = new StockDocument
        {
            Type = StockDocumentType.Adjustment,
            Status = StockDocumentStatus.Posted,
            Reason = request.Reason,
            WarehouseId = warehouse.Id,
            WarehouseIds = new List<Guid> { warehouse.Id },
            Remarks = Clean(request.Remarks),
            Lines = lines,
        };

        return await PostNewAsync(document, "ADJ", lines.Select((line, i) =>
            new StockMovement(StockPoster.EntryId(document.Id, i, "post"), warehouse.Id, warehouse.Code, line, line.BaseQuantity, request.Reason)), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<StockDocument> PostOpeningAsync(Guid warehouseId, IReadOnlyList<StockLineRequest> lines, string remarks, CancellationToken cancellationToken = default)
    {
        Warehouse warehouse = await RequireWarehouseAsync(warehouseId, Capabilities.StockAdjust, "post opening stock in", "WarehouseId", cancellationToken);
        List<StockDocumentLine> resolved = await _lines.ResolveAsync(lines, cancellationToken);

        var document = new StockDocument
        {
            Type = StockDocumentType.Opening,
            Status = StockDocumentStatus.Posted,
            Reason = StockReason.OpeningBalance,
            WarehouseId = warehouse.Id,
            WarehouseIds = new List<Guid> { warehouse.Id },
            Remarks = Clean(remarks),
            Lines = resolved,
        };

        return await PostNewAsync(document, "OPN", resolved.Select((line, i) =>
            new StockMovement(StockPoster.EntryId(document.Id, i, "post"), warehouse.Id, warehouse.Code, line, line.BaseQuantity, StockReason.OpeningBalance)), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<StockDocument> ReverseAsync(Guid documentId, ReverseStockDocumentRequest request, CancellationToken cancellationToken = default)
    {
        StockDocument original = await LoadVisibleAsync(documentId, cancellationToken);
        await RequireManageAsync(original, cancellationToken);

        IReadOnlyDictionary<Guid, Warehouse> warehouses = (await _warehouses.GetByIdsAsync(original.WarehouseIds, cancellationToken)).ToDictionary(w => w.Id);
        string CodeOf(Guid id) => warehouses.TryGetValue(id, out Warehouse? w) ? w.Code : "?";

        return await _poster.PostAsync(async session =>
        {
            StockDocument current = await session.GetAsync<StockDocument>(documentId) ?? throw new NotFoundException("Stock document");
            if (current.ReversesDocumentId is not null)
            {
                throw new BusinessRuleException($"{current.ReferenceNo} is itself a reversal and cannot be reversed.");
            }

            if (current.Status == StockDocumentStatus.Reversed)
            {
                throw new BusinessRuleException($"{current.ReferenceNo} has already been reversed.");
            }

            var reversal = new StockDocument
            {
                Type = current.Type,
                Status = StockDocumentStatus.Posted,
                Reason = current.Reason,
                WarehouseId = current.WarehouseId,
                ToWarehouseId = current.ToWarehouseId,
                WarehouseIds = current.WarehouseIds.ToList(),
                SupplierName = current.SupplierName,
                SupplierReferenceNo = current.SupplierReferenceNo,
                Remarks = request.Remarks.Trim(),
                Lines = current.Lines,
                ReversesDocumentId = current.Id,
            };

            var posting = new StockPosting { Document = reversal, IsNew = true, Series = "REV" };
            for (int i = 0; i < current.Lines.Count; i++)
            {
                StockDocumentLine line = current.Lines[i];
                if (current.Type == StockDocumentType.Transfer)
                {
                    posting.Movements.Add(new StockMovement(StockPoster.EntryId(reversal.Id, i, "out"), current.WarehouseId, CodeOf(current.WarehouseId),
                        line, line.BaseQuantity, StockReason.TransferOut, StockPoster.EntryId(current.Id, i, "out")));
                    if (current.Status == StockDocumentStatus.Received && current.ToWarehouseId is { } destinationId)
                    {
                        posting.Movements.Add(new StockMovement(StockPoster.EntryId(reversal.Id, i, "in"), destinationId, CodeOf(destinationId),
                            line, -line.BaseQuantity, StockReason.TransferIn, StockPoster.EntryId(current.Id, i, "in")));
                    }
                }
                else
                {
                    posting.Movements.Add(new StockMovement(StockPoster.EntryId(reversal.Id, i, "post"), current.WarehouseId, CodeOf(current.WarehouseId),
                        line, -line.BaseQuantity, current.Reason, StockPoster.EntryId(current.Id, i, "post")));
                }
            }

            current.Status = StockDocumentStatus.Reversed;
            current.ReversedByDocumentId = reversal.Id;
            posting.Updates.Add(current);
            return posting;
        }, cancellationToken);
    }

    /// <summary>
    /// The capability that manages a kind of document.
    /// </summary>
    /// <param name="type">Document type.</param>
    /// <returns>Capability code.</returns>
    public static string ManageCapabilityFor(StockDocumentType type) => type switch
    {
        StockDocumentType.Receipt => Capabilities.ReceiptsManage,
        StockDocumentType.Transfer => Capabilities.TransfersManage,
        _ => Capabilities.StockAdjust,
    };

    /// <summary>
    /// The capability that shows a kind of document.
    /// </summary>
    /// <param name="type">Document type.</param>
    /// <returns>Capability code.</returns>
    public static string ViewCapabilityFor(StockDocumentType type) => type switch
    {
        StockDocumentType.Receipt => Capabilities.ReceiptsView,
        StockDocumentType.Transfer => Capabilities.TransfersView,
        _ => Capabilities.StockView,
    };

    /// <summary>
    /// Posts a new document with its movements.
    /// </summary>
    /// <param name="document">New document.</param>
    /// <param name="series">Reference number prefix.</param>
    /// <param name="movements">Its movements.</param>
    /// <param name="cancellationToken">Cancels the posting.</param>
    /// <returns>The posted document.</returns>
    private Task<StockDocument> PostNewAsync(StockDocument document, string series, IEnumerable<StockMovement> movements, CancellationToken cancellationToken)
    {
        List<StockMovement> list = movements.ToList();
        return _poster.PostAsync(_ =>
        {
            var posting = new StockPosting { Document = document, IsNew = true, Series = series };
            posting.Movements.AddRange(list);
            return Task.FromResult(posting);
        }, cancellationToken);
    }

    /// <summary>
    /// Checks the caller may act in a warehouse and that it can take stock.
    /// A warehouse outside their access is refused without saying whether it exists.
    /// </summary>
    /// <param name="warehouseId">Warehouse chosen.</param>
    /// <param name="capability">Capability the action needs there.</param>
    /// <param name="action">Words for the refusal, e.g. "receive goods into".</param>
    /// <param name="field">Request field, for a validation message.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The warehouse.</returns>
    private async Task<Warehouse> RequireWarehouseAsync(Guid warehouseId, string capability, string action, string field, CancellationToken cancellationToken)
    {
        if (!await _permissions.CoversAsync(capability, ScopeType.Warehouse, warehouseId, cancellationToken))
        {
            throw new ForbiddenException($"You cannot {action} this warehouse.");
        }

        return await _warehouses.GetByIdAsync(warehouseId, cancellationToken) is { IsActive: true } warehouse
            ? warehouse
            : throw new FieldValidationException(field, "Choose an active warehouse.");
    }

    /// <summary>
    /// Loads a document the caller can see (404 otherwise, P6).
    /// </summary>
    /// <param name="documentId">Document id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The document.</returns>
    private async Task<StockDocument> LoadVisibleAsync(Guid documentId, CancellationToken cancellationToken)
    {
        StockDocument? document = await _documents.GetByIdAsync(documentId, cancellationToken);
        if (document is not null)
        {
            string capability = ViewCapabilityFor(document.Type);
            foreach (Guid warehouseId in document.WarehouseIds)
            {
                if (await _permissions.CoversAsync(capability, ScopeType.Warehouse, warehouseId, cancellationToken))
                {
                    return document;
                }
            }
        }

        throw new NotFoundException("Stock document");
    }

    /// <summary>
    /// Checks the caller manages every warehouse a reversal would change.
    /// </summary>
    /// <param name="document">Document to reverse.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when allowed.</returns>
    /// <exception cref="ForbiddenException">A warehouse is outside the caller's manage access.</exception>
    private async Task RequireManageAsync(StockDocument document, CancellationToken cancellationToken)
    {
        string capability = ManageCapabilityFor(document.Type);
        var touched = new List<Guid> { document.WarehouseId };
        if (document.Type == StockDocumentType.Transfer && document.Status == StockDocumentStatus.Received && document.ToWarehouseId is { } destination)
        {
            touched.Add(destination);
        }

        foreach (Guid warehouseId in touched)
        {
            if (!await _permissions.CoversAsync(capability, ScopeType.Warehouse, warehouseId, cancellationToken))
            {
                throw new ForbiddenException("You cannot reverse this document — it moves stock in a warehouse you do not manage.");
            }
        }
    }

    /// <summary>
    /// Words for a status in messages.
    /// </summary>
    /// <param name="status">Status.</param>
    /// <returns>Lower-case words.</returns>
    private static string Describe(StockDocumentStatus status) => status switch
    {
        StockDocumentStatus.InTransit => "in transit",
        StockDocumentStatus.Received => "already received",
        StockDocumentStatus.Reversed => "cancelled",
        _ => "posted",
    };

    /// <summary>
    /// Trims optional text and turns blank into null.
    /// </summary>
    /// <param name="value">Text.</param>
    /// <returns>The trimmed text, or null.</returns>
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
