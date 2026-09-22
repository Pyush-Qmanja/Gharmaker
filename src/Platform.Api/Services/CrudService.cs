using Platform.Api.Common;
using Platform.Api.Common.Exceptions;
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
    Task<TDto> CreateAsync(TCreate request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a record.
    /// </summary>
    /// <param name="id">Primary key.</param>
    /// <param name="request">Validated update request.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The updated record.</returns>
    /// <exception cref="NotFoundException">Absent or out of scope.</exception>
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
/// overriding <see cref="ApplySearch"/> and <see cref="ApplyOrder"/>.
/// </summary>
/// <typeparam name="TEntity">Persisted entity.</typeparam>
/// <typeparam name="TDto">Read model.</typeparam>
/// <typeparam name="TCreate">Create request body.</typeparam>
/// <typeparam name="TUpdate">Update request body.</typeparam>
public abstract class CrudService<TEntity, TDto, TCreate, TUpdate> : ICrudService<TDto, TCreate, TUpdate>
    where TEntity : BaseEntity
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
    public virtual Task<PagedResult<TDto>> GetPagedAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        IQueryable<TEntity> query = Repository.Query();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = ApplySearch(query, request.Search.Trim());
        }

        return ApplyOrder(query).ToPagedResultAsync(request, Mapper.ToDto, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Mapper.ToDto(await LoadAsync(id, cancellationToken));

    /// <inheritdoc />
    public virtual async Task<TDto> CreateAsync(TCreate request, CancellationToken cancellationToken = default)
    {
        TEntity entity = Mapper.ToEntity(request);
        await Repository.AddAsync(entity, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
        return Mapper.ToDto(entity);
    }

    /// <inheritdoc />
    public virtual async Task<TDto> UpdateAsync(Guid id, TUpdate request, CancellationToken cancellationToken = default)
    {
        TEntity entity = await LoadAsync(id, cancellationToken);
        Mapper.Apply(request, entity);
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

        softDeletable.IsActive = false;
        await UnitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Filters a list query by free text. Default: no filtering.
    /// </summary>
    /// <param name="query">Query to filter.</param>
    /// <param name="search">Trimmed, non-empty search text.</param>
    /// <returns>The filtered query.</returns>
    protected virtual IQueryable<TEntity> ApplySearch(IQueryable<TEntity> query, string search) => query;

    /// <summary>
    /// Orders a list query. Default: newest first. Paging requires a stable order.
    /// </summary>
    /// <param name="query">Query to order.</param>
    /// <returns>The ordered query.</returns>
    protected virtual IOrderedQueryable<TEntity> ApplyOrder(IQueryable<TEntity> query) =>
        query.OrderByDescending(e => e.CreatedAt).ThenBy(e => e.Id);

    /// <summary>
    /// Loads a tracked entity or throws 404.
    /// </summary>
    /// <param name="id">Primary key.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The tracked entity.</returns>
    /// <exception cref="NotFoundException">Absent or out of scope.</exception>
    protected async Task<TEntity> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await Repository.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException(EntityName);
}
