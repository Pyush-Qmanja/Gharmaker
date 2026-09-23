using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Inventory;

namespace Platform.Web.Services.Api;

/// <summary>
/// Typed client for <c>/api/stock</c>: balances, movements, documents,
/// postings, opening stock and the ledger check.
/// </summary>
public interface IStockApiClient
{
    /// <summary>Warehouses where the user can work with stock, and what they may do there.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The warehouses.</returns>
    Task<ApiResult<List<StockWarehouseDto>>> GetWarehousesAsync(CancellationToken cancellationToken = default);

    /// <summary>Every warehouse a transfer can be sent to.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The warehouses.</returns>
    Task<ApiResult<List<StockWarehouseDto>>> GetDestinationsAsync(CancellationToken cancellationToken = default);

    /// <summary>SKUs and their units, for line pickers.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The SKUs.</returns>
    Task<ApiResult<List<StockSkuOptionDto>>> GetSkuOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>One page of balances.</summary>
    /// <param name="request">Filters and paging.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The page.</returns>
    Task<ApiResult<PagedResult<StockBalanceDto>>> GetBalancesAsync(StockBalanceRequest request, CancellationToken cancellationToken = default);

    /// <summary>One SKU's stock by warehouse.</summary>
    /// <param name="skuId">SKU.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The SKU's stock.</returns>
    Task<ApiResult<SkuStockDto>> GetSkuStockAsync(Guid skuId, CancellationToken cancellationToken = default);

    /// <summary>One page of movements, newest first.</summary>
    /// <param name="request">Filters and paging.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The page.</returns>
    Task<ApiResult<PagedResult<StockLedgerEntryDto>>> GetLedgerAsync(StockLedgerRequest request, CancellationToken cancellationToken = default);

    /// <summary>One page of documents of a kind, newest first.</summary>
    /// <param name="request">Type, warehouse and paging.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The page.</returns>
    Task<ApiResult<PagedResult<StockDocumentDto>>> GetDocumentsAsync(StockDocumentListRequest request, CancellationToken cancellationToken = default);

    /// <summary>One document.</summary>
    /// <param name="id">Document id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The document.</returns>
    Task<ApiResult<StockDocumentDto>> GetDocumentAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Posts a goods receipt.</summary>
    /// <param name="request">Receipt.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The receipt.</returns>
    Task<ApiResult<StockDocumentDto>> ReceiveAsync(CreateReceiptRequest request, CancellationToken cancellationToken = default);

    /// <summary>Sends a transfer.</summary>
    /// <param name="request">Transfer.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The transfer.</returns>
    Task<ApiResult<StockDocumentDto>> SendTransferAsync(CreateTransferRequest request, CancellationToken cancellationToken = default);

