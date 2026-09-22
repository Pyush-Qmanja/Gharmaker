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
