using Google.Cloud.Firestore;
using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Mapping;
using Platform.Api.Repositories;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Entities.Common;

namespace Platform.Api.Services;

/// <summary>
/// Standard list / get / create / update / deactivate operations, expressed
/// only in DTOs so controllers never see an entity.
/// </summary>
/// <typeparam name="TDto">Read model.</typeparam>
/// <typeparam name="TCreate">Create request body.</typeparam>
/// <typeparam name="TUpdate">Update request body.</typeparam>
public interface ICrudService<TDto, in TCreate, in TUpdate>
{
    /// <summary>
    /// Returns one page of records, optionally filtered by <see cref="PagedRequest.Search"/>.
    /// </summary>
    /// <param name="request">Paging and search parameters.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The requested page.</returns>
    Task<PagedResult<TDto>> GetPagedAsync(PagedRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one record.
    /// </summary>
    /// <param name="id">Primary key.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The record.</returns>
    /// <exception cref="NotFoundException">Absent or out of scope.</exception>
    Task<TDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a record.
    /// </summary>
    /// <param name="request">Validated create request.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The created record.</returns>
    /// <exception cref="ConflictException">A unique value is already used.</exception>
    Task<TDto> CreateAsync(TCreate request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a record.
    /// </summary>
    /// <param name="id">Primary key.</param>
    /// <param name="request">Validated update request.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The updated record.</returns>
    /// <exception cref="NotFoundException">Absent or out of scope.</exception>
    /// <exception cref="ConflictException">A unique value is already used.</exception>
    Task<TDto> UpdateAsync(Guid id, TUpdate request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates a master record. Records that are not soft-deletable
    /// (transactions) are never deleted and the call is refused.
    /// </summary>
    /// <param name="id">Primary key.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>A task that completes once saved.</returns>
    /// <exception cref="NotFoundException">Absent or out of scope.</exception>
    /// <exception cref="BusinessRuleException">The entity type cannot be deleted.</exception>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Generic implementation of <see cref="ICrudService{TDto,TCreate,TUpdate}"/>.
/// An entity gets full CRUD by deriving from this class and, at most,
/// overriding <see cref="ApplySearch"/>, <see cref="ApplyOrder"/> and
/// <see cref="EnsureUniqueAsync"/>.
/// </summary>
/// <typeparam name="TEntity">Persisted entity.</typeparam>
/// <typeparam name="TDto">Read model.</typeparam>
/// <typeparam name="TCreate">Create request body.</typeparam>
/// <typeparam name="TUpdate">Update request body.</typeparam>
public abstract class CrudService<TEntity, TDto, TCreate, TUpdate> : ICrudService<TDto, TCreate, TUpdate>
    where TEntity : BaseEntity, new()
{
    /// <summary>Data access for <typeparamref name="TEntity"/>.</summary>
    protected readonly IRepository<TEntity> Repository;

    /// <summary>Commits staged changes.</summary>
    protected readonly IUnitOfWork UnitOfWork;

    /// <summary>Entity/DTO conversion.</summary>
    protected readonly IEntityMapper<TEntity, TDto, TCreate, TUpdate> Mapper;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="repository">Data access for the entity.</param>
    /// <param name="unitOfWork">Commits staged changes.</param>
    /// <param name="mapper">Entity/DTO conversion.</param>
    protected CrudService(
        IRepository<TEntity> repository,
        IUnitOfWork unitOfWork,
        IEntityMapper<TEntity, TDto, TCreate, TUpdate> mapper)
    {
        Repository = repository;
        UnitOfWork = unitOfWork;
        Mapper = mapper;
    }

    /// <summary>Human name used in error messages, e.g. "Brand".</summary>
    protected virtual string EntityName => typeof(TEntity).Name;

    /// <inheritdoc />
    public virtual async Task<PagedResult<TDto>> GetPagedAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        Query query = string.IsNullOrWhiteSpace(request.Search)
            ? ApplyOrder(Repository.Query())
            : ApplySearch(Repository.Query(), request.Search.Trim());

        PagedResult<TEntity> page = await Repository.GetPagedAsync(query, request, cancellationToken);
        return new PagedResult<TDto>
        {
            Items = page.Items.Select(Mapper.ToDto).ToList(),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
        };
    }

    /// <inheritdoc />
    public virtual async Task<TDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Mapper.ToDto(await LoadAsync(id, cancellationToken));

    /// <inheritdoc />
    public virtual async Task<TDto> CreateAsync(TCreate request, CancellationToken cancellationToken = default)
    {
        TEntity entity = Mapper.ToEntity(request);
        await BeforeWriteAsync(entity, isNew: true, cancellationToken);
        await EnsureUniqueAsync(entity, cancellationToken);
        Repository.Add(entity);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
        return Mapper.ToDto(entity);
    }

    /// <inheritdoc />
    public virtual async Task<TDto> UpdateAsync(Guid id, TUpdate request, CancellationToken cancellationToken = default)
    {
        TEntity entity = await LoadAsync(id, cancellationToken);
        Mapper.Apply(request, entity);
        await BeforeWriteAsync(entity, isNew: false, cancellationToken);
        await EnsureUniqueAsync(entity, cancellationToken);
        Repository.Update(entity);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
        return Mapper.ToDto(entity);
    }

    /// <inheritdoc />
    public virtual async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        TEntity entity = await LoadAsync(id, cancellationToken);
        if (entity is not ISoftDeletable softDeletable)
        {
            throw new BusinessRuleException($"{EntityName} records cannot be deleted.");
        }

        await BeforeDeleteAsync(entity, cancellationToken);
        softDeletable.IsActive = false;
        Repository.Update(entity);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Filters a list query by free text. Firestore has no "contains" search, so
    /// implementations typically do a prefix match on one normalised field and
    /// must order by that same field. Default: no filtering, default order.
    /// </summary>
    /// <param name="query">Org-scoped query.</param>
    /// <param name="search">Trimmed, non-empty search text.</param>
    /// <returns>The filtered, ordered query.</returns>
    protected virtual Query ApplySearch(Query query, string search) => ApplyOrder(query);

    /// <summary>
    /// Orders a list query when not searching. Default: newest first.
    /// Every ordering combined with the org filter needs a composite index in
    /// <c>firestore.indexes.json</c>.
    /// </summary>
    /// <param name="query">Org-scoped query.</param>
    /// <returns>The ordered query.</returns>
    protected virtual Query ApplyOrder(Query query) =>
        query.OrderByDescending(FirestoreNaming.Field(nameof(BaseEntity.CreatedAt)));

    /// <summary>
    /// Last check before a create or update is saved, after the request has been
    /// applied to the entity (e.g. scope checks). Default: nothing.
    /// </summary>
    /// <param name="entity">Entity about to be written.</param>
    /// <param name="isNew">True for a create.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task that completes when the write may go ahead.</returns>
    protected virtual Task BeforeWriteAsync(TEntity entity, bool isNew, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Last check before a record is deactivated (e.g. scope checks). Default: nothing.
    /// </summary>
    /// <param name="entity">Entity about to be deactivated.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task that completes when the deactivation may go ahead.</returns>
    protected virtual Task BeforeDeleteAsync(TEntity entity, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Enforces unique values before a write. Default: nothing is unique.
    /// Override and call <see cref="RequireUniqueAsync"/> per unique field.
    /// </summary>
    /// <param name="entity">Entity about to be written.</param>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <returns>A task that completes when all checks pass.</returns>
    protected virtual Task EnsureUniqueAsync(TEntity entity, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Throws 409 when another record in scope already has this value.
    /// </summary>
    /// <param name="entity">Entity about to be written.</param>
    /// <param name="propertyName">C# property name, via <c>nameof</c>.</param>
    /// <param name="value">Value that must be unique.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task that completes when the value is free.</returns>
    /// <exception cref="ConflictException">The value is taken.</exception>
    protected async Task RequireUniqueAsync(TEntity entity, string propertyName, object? value, CancellationToken cancellationToken)
    {
        if (await Repository.ExistsAsync(propertyName, value, entity.Id, cancellationToken))
        {
            throw new ConflictException(propertyName, $"Another {EntityName.ToLowerInvariant()} already uses this {propertyName.ToLowerInvariant()}.");
        }
    }

    /// <summary>
    /// Loads an entity or throws 404.
    /// </summary>
    /// <param name="id">Primary key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The entity.</returns>
    /// <exception cref="NotFoundException">Absent or out of scope.</exception>
    protected virtual async Task<TEntity> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await Repository.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException(EntityName);
}
