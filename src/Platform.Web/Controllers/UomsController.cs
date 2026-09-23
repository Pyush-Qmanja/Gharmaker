using FluentValidation;
using Platform.Shared.Dtos.Catalog;
using Platform.Web.Services.Api;

namespace Platform.Web.Controllers;

/// <summary>
/// Units of measure screens. Everything is inherited from <see cref="CrudController{TDto,TCreate,TUpdate}"/>.
/// </summary>
public sealed class UomsController : CrudController<UomDto, CreateUomRequest, UpdateUomRequest>
{
    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="api">Unit API client.</param>
    /// <param name="createValidator">Shared create validator.</param>
    /// <param name="updateValidator">Shared update validator.</param>
    public UomsController(
        ICrudApiClient<UomDto, CreateUomRequest, UpdateUomRequest> api,
        IValidator<CreateUomRequest> createValidator,
        IValidator<UpdateUomRequest> updateValidator)
        : base(api, createValidator, updateValidator)
    {
    }

    /// <inheritdoc />
    protected override string SingularName => "Unit";

    /// <inheritdoc />
    protected override string PluralName => "Units";

    /// <inheritdoc />
    protected override UpdateUomRequest ToUpdateRequest(UomDto dto) => new()
    {
        Code = dto.Code,
        Name = dto.Name,
        Dimension = dto.Dimension,
        BaseFactor = dto.BaseFactor,
        IsActive = dto.IsActive,
    };
}