    /// <summary>Receives a transfer.</summary>
    /// <param name="id">Transfer id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The transfer.</returns>
    Task<ApiResult<StockDocumentDto>> ReceiveTransferAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Posts an adjustment.</summary>
    /// <param name="request">Adjustment.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The adjustment.</returns>
    Task<ApiResult<StockDocumentDto>> AdjustAsync(CreateAdjustmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reverses a document.</summary>
    /// <param name="id">Document id.</param>
    /// <param name="request">Why.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The reversing document.</returns>
    Task<ApiResult<StockDocumentDto>> ReverseAsync(Guid id, ReverseStockDocumentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Checks every balance against the ledger.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The check.</returns>
    Task<ApiResult<StockReconcileResultDto>> ReconcileAsync(CancellationToken cancellationToken = default);

    /// <summary>Rewrites balances that disagree with the ledger.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The check after rebuilding.</returns>
    Task<ApiResult<StockReconcileResultDto>> RebuildAsync(CancellationToken cancellationToken = default);

    /// <summary>Downloads the opening-stock template.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The file.</returns>
    Task<ApiResult<ApiFile>> GetOpeningTemplateAsync(CancellationToken cancellationToken = default);

    /// <summary>Checks an opening-stock file.</summary>
    /// <param name="content">File content.</param>
    /// <param name="fileName">File name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The preview.</returns>
    Task<ApiResult<OpeningStockPreviewDto>> PreviewOpeningAsync(Stream content, string fileName, CancellationToken cancellationToken = default);

    /// <summary>Posts a previewed opening-stock file.</summary>
    /// <param name="importId">Preview id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The documents posted.</returns>
    Task<ApiResult<OpeningStockResultDto>> CommitOpeningAsync(Guid importId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IStockApiClient"/> over <see cref="IApiClient"/>.
/// </summary>
public sealed class StockApiClient : IStockApiClient
{
    private readonly IApiClient _api;

    /// <summary>
    /// Creates the client.
    /// </summary>
    /// <param name="api">Underlying JSON client.</param>
    public StockApiClient(IApiClient api)
    {
        _api = api;
    }

    /// <inheritdoc />
    public Task<ApiResult<List<StockWarehouseDto>>> GetWarehousesAsync(CancellationToken cancellationToken = default) =>
        _api.GetAsync<List<StockWarehouseDto>>($"{ApiRoutes.Stock}/warehouses", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<List<StockWarehouseDto>>> GetDestinationsAsync(CancellationToken cancellationToken = default) =>
        _api.GetAsync<List<StockWarehouseDto>>($"{ApiRoutes.Stock}/destinations", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<List<StockSkuOptionDto>>> GetSkuOptionsAsync(CancellationToken cancellationToken = default) =>
        _api.GetAsync<List<StockSkuOptionDto>>($"{ApiRoutes.Stock}/sku-options", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<PagedResult<StockBalanceDto>>> GetBalancesAsync(StockBalanceRequest request, CancellationToken cancellationToken = default) =>
        _api.GetAsync<PagedResult<StockBalanceDto>>(Url("balances", request,
            (nameof(request.WarehouseId), request.WarehouseId?.ToString()),
            (nameof(request.InStockOnly), request.InStockOnly ? "true" : null)), cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<SkuStockDto>> GetSkuStockAsync(Guid skuId, CancellationToken cancellationToken = default) =>
        _api.GetAsync<SkuStockDto>($"{ApiRoutes.Stock}/skus/{skuId}", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<PagedResult<StockLedgerEntryDto>>> GetLedgerAsync(StockLedgerRequest request, CancellationToken cancellationToken = default) =>
        _api.GetAsync<PagedResult<StockLedgerEntryDto>>(Url("ledger", request,
            (nameof(request.WarehouseId), request.WarehouseId?.ToString()),
            (nameof(request.SkuId), request.SkuId?.ToString())), cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<PagedResult<StockDocumentDto>>> GetDocumentsAsync(StockDocumentListRequest request, CancellationToken cancellationToken = default) =>
        _api.GetAsync<PagedResult<StockDocumentDto>>(Url("documents", request,
            (nameof(request.Type), request.Type?.ToString()),
            (nameof(request.WarehouseId), request.WarehouseId?.ToString())), cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<StockDocumentDto>> GetDocumentAsync(Guid id, CancellationToken cancellationToken = default) =>
        _api.GetAsync<StockDocumentDto>($"{ApiRoutes.Stock}/documents/{id}", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<StockDocumentDto>> ReceiveAsync(CreateReceiptRequest request, CancellationToken cancellationToken = default) =>
        _api.PostAsync<CreateReceiptRequest, StockDocumentDto>($"{ApiRoutes.Stock}/receipts", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<StockDocumentDto>> SendTransferAsync(CreateTransferRequest request, CancellationToken cancellationToken = default) =>
        _api.PostAsync<CreateTransferRequest, StockDocumentDto>($"{ApiRoutes.Stock}/transfers", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<StockDocumentDto>> ReceiveTransferAsync(Guid id, CancellationToken cancellationToken = default) =>
        _api.PostAsync<object?, StockDocumentDto>($"{ApiRoutes.Stock}/transfers/{id}/receive", null, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<StockDocumentDto>> AdjustAsync(CreateAdjustmentRequest request, CancellationToken cancellationToken = default) =>
        _api.PostAsync<CreateAdjustmentRequest, StockDocumentDto>($"{ApiRoutes.Stock}/adjustments", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<StockDocumentDto>> ReverseAsync(Guid id, ReverseStockDocumentRequest request, CancellationToken cancellationToken = default) =>
        _api.PostAsync<ReverseStockDocumentRequest, StockDocumentDto>($"{ApiRoutes.Stock}/documents/{id}/reverse", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<StockReconcileResultDto>> ReconcileAsync(CancellationToken cancellationToken = default) =>
        _api.GetAsync<StockReconcileResultDto>($"{ApiRoutes.Stock}/reconcile", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<StockReconcileResultDto>> RebuildAsync(CancellationToken cancellationToken = default) =>
        _api.PostAsync<object?, StockReconcileResultDto>($"{ApiRoutes.Stock}/reconcile/rebuild", null, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<ApiFile>> GetOpeningTemplateAsync(CancellationToken cancellationToken = default) =>
        _api.GetFileAsync($"{ApiRoutes.Stock}/opening/template", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<OpeningStockPreviewDto>> PreviewOpeningAsync(Stream content, string fileName, CancellationToken cancellationToken = default) =>
        _api.PostFileAsync<OpeningStockPreviewDto>($"{ApiRoutes.Stock}/opening/preview", content, fileName, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<OpeningStockResultDto>> CommitOpeningAsync(Guid importId, CancellationToken cancellationToken = default) =>
        _api.PostAsync<object?, OpeningStockResultDto>($"{ApiRoutes.Stock}/opening/{importId}/commit", null, cancellationToken);

    /// <summary>
    /// Builds a stock URL with paging, search and extra filters (empty values left out).
    /// </summary>
    /// <param name="path">Path under <c>/api/stock</c>.</param>
    /// <param name="paging">Page, page size and search.</param>
    /// <param name="filters">Extra query values.</param>
    /// <returns>The relative URL.</returns>
    private static string Url(string path, PagedRequest paging, params (string Name, string? Value)[] filters)
    {
        var query = new Dictionary<string, string?>
        {
            [nameof(PagedRequest.Page)] = paging.Page.ToString(CultureInfo.InvariantCulture),
            [nameof(PagedRequest.PageSize)] = paging.PageSize.ToString(CultureInfo.InvariantCulture),
            [nameof(PagedRequest.Search)] = paging.Search,
        };
        foreach (var (name, value) in filters)
        {
            query[name] = value;
        }

        return QueryHelpers.AddQueryString($"{ApiRoutes.Stock}/{path}", query.Where(q => !string.IsNullOrEmpty(q.Value)));
    }
}

/// <summary>
/// Typed client for <c>/api/audit</c>.
/// </summary>
public interface IAuditApiClient
{
    /// <summary>One page of the audit log.</summary>
    /// <param name="request">Filters and paging.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The page.</returns>
    Task<ApiResult<PagedResult<AuditEntryDto>>> GetPagedAsync(AuditListRequest request, CancellationToken cancellationToken = default);

    /// <summary>One entry.</summary>
    /// <param name="id">Entry id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The entry.</returns>
    Task<ApiResult<AuditEntryDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IAuditApiClient"/>.
/// </summary>
public sealed class AuditApiClient : IAuditApiClient
{
    private readonly IApiClient _api;

    /// <summary>
    /// Creates the client.
    /// </summary>
    /// <param name="api">Underlying JSON client.</param>
    public AuditApiClient(IApiClient api)
    {
        _api = api;
    }

    /// <inheritdoc />
    public Task<ApiResult<PagedResult<AuditEntryDto>>> GetPagedAsync(AuditListRequest request, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string?>
        {
            [nameof(request.Page)] = request.Page.ToString(CultureInfo.InvariantCulture),
            [nameof(request.PageSize)] = request.PageSize.ToString(CultureInfo.InvariantCulture),
            [nameof(request.Entity)] = request.Entity,
            [nameof(request.EntityId)] = request.EntityId?.ToString(),
        };
        return _api.GetAsync<PagedResult<AuditEntryDto>>(
            QueryHelpers.AddQueryString(ApiRoutes.Audit, query.Where(q => !string.IsNullOrEmpty(q.Value))), cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<AuditEntryDto>> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _api.GetAsync<AuditEntryDto>($"{ApiRoutes.Audit}/{id}", cancellationToken);
}
