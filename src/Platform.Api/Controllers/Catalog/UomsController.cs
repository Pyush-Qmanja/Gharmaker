using Microsoft.AspNetCore.Mvc;
using Platform.Api.Security.Authorization;
using Platform.Api.Services;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Catalog;

namespace Platform.Api.Controllers.Catalog;

/// <summary>
/// Units of measure: <c>/api/uoms</c>. All endpoints come from
/// <see cref="CrudControllerBase{TDto,TCreate,TUpdate}"/>.
/// </summary>
[Route(ApiRoutes.Uoms)]
[CrudCapabilities(Capabilities.CatalogView, Capabilities.CatalogManage)]
public sealed class UomsController : CrudControllerBase<UomDto, CreateUomRequest, UpdateUomRequest>
{
    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="service">Unit operations.</param>
    public UomsController(ICrudService<UomDto, CreateUomRequest, UpdateUomRequest> service)
        : base(service)
    {
    }
}
