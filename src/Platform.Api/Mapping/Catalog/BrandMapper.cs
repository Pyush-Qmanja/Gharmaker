using Platform.Api.Mapping;
using Platform.Shared.Dtos.Catalog;
using Platform.Shared.Entities.Catalog;

namespace Platform.Api.Mapping.Catalog;

/// <summary>
/// Maps <see cref="Brand"/> to and from its DTOs.
/// </summary>
public sealed class BrandMapper : IEntityMapper<Brand, BrandDto, CreateBrandRequest, UpdateBrandRequest>
{
    /// <inheritdoc />
    public BrandDto ToDto(Brand entity) => new BrandDto
    {
        Name = entity.Name,
        Slug = entity.Slug,
        LogoUrl = entity.LogoUrl,
        ManufacturerName = entity.ManufacturerName,
        DisplayOrder = entity.DisplayOrder,
        IsActive = entity.IsActive,
    }.WithAuditFrom(entity);

    /// <inheritdoc />
    public Brand ToEntity(CreateBrandRequest request) => new()
    {
        Name = DtoMapping.Clean(request.Name),
        Slug = DtoMapping.Clean(request.Slug),
        LogoUrl = DtoMapping.CleanOptional(request.LogoUrl),
        ManufacturerName = DtoMapping.CleanOptional(request.ManufacturerName),
        DisplayOrder = request.DisplayOrder,
    };

    /// <inheritdoc />
    public void Apply(UpdateBrandRequest request, Brand entity)
    {
        entity.Name = DtoMapping.Clean(request.Name);
        entity.Slug = DtoMapping.Clean(request.Slug);
        entity.LogoUrl = DtoMapping.CleanOptional(request.LogoUrl);
        entity.ManufacturerName = DtoMapping.CleanOptional(request.ManufacturerName);
        entity.DisplayOrder = request.DisplayOrder;
        entity.IsActive = request.IsActive;
    }
}
