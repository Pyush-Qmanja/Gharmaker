using Microsoft.AspNetCore.Mvc;
using Platform.Api.Security.Authorization;
using Platform.Api.Services;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Inventory;

namespace Platform.Api.Controllers.Inventory;

/// <summary>
/// Warehouses: <c>/api/warehouses</c>. Internal only — never add a storefront
/// route here (P1). All endpoints come from <see cref="CrudControllerBase{TDto,TCreate,TUpdate}"/>;
/// scope filtering happens in <c>WarehouseService</c>.
/// </summary>
[Route(ApiRoutes.Warehouses)]
[CrudCapabilities(Capabilities.WarehousesView, Capabilities.WarehousesManage)]
public sealed class WarehousesController : CrudControllerBase<WarehouseDto, CreateWarehouseRequest, UpdateWarehouseRequest>
{
    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="service">Warehouse operations.</param>
    public WarehousesController(ICrudService<WarehouseDto, CreateWarehouseRequest, UpdateWarehouseRequest> service)
        : base(service)
    {
    }
}
