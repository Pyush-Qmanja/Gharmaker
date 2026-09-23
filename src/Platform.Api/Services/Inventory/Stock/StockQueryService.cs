using Google.Cloud.Firestore;
using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Repositories;
using Platform.Api.Security.Authorization;
using Platform.Api.Services.Catalog;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Inventory;
using Platform.Shared.Entities.Catalog;
using Platform.Shared.Entities.Identity;
using Platform.Shared.Entities.Inventory;
using Platform.Shared.Units;

namespace Platform.Api.Services.Inventory.Stock;

/// <summary>
/// Reads stock for screens: balances, one SKU across warehouses, movement
/// history and documents — always limited to the warehouses where the caller
/// holds the matching view capability (P6).
/// </summary>
public interface IStockQueryService
{
    /// <summary>
    /// Lists every active warehouse where the caller can do anything with stock, and what.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The warehouses, by code.</returns>
    Task<List<StockWarehouseDto>> GetWarehousesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every active warehouse a transfer can be sent to.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The warehouses, by code (only id, code and name are filled).</returns>
    Task<List<StockWarehouseDto>> GetDestinationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every active SKU with the units it can be entered in, for the line pickers.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>SKUs by code.</returns>
    Task<List<StockSkuOptionDto>> GetSkuOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pages through stock balances.
    /// </summary>
    /// <param name="request">Warehouse, search (SKU code prefix), in-stock filter, paging.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One page, by SKU code.</returns>
    Task<PagedResult<StockBalanceDto>> GetBalancesAsync(StockBalanceRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Shows one SKU's stock in every warehouse the caller can see.
    /// </summary>
    /// <param name="skuId">SKU.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The SKU's stock.</returns>
    Task<SkuStockDto> GetSkuStockAsync(Guid skuId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pages through ledger entries, newest first.
    /// </summary>
    /// <param name="request">Warehouse and SKU filters, paging.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One page.</returns>
    Task<PagedResult<StockLedgerEntryDto>> GetLedgerAsync(StockLedgerRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pages through documents of one kind, newest first.
    /// </summary>
    /// <param name="request">Type (required), warehouse, paging.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One page.</returns>
    Task<PagedResult<StockDocumentDto>> GetDocumentsAsync(StockDocumentListRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Shows one document, with what the caller may do to it.
    /// </summary>
    /// <param name="documentId">Document id.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The document.</returns>
    Task<StockDocumentDto> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Firestore-backed <see cref="IStockQueryService"/>.
/// </summary>
/// <remarks>
/// A caller limited to warehouses is served with Firestore "in" filters, which
/// take at most 30 warehouses; beyond that only the first 30 (by id) are shown.
/// </remarks>
public sealed class StockQueryService : IStockQueryService
{
    /// <summary>Firestore's limit on values in an "in" / "array-contains-any" filter.</summary>
    private const int InFilterLimit = 30;

    private static readonly string WarehouseIdField = FirestoreNaming.Field(nameof(StockBalance.WarehouseId));
    private static readonly string SkuIdField = FirestoreNaming.Field(nameof(StockBalance.SkuId));
    private static readonly string SkuCodeField = FirestoreNaming.Field(nameof(StockBalance.SkuCode));
    private static readonly string IsInStockField = FirestoreNaming.Field(nameof(StockBalance.IsInStock));
    private static readonly string CreatedAtField = FirestoreNaming.Field(nameof(StockLedgerEntry.CreatedAt));
    private static readonly string TypeField = FirestoreNaming.Field(nameof(StockDocument.Type));
    private static readonly string WarehouseIdsField = FirestoreNaming.Field(nameof(StockDocument.WarehouseIds));

    private readonly IRepository<StockBalance> _balances;
    private readonly IRepository<StockLedgerEntry> _ledger;
    private readonly IRepository<StockDocument> _documents;
    private readonly IRepository<Warehouse> _warehouses;
    private readonly IRepository<Sku> _skus;
    private readonly IRepository<Product> _products;
    private readonly IRepository<User> _users;
    private readonly IPermissionService _permissions;
    private readonly IUomConversionProvider _conversions;
    private Dictionary<Guid, Warehouse>? _allWarehouses;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="balances">Balance data access.</param>
    /// <param name="ledger">Ledger data access.</param>
    /// <param name="documents">Stock document data access.</param>
    /// <param name="warehouses">Warehouse data access, for codes.</param>
    /// <param name="skus">SKU data access.</param>
    /// <param name="products">Product data access, for names.</param>
    /// <param name="users">User data access, for who did what.</param>
    /// <param name="permissions">Caller's capabilities and scopes.</param>
    /// <param name="conversions">Unit conversion, for the units a SKU can be entered in.</param>
    public StockQueryService(
        IRepository<StockBalance> balances,
        IRepository<StockLedgerEntry> ledger,
        IRepository<StockDocument> documents,
        IRepository<Warehouse> warehouses,
        IRepository<Sku> skus,
        IRepository<Product> products,
        IRepository<User> users,
        IPermissionService permissions,
        IUomConversionProvider conversions)
    {
        _conversions = conversions;
        _balances = balances;
        _ledger = ledger;
        _documents = documents;
        _warehouses = warehouses;
        _skus = skus;
        _products = products;
        _users = users;
        _permissions = permissions;
    }

    /// <inheritdoc />
    public async Task<List<StockWarehouseDto>> GetWarehousesAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<StockWarehouseDto>();
        foreach (Warehouse warehouse in (await AllWarehousesAsync(cancellationToken)).Values.Where(w => w.IsActive).OrderBy(w => w.Code, StringComparer.Ordinal))
        {
            var dto = new StockWarehouseDto
            {
                Id = warehouse.Id,
                Code = warehouse.Code,
                Name = warehouse.Name,
                CanView = await CoversAsync(Capabilities.StockView, warehouse.Id, cancellationToken),
                CanReceive = await CoversAsync(Capabilities.ReceiptsManage, warehouse.Id, cancellationToken),
                CanTransfer = await CoversAsync(Capabilities.TransfersManage, warehouse.Id, cancellationToken),
                CanAdjust = await CoversAsync(Capabilities.StockAdjust, warehouse.Id, cancellationToken),
            };
            if (dto.CanView || dto.CanReceive || dto.CanTransfer || dto.CanAdjust)
            {
                result.Add(dto);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<List<StockWarehouseDto>> GetDestinationsAsync(CancellationToken cancellationToken = default) =>
        (await AllWarehousesAsync(cancellationToken)).Values
            .Where(w => w.IsActive)
            .OrderBy(w => w.Code, StringComparer.Ordinal)
            .Select(w => new StockWarehouseDto { Id = w.Id, Code = w.Code, Name = w.Name })
            .ToList();

    /// <inheritdoc />
    public async Task<List<StockSkuOptionDto>> GetSkuOptionsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Sku> skus = await _skus.ListAsync(_skus.Query(), cancellationToken);
        IReadOnlyDictionary<Guid, Product> products = (await _products.ListAsync(_products.Query(), cancellationToken)).ToDictionary(p => p.Id);
        IUomConversionService converter = await _conversions.GetAsync(cancellationToken);

        return skus
            .Where(s => s.IsActive)
            .OrderBy(s => s.Code, StringComparer.Ordinal)
            .Select(s => new StockSkuOptionDto
            {
                SkuId = s.Id,
                Code = s.Code,
                Name = $"{(products.TryGetValue(s.ProductId, out Product? p) ? p.Name : s.Code)} · {s.VariantLabel}",
                BaseUom = s.BaseUom,
                Units = converter.ReachableUnits(new SkuUnits(s.BaseUom, s.Conversions)).Select(u => u.Uom).ToList(),
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<PagedResult<StockBalanceDto>> GetBalancesAsync(StockBalanceRequest request, CancellationToken cancellationToken = default)
    {
        Query? query = await ScopedAsync(_balances.Query(), Capabilities.StockView, request.WarehouseId, WarehouseIdField, cancellationToken);
        if (query is null)
        {
            return Empty<StockBalanceDto>(request);
        }

        if (request.InStockOnly)
        {
            query = query.WhereEqualTo(IsInStockField, true);
        }

        query = string.IsNullOrWhiteSpace(request.Search)
            ? query.OrderBy(SkuCodeField)
            : query.WhereStartsWith(SkuCodeField, request.Search.Trim().ToUpperInvariant());

        var page = await _balances.GetPagedAsync(query, request, cancellationToken);
        Dictionary<Guid, Warehouse> warehouses = await AllWarehousesAsync(cancellationToken);
        return Map(page, b => ToDto(b, warehouses));
    }

    /// <inheritdoc />
    public async Task<SkuStockDto> GetSkuStockAsync(Guid skuId, CancellationToken cancellationToken = default)
    {
        Sku sku = await _skus.GetByIdAsync(skuId, cancellationToken) ?? throw new NotFoundException("SKU");
        Product? product = await _products.GetByIdAsync(sku.ProductId, cancellationToken);
        IReadOnlySet<Guid>? scope = await _permissions.GetScopeIdsAsync(Capabilities.StockView, ScopeType.Warehouse, cancellationToken);
        Dictionary<Guid, Warehouse> warehouses = await AllWarehousesAsync(cancellationToken);

        List<StockBalanceDto> balances = (await _balances.ListAsync(_balances.Query().WhereEqualTo(SkuIdField, skuId.ToString()), cancellationToken))
            .Where(b => scope is null || scope.Contains(b.WarehouseId))
            .Select(b => ToDto(b, warehouses))
            .OrderBy(b => b.WarehouseCode, StringComparer.Ordinal)
            .ToList();

        return new SkuStockDto
        {
            SkuId = sku.Id,
            SkuCode = sku.Code,
            Name = $"{product?.Name ?? sku.Code} · {sku.VariantLabel}",
            Uom = sku.BaseUom,
            TotalOnHand = balances.Sum(b => b.OnHand),
            Balances = balances,
        };
    }

    /// <inheritdoc />
    public async Task<PagedResult<StockLedgerEntryDto>> GetLedgerAsync(StockLedgerRequest request, CancellationToken cancellationToken = default)
    {
        Query? query = await ScopedAsync(_ledger.Query(), Capabilities.StockView, request.WarehouseId, WarehouseIdField, cancellationToken);
        if (query is null)
        {
            return Empty<StockLedgerEntryDto>(request);
        }

        if (request.SkuId is { } skuId)
        {
            query = query.WhereEqualTo(SkuIdField, skuId.ToString());
        }

        var page = await _ledger.GetPagedAsync(query.OrderByDescending(CreatedAtField), request, cancellationToken);
        Dictionary<Guid, Warehouse> warehouses = await AllWarehousesAsync(cancellationToken);
        IReadOnlyDictionary<Guid, string> names = await UserNamesAsync(page.Items.Select(e => e.CreatedBy), cancellationToken);
        IReadOnlyDictionary<Guid, string> skuCodes = (await _skus.GetByIdsAsync(page.Items.Select(e => e.SkuId), cancellationToken))
            .ToDictionary(s => s.Id, s => s.Code);

        return Map(page, e => new StockLedgerEntryDto
        {
            Id = e.Id,
            CreatedAt = e.CreatedAt,
            CreatedBy = e.CreatedBy,
            WarehouseId = e.WarehouseId,
            WarehouseCode = warehouses.TryGetValue(e.WarehouseId, out Warehouse? w) ? w.Code : string.Empty,
            SkuId = e.SkuId,
            SkuCode = skuCodes.GetValueOrDefault(e.SkuId, string.Empty),
            Quantity = e.Quantity,
            Uom = e.Uom,
            Direction = e.Direction,
            Reason = e.Reason,
            RefId = e.RefId,
            ReferenceNo = e.ReferenceNo,
            ReversesEntryId = e.ReversesEntryId,
            BalanceAfter = e.BalanceAfter,
            CreatedByName = e.CreatedBy is { } by ? names.GetValueOrDefault(by) : null,
        });
    }

    /// <inheritdoc />
    public async Task<PagedResult<StockDocumentDto>> GetDocumentsAsync(StockDocumentListRequest request, CancellationToken cancellationToken = default)
    {
        StockDocumentType type = request.Type ?? throw new FieldValidationException(nameof(request.Type), "Choose a kind of document.");
        string capability = StockService.ViewCapabilityFor(type);
        IReadOnlySet<Guid>? scope = await _permissions.GetScopeIdsAsync(capability, ScopeType.Warehouse, cancellationToken);

        Query query = _documents.Query().WhereEqualTo(TypeField, type.ToString());
        if (request.WarehouseId is { } warehouseId)
        {
            if (scope is not null && !scope.Contains(warehouseId))
            {
                throw new NotFoundException("Warehouse");
            }

            query = query.WhereArrayContains(WarehouseIdsField, warehouseId.ToString());
        }
        else if (scope is not null)
        {
            if (scope.Count == 0)
            {
                return Empty<StockDocumentDto>(request);
            }

            query = query.WhereArrayContainsAny(WarehouseIdsField, scope.Order().Take(InFilterLimit).Select(id => id.ToString()).ToArray());
        }

        var page = await _documents.GetPagedAsync(query.OrderByDescending(CreatedAtField), request, cancellationToken);
        var items = new List<StockDocumentDto>();
        foreach (StockDocument document in page.Items)
        {
            items.Add(await ToDtoAsync(document, cancellationToken));
        }

        return new PagedResult<StockDocumentDto> { Items = items, Page = page.Page, PageSize = page.PageSize, TotalCount = page.TotalCount };
    }

    /// <inheritdoc />
    public async Task<StockDocumentDto> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        StockDocument document = await _documents.GetByIdAsync(documentId, cancellationToken) ?? throw new NotFoundException("Stock document");
        string capability = StockService.ViewCapabilityFor(document.Type);
        foreach (Guid warehouseId in document.WarehouseIds)
        {
            if (await CoversAsync(capability, warehouseId, cancellationToken))
            {
                return await ToDtoAsync(document, cancellationToken);
            }
        }

        throw new NotFoundException("Stock document");
    }

    /// <summary>
    /// Limits a query to the warehouses where the caller holds a capability,
    /// optionally narrowed to one warehouse.
    /// </summary>
    /// <param name="query">Org-scoped query.</param>
    /// <param name="capability">View capability.</param>
    /// <param name="warehouseId">One warehouse, or null for all the caller may see.</param>
    /// <param name="field">Stored warehouse id field.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The narrowed query, or null when the caller can see nothing.</returns>
    /// <exception cref="NotFoundException">The chosen warehouse is outside the caller's scope.</exception>
    private async Task<Query?> ScopedAsync(Query query, string capability, Guid? warehouseId, string field, CancellationToken cancellationToken)
    {
        IReadOnlySet<Guid>? scope = await _permissions.GetScopeIdsAsync(capability, ScopeType.Warehouse, cancellationToken);
        if (warehouseId is { } id)
        {
            return scope is not null && !scope.Contains(id)
                ? throw new NotFoundException("Warehouse")
                : query.WhereEqualTo(field, id.ToString());
        }

        if (scope is null)
        {
            return query;
        }

        return scope.Count == 0
            ? null
            : query.WhereIn(field, scope.Order().Take(InFilterLimit).Select(i => i.ToString()).ToArray());
    }

    /// <summary>
    /// Builds a document's read model, with names, codes and what the caller may do.
    /// </summary>
    /// <param name="document">Document.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The read model.</returns>
    private async Task<StockDocumentDto> ToDtoAsync(StockDocument document, CancellationToken cancellationToken)
    {
        Dictionary<Guid, Warehouse> warehouses = await AllWarehousesAsync(cancellationToken);
        IReadOnlyDictionary<Guid, string> names = await UserNamesAsync(new[] { document.CreatedBy }, cancellationToken);
        string manage = StockService.ManageCapabilityFor(document.Type);

        bool canReceive = document.Type == StockDocumentType.Transfer
            && document.Status == StockDocumentStatus.InTransit
            && document.ToWarehouseId is { } destination
            && await CoversAsync(Capabilities.TransfersManage, destination, cancellationToken);

        bool canReverse = document.Status != StockDocumentStatus.Reversed
            && document.ReversesDocumentId is null
            && await CoversAsync(manage, document.WarehouseId, cancellationToken)
            && (document.Status != StockDocumentStatus.Received || document.ToWarehouseId is not { } to || await CoversAsync(manage, to, cancellationToken));

        return new StockDocumentDto
        {
            Id = document.Id,
            CreatedAt = document.CreatedAt,
            CreatedBy = document.CreatedBy,
            UpdatedAt = document.UpdatedAt,
            UpdatedBy = document.UpdatedBy,
            ReferenceNo = document.ReferenceNo,
            Type = document.Type,
            Status = document.Status,
            Reason = document.Reason,
            WarehouseId = document.WarehouseId,
            WarehouseCode = warehouses.TryGetValue(document.WarehouseId, out Warehouse? source) ? source.Code : string.Empty,
            ToWarehouseId = document.ToWarehouseId,
            ToWarehouseCode = document.ToWarehouseId is { } toId && warehouses.TryGetValue(toId, out Warehouse? target) ? target.Code : null,
            SupplierName = document.SupplierName,
            SupplierReferenceNo = document.SupplierReferenceNo,
            Remarks = document.Remarks,
            Lines = document.Lines.Select(l => new StockDocumentLineDto
            {
                SkuId = l.SkuId,
                SkuCode = l.SkuCode,
                Name = l.Name,
                Quantity = l.Quantity,
                Uom = l.Uom,
                BaseQuantity = l.BaseQuantity,
                BaseUom = l.BaseUom,
            }).ToList(),
            ReceivedAt = document.ReceivedAt,
            ReversesDocumentId = document.ReversesDocumentId,
            ReversedByDocumentId = document.ReversedByDocumentId,
            CreatedByName = document.CreatedBy is { } by ? names.GetValueOrDefault(by) : null,
            CanReceive = canReceive,
            CanReverse = canReverse,
        };
    }

    /// <summary>
    /// A balance's read model.
    /// </summary>
    /// <param name="balance">Balance.</param>
    /// <param name="warehouses">Warehouses by id, for the code.</param>
    /// <returns>The read model.</returns>
    private static StockBalanceDto ToDto(StockBalance balance, IReadOnlyDictionary<Guid, Warehouse> warehouses) => new()
    {
        WarehouseId = balance.WarehouseId,
        WarehouseCode = warehouses.TryGetValue(balance.WarehouseId, out Warehouse? w) ? w.Code : string.Empty,
        SkuId = balance.SkuId,
        SkuCode = balance.SkuCode,
        Name = balance.Name,
        OnHand = balance.OnHand,
        Reserved = balance.Reserved,
        Available = balance.Available(),
        Uom = balance.Uom,
        LastMovedAt = balance.LastMovedAt,
    };

    /// <summary>
    /// Every warehouse of the organisation, loaded once per request.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Warehouses by id.</returns>
    private async Task<Dictionary<Guid, Warehouse>> AllWarehousesAsync(CancellationToken cancellationToken) =>
        _allWarehouses ??= (await _warehouses.ListAsync(_warehouses.Query(), cancellationToken)).ToDictionary(w => w.Id);

    /// <summary>
    /// Names of users, for "posted by".
    /// </summary>
    /// <param name="ids">User ids (nulls ignored).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Name by id.</returns>
    private async Task<IReadOnlyDictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        List<Guid> wanted = ids.OfType<Guid>().Distinct().ToList();
        return wanted.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _users.GetByIdsAsync(wanted, cancellationToken)).ToDictionary(u => u.Id, u => u.Name);
    }

    /// <summary>
    /// Whether the caller holds a capability in one warehouse.
    /// </summary>
    /// <param name="capability">Capability.</param>
    /// <param name="warehouseId">Warehouse.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when covered.</returns>
    private Task<bool> CoversAsync(string capability, Guid warehouseId, CancellationToken cancellationToken) =>
        _permissions.CoversAsync(capability, ScopeType.Warehouse, warehouseId, cancellationToken);

    /// <summary>
    /// Converts a page of entities to a page of read models.
    /// </summary>
    /// <typeparam name="TIn">Entity type.</typeparam>
    /// <typeparam name="TOut">Read model type.</typeparam>
    /// <param name="page">Page of entities.</param>
    /// <param name="map">Conversion.</param>
    /// <returns>The page of read models.</returns>
    private static PagedResult<TOut> Map<TIn, TOut>(PagedResult<TIn> page, Func<TIn, TOut> map) => new()
    {
        Items = page.Items.Select(map).ToList(),
        Page = page.Page,
        PageSize = page.PageSize,
        TotalCount = page.TotalCount,
    };

    /// <summary>
    /// An empty page, for a caller who can see nothing.
    /// </summary>
    /// <typeparam name="T">Row type.</typeparam>
    /// <param name="request">Paging requested.</param>
    /// <returns>An empty page.</returns>
    private static PagedResult<T> Empty<T>(PagedRequest request) => new() { Page = request.Page, PageSize = request.PageSize };
}
