using Platform.Shared.Dtos.Inventory;
using Platform.Shared.Entities.Inventory;

namespace Platform.Api.Mapping.Inventory;

/// <summary>
/// Maps <see cref="Warehouse"/> to and from its DTOs.
/// </summary>
public sealed class WarehouseMapper : IEntityMapper<Warehouse, WarehouseDto, CreateWarehouseRequest, UpdateWarehouseRequest>
{
    /// <inheritdoc />
    public WarehouseDto ToDto(Warehouse entity) => new WarehouseDto
    {
        Code = entity.Code,
        Name = entity.Name,
        Type = entity.Type,
        Address = DtoMapping.ToAddressDto(entity.Address),
        Lat = entity.Lat,
        Lng = entity.Lng,
        OwnerUserId = entity.OwnerUserId,
        IsActive = entity.IsActive,
    }.WithAuditFrom(entity);

    /// <inheritdoc />
    public Warehouse ToEntity(CreateWarehouseRequest request) => new()
    {
        Code = NormaliseCode(request.Code),
        Name = DtoMapping.Clean(request.Name),
        Type = request.Type,
        Address = DtoMapping.ToAddress(request.Address),
        Lat = request.Lat,
        Lng = request.Lng,
        OwnerUserId = request.OwnerUserId,
    };

    /// <inheritdoc />
    public void Apply(UpdateWarehouseRequest request, Warehouse entity)
    {
        entity.Code = NormaliseCode(request.Code);
        entity.Name = DtoMapping.Clean(request.Name);
        entity.Type = request.Type;
        entity.Address = DtoMapping.ToAddress(request.Address);
        entity.Lat = request.Lat;
        entity.Lng = request.Lng;
        entity.OwnerUserId = request.OwnerUserId;
        entity.IsActive = request.IsActive;
    }

    /// <summary>
    /// Codes are stored trimmed and upper-case so uniqueness checks and prefix search are exact.
    /// </summary>
    /// <param name="code">Requested code.</param>
    /// <returns>The normalised code.</returns>
    private static string NormaliseCode(string code) => code.Trim().ToUpperInvariant();
}
