using FluentValidation;
using Platform.Shared.Dtos.Catalog;
using Platform.Web.Services.Api;

namespace Platform.Web.Controllers;

/// <summary>
/// Brand screens. Everything is inherited from <see cref="CrudController{TDto,TCreate,TUpdate}"/>;
/// the brand-specific markup is in <c>Views/Brands/_Table.cshtml</c> and <c>_Form.cshtml</c>.
/// </summary>
public sealed class BrandsController : CrudController<BrandDto, CreateBrandRequest, UpdateBrandRequest>
{
    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="api">Brand API client.</param>
    /// <param name="createValidator">Shared create validator.</param>
    /// <param name="updateValidator">Shared update validator.</param>
    public BrandsController(
        ICrudApiClient<BrandDto, CreateBrandRequest, UpdateBrandRequest> api,
        IValidator<CreateBrandRequest> createValidator,
        IValidator<UpdateBrandRequest> updateValidator)
        : base(api, createValidator, updateValidator)
    {
    }

    /// <inheritdoc />
    protected override string SingularName => "Brand";

    /// <inheritdoc />
    protected override string PluralName => "Brands";

    /// <inheritdoc />
    protected override UpdateBrandRequest ToUpdateRequest(BrandDto dto) => new()
    {
        Name = dto.Name,
        Slug = dto.Slug,
        LogoUrl = dto.LogoUrl,
        ManufacturerName = dto.ManufacturerName,
        DisplayOrder = dto.DisplayOrder,
        IsActive = dto.IsActive,
    };
}
