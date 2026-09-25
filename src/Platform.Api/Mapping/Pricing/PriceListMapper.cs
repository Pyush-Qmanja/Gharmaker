using Platform.Shared.Dtos.Pricing;
using Platform.Shared.Entities.Pricing;

namespace Platform.Api.Mapping.Pricing;

/// <summary>
/// Maps price lists between entity and DTOs. Codes are stored upper-case.
/// </summary>
public sealed class PriceListMapper : IEntityMapper<PriceList, PriceListDto, CreatePriceListRequest, UpdatePriceListRequest>
{
    /// <inheritdoc />
    public PriceListDto ToDto(PriceList entity) => new PriceListDto
    {
        Name = entity.Name,
        Code = entity.Code,
        Type = entity.Type,
        CustomerId = entity.CustomerId,
        Currency = entity.Currency,
        Remarks = entity.Remarks,
        IsActive = entity.IsActive,
    }.WithAuditFrom(entity);

    /// <inheritdoc />
    public PriceList ToEntity(CreatePriceListRequest request) => new()
    {
        Name = DtoMapping.Clean(request.Name),
        Code = DtoMapping.Clean(request.Code).ToUpperInvariant(),
        Type = request.Type,
        CustomerId = request.CustomerId,
        Remarks = DtoMapping.CleanOptional(request.Remarks),
    };

    /// <inheritdoc />
    public void Apply(UpdatePriceListRequest request, PriceList entity)
    {
        entity.Name = DtoMapping.Clean(request.Name);
        entity.Code = DtoMapping.Clean(request.Code).ToUpperInvariant();
        entity.Type = request.Type;
        entity.CustomerId = request.CustomerId;
        entity.Remarks = DtoMapping.CleanOptional(request.Remarks);
        entity.IsActive = request.IsActive;
    }
}
