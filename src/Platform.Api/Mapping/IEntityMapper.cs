using Platform.Shared.Entities.Common;

namespace Platform.Api.Mapping;

/// <summary>
/// Converts between an entity and its DTOs. One small, explicit mapper per
/// entity keeps mapping visible and testable, with no reflection magic.
/// </summary>
/// <typeparam name="TEntity">Persisted entity.</typeparam>
/// <typeparam name="TDto">Read model returned to callers.</typeparam>
/// <typeparam name="TCreate">Create request body.</typeparam>
/// <typeparam name="TUpdate">Update request body.</typeparam>
public interface IEntityMapper<TEntity, TDto, in TCreate, in TUpdate>
    where TEntity : BaseEntity
{
    /// <summary>
    /// Builds the read model of an entity.
    /// </summary>
    /// <param name="entity">Source entity.</param>
    /// <returns>A new DTO.</returns>
    TDto ToDto(TEntity entity);

    /// <summary>
    /// Builds a new entity from a create request. Audit and tenancy fields are
    /// left unset; the data layer fills them.
    /// </summary>
    /// <param name="request">Validated create request.</param>
    /// <returns>A new, unsaved entity.</returns>
    TEntity ToEntity(TCreate request);

    /// <summary>
    /// Copies the fields of an update request onto an existing entity.
    /// </summary>
    /// <param name="request">Validated update request.</param>
    /// <param name="entity">Tracked entity to modify.</param>
    void Apply(TUpdate request, TEntity entity);
}
