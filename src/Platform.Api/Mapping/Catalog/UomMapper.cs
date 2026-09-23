using Platform.Shared.Dtos.Catalog;
using Platform.Shared.Entities.Catalog;

namespace Platform.Api.Mapping.Catalog;

/// <summary>
/// Maps <see cref="Uom"/> to and from its DTOs. Codes are stored upper-case.
/// </summary>
public sealed class UomMapper : IEntityMapper<Uom, UomDto, CreateUomRequest, UpdateUomRequest>
{
    /// <inheritdoc />
    public UomDto ToDto(Uom entity) => new UomDto
    {
        Code = entity.Code,
        Name = entity.Name,
        Dimension = entity.Dimension,
        BaseFactor = entity.BaseFactor,
        IsActive = entity.IsActive,
    }.WithAuditFrom(entity);

    /// <inheritdoc />
    public Uom ToEntity(CreateUomRequest request) => new()
    {
        Code = request.Code.Trim().ToUpperInvariant(),
        Name = DtoMapping.Clean(request.Name),
        Dimension = request.Dimension,
        BaseFactor = request.BaseFactor,
    };

    /// <inheritdoc />
    public void Apply(UpdateUomRequest request, Uom entity)
    {
        entity.Code = request.Code.Trim().ToUpperInvariant();
        entity.Name = DtoMapping.Clean(request.Name);
        entity.Dimension = request.Dimension;
        entity.BaseFactor = request.BaseFactor;
        entity.IsActive = request.IsActive;
    }
}
