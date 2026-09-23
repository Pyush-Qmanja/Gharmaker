using Platform.Shared.Dtos.Common;
using Platform.Shared.Entities.Common;

namespace Platform.Api.Mapping;

/// <summary>
/// Mapping helpers reused by every entity mapper.
/// </summary>
public static class DtoMapping
{
    /// <summary>
    /// Copies the key and audit fields from an entity to a DTO so no mapper
    /// repeats them.
    /// </summary>
    /// <typeparam name="TDto">Read model type.</typeparam>
    /// <param name="dto">Destination DTO.</param>
    /// <param name="entity">Source entity.</param>
    /// <returns>The same DTO, for chaining.</returns>
    public static TDto WithAuditFrom<TDto>(this TDto dto, BaseEntity entity) where TDto : EntityDto
    {
        dto.Id = entity.Id;
        dto.CreatedAt = entity.CreatedAt;
        dto.CreatedBy = entity.CreatedBy;
        dto.UpdatedAt = entity.UpdatedAt;
        dto.UpdatedBy = entity.UpdatedBy;
        return dto;
    }

    /// <summary>
    /// Converts a requested address to the stored value object, trimming every field.
    /// </summary>
    /// <param name="address">Validated address from a request.</param>
    /// <returns>The value object to store.</returns>
    public static Address ToAddress(AddressDto address) => new()
    {
        Line1 = Clean(address.Line1),
        Line2 = CleanOptional(address.Line2),
        City = Clean(address.City),
        State = Clean(address.State),
        Pincode = Clean(address.Pincode),
    };

    /// <summary>
    /// Converts a stored address to its DTO.
    /// </summary>
    /// <param name="address">Stored value object.</param>
    /// <returns>The DTO.</returns>
    public static AddressDto ToAddressDto(Address address) => new()
    {
        Line1 = address.Line1,
        Line2 = address.Line2,
        City = address.City,
        State = address.State,
        Pincode = address.Pincode,
    };

    /// <summary>
    /// Trims a required string.
    /// </summary>
    /// <param name="value">Input value.</param>
    /// <returns>The trimmed value.</returns>
    public static string Clean(string value) => value.Trim();

    /// <summary>
    /// Trims an optional string and turns blank input into null.
    /// </summary>
    /// <param name="value">Input value.</param>
    /// <returns>The trimmed value, or null when blank.</returns>
    public static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
