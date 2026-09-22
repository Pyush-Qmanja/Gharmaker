using Microsoft.AspNetCore.Mvc;
using Platform.Api.Services;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Catalog;

namespace Platform.Api.Controllers.Catalog;

/// <summary>
/// Brand master data: <c>/api/brands</c>. All endpoints come from
/// <see cref="CrudControllerBase{TDto,TCreate,TUpdate}"/>.
/// </summary>
[Route(ApiRoutes.Brands)]
public sealed class BrandsController : CrudControllerBase<BrandDto, CreateBrandRequest, UpdateBrandRequest>
{
    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="service">Brand operations.</param>
    public BrandsController(ICrudService<BrandDto, CreateBrandRequest, UpdateBrandRequest> service)
        : base(service)
    {
    }
}
