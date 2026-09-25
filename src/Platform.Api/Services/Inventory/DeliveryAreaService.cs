using Google.Cloud.Firestore;
using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Mapping;
using Platform.Api.Repositories;
using Platform.Api.Security.Authorization;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Inventory;
using Platform.Shared.Entities.Identity;
using Platform.Shared.Entities.Inventory;

namespace Platform.Api.Services.Inventory;

/// <summary>
/// PIN codes each warehouse delivers to. Warehouse-scoped (P6): staff see and
/// change only the areas of warehouses where they hold the delivery feature;
/// anything else is 404.
/// </summary>
public interface IDeliveryAreaService
{
    /// <summary>
    /// Lists active areas, by PIN code, optionally for one warehouse and a PIN code prefix.
    /// </summary>
    /// <param name="request">Filter and page.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One page of areas.</returns>
    Task<PagedResult<DeliveryAreaDto>> ListAsync(DeliveryAreaListRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds PIN codes to a warehouse, or updates the lead time of ones it already has.
    /// </summary>
    /// <param name="request">Warehouse, PIN codes, lead time.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>How many were added and updated.</returns>
    Task<AddDeliveryAreasResult> AddAsync(AddDeliveryAreasRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes one area's lead time, or removes / restores it.
    /// </summary>
    /// <param name="id">Area id.</param>
    /// <param name="request">New values.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The area.</returns>
    Task<DeliveryAreaDto> UpdateAsync(Guid id, UpdateDeliveryAreaRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops delivering to a PIN code from a warehouse.
    /// </summary>
    /// <param name="id">Area id.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when saved.</returns>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Firestore-backed <see cref="IDeliveryAreaService"/>.
/// </summary>
public sealed class DeliveryAreaService : IDeliveryAreaService
{
    /// <summary>Writes per commit; Firestore allows 500 including the audit entries.</summary>
    private const int WritesPerCommit = 200;

    private static readonly string WarehouseIdField = FirestoreNaming.Field(nameof(DeliveryArea.WarehouseId));
    private static readonly string PincodeField = FirestoreNaming.Field(nameof(DeliveryArea.Pincode));
    private static readonly string IsActiveField = FirestoreNaming.Field(nameof(DeliveryArea.IsActive));

    private readonly IRepository<DeliveryArea> _areas;
    private readonly IRepository<Warehouse> _warehouses;
    private readonly IPermissionService _permissions;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="areas">Delivery area data access.</param>
    /// <param name="warehouses">Warehouse data access.</param>
    /// <param name="permissions">Caller's capabilities and scopes.</param>
    /// <param name="unitOfWork">Commits changes.</param>
    public DeliveryAreaService(
        IRepository<DeliveryArea> areas,
        IRepository<Warehouse> warehouses,
        IPermissionService permissions,
        IUnitOfWork unitOfWork)
    {
        _areas = areas;
        _warehouses = warehouses;
        _permissions = permissions;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<PagedResult<DeliveryAreaDto>> ListAsync(DeliveryAreaListRequest request, CancellationToken cancellationToken = default)
    {
        IReadOnlySet<Guid>? scope = await _permissions.GetScopeIdsAsync(Capabilities.DeliveryView, ScopeType.Warehouse, cancellationToken);
        if (request.WarehouseId is { } only && scope is not null && !scope.Contains(only))
        {
            throw new NotFoundException("Warehouse");
        }

        Query query = _areas.Query().WhereEqualTo(IsActiveField, true);
        if (request.WarehouseId is { } warehouseId)
        {
            query = query.WhereEqualTo(WarehouseIdField, DocumentConverter.ToFirestoreValue(warehouseId));
        }

        query = string.IsNullOrWhiteSpace(request.Search)
            ? query.OrderBy(PincodeField)
            : query.WhereStartsWith(PincodeField, request.Search.Trim());

        PagedResult<DeliveryArea> page;
        if (scope is null || request.WarehouseId is not null)
        {
            page = await _areas.GetPagedAsync(query, request, cancellationToken);
        }
        else
        {
            // Limited to some warehouses: filter in memory so paging counts only what the caller may see.
            List<DeliveryArea> visible = (await _areas.ListAsync(query, cancellationToken)).Where(a => scope.Contains(a.WarehouseId)).ToList();
            page = new PagedResult<DeliveryArea>
            {
                Items = visible.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList(),
                Page = request.Page,
                PageSize = request.PageSize,
                TotalCount = visible.Count,
            };
        }

        var warehouses = (await _warehouses.GetByIdsAsync(page.Items.Select(a => a.WarehouseId), cancellationToken)).ToDictionary(w => w.Id);
        return new PagedResult<DeliveryAreaDto>
        {
            Items = page.Items.Select(a => ToDto(a, warehouses.GetValueOrDefault(a.WarehouseId))).ToList(),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
        };
    }

    /// <inheritdoc />
    public async Task<AddDeliveryAreasResult> AddAsync(AddDeliveryAreasRequest request, CancellationToken cancellationToken = default)
    {
        Warehouse warehouse = await LoadWarehouseAsync(request.WarehouseId, cancellationToken);
        await RequireManageAsync(warehouse.Id, cancellationToken);

        var existing = (await _areas.GetByIdsAsync(request.Pincodes.Select(p => DeliveryArea.IdFor(warehouse.Id, p)), cancellationToken))
            .ToDictionary(a => a.Id);
        var result = new AddDeliveryAreasResult();
        int staged = 0;
        foreach (string pincode in request.Pincodes)
        {
            Guid id = DeliveryArea.IdFor(warehouse.Id, pincode);
            if (existing.TryGetValue(id, out DeliveryArea? area))
            {
                if (area.IsActive && area.LeadTimeDays == request.LeadTimeDays)
                {
                    continue;
                }

                area.IsActive = true;
                area.LeadTimeDays = request.LeadTimeDays;
                _areas.Update(area);
                result.Updated++;
            }
            else
            {
                _areas.Add(new DeliveryArea { Id = id, WarehouseId = warehouse.Id, Pincode = pincode, LeadTimeDays = request.LeadTimeDays });
                result.Added++;
            }

            if (++staged % WritesPerCommit == 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return result;
    }

    /// <inheritdoc />
    public async Task<DeliveryAreaDto> UpdateAsync(Guid id, UpdateDeliveryAreaRequest request, CancellationToken cancellationToken = default)
    {
        DeliveryArea area = await LoadAsync(id, cancellationToken);
        await RequireManageAsync(area.WarehouseId, cancellationToken);
        area.LeadTimeDays = request.LeadTimeDays;
        area.IsActive = request.IsActive;
        _areas.Update(area);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(area, await _warehouses.GetByIdAsync(area.WarehouseId, cancellationToken));
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        DeliveryArea area = await LoadAsync(id, cancellationToken);
        await RequireManageAsync(area.WarehouseId, cancellationToken);
        area.IsActive = false;
        _areas.Update(area);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Loads an area the caller may see, or fails with 404.
    /// </summary>
    /// <param name="id">Area id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The area.</returns>
    private async Task<DeliveryArea> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        DeliveryArea? area = await _areas.GetByIdAsync(id, cancellationToken);
        return area is not null && await _permissions.CoversAsync(Capabilities.DeliveryView, ScopeType.Warehouse, area.WarehouseId, cancellationToken)
            ? area
            : throw new NotFoundException("Delivery area");
    }

    /// <summary>
    /// Loads an active warehouse the caller may see, or fails with 404.
    /// </summary>
    /// <param name="id">Warehouse id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The warehouse.</returns>
    private async Task<Warehouse> LoadWarehouseAsync(Guid id, CancellationToken cancellationToken)
    {
        Warehouse? warehouse = await _warehouses.GetByIdAsync(id, cancellationToken);
        return warehouse is { IsActive: true } && await _permissions.CoversAsync(Capabilities.DeliveryView, ScopeType.Warehouse, id, cancellationToken)
            ? warehouse
            : throw new NotFoundException("Warehouse");
    }

    /// <summary>
    /// Refuses a change unless the caller manages delivery areas of the warehouse.
    /// </summary>
    /// <param name="warehouseId">Warehouse.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when allowed.</returns>
    private async Task RequireManageAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        if (!await _permissions.CoversAsync(Capabilities.DeliveryManage, ScopeType.Warehouse, warehouseId, cancellationToken))
        {
            throw new ForbiddenException("You cannot change the delivery areas of this warehouse.");
        }
    }

    /// <summary>
    /// Maps an area with its warehouse's code and name.
    /// </summary>
    /// <param name="area">Area.</param>
    /// <param name="warehouse">Its warehouse, if found.</param>
    /// <returns>The DTO.</returns>
    private static DeliveryAreaDto ToDto(DeliveryArea area, Warehouse? warehouse) => new DeliveryAreaDto
    {
        WarehouseId = area.WarehouseId,
        WarehouseCode = warehouse?.Code ?? string.Empty,
        WarehouseName = warehouse?.Name ?? string.Empty,
        Pincode = area.Pincode,
        LeadTimeDays = area.LeadTimeDays,
        IsActive = area.IsActive,
    }.WithAuditFrom(area);
}
