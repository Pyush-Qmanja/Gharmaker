using Microsoft.EntityFrameworkCore;
using Platform.Api.Mapping;
using Platform.Api.Repositories;
using Platform.Shared.Dtos.Catalog;
using Platform.Shared.Entities.Catalog;

namespace Platform.Api.Services.Catalog;

/// <summary>
/// CRUD for brands. Inherits everything from <see cref="CrudService{TEntity,TDto,TCreate,TUpdate}"/>
/// and only defines how brands are searched and sorted.
/// </summary>
public sealed class BrandService : CrudService<Brand, BrandDto, CreateBrandRequest, UpdateBrandRequest>
{
    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="repository">Brand data access.</param>
    /// <param name="unitOfWork">Commits staged changes.</param>
    /// <param name="mapper">Brand mapping.</param>
    public BrandService(
        IRepository<Brand> repository,
        IUnitOfWork unitOfWork,
        IEntityMapper<Brand, BrandDto, CreateBrandRequest, UpdateBrandRequest> mapper)
        : base(repository, unitOfWork, mapper)
    {
    }

    /// <summary>
    /// Matches name, slug or manufacturer, case-insensitively.
    /// </summary>
    /// <param name="query">Query to filter.</param>
    /// <param name="search">Trimmed search text.</param>
    /// <returns>The filtered query.</returns>
    protected override IQueryable<Brand> ApplySearch(IQueryable<Brand> query, string search)
    {
        string pattern = $"%{search}%";
        return query.Where(b =>
            EF.Functions.ILike(b.Name, pattern)
            || EF.Functions.ILike(b.Slug, pattern)
            || (b.ManufacturerName != null && EF.Functions.ILike(b.ManufacturerName, pattern)));
    }

    /// <summary>
    /// Sorts by display order, then name.
    /// </summary>
    /// <param name="query">Query to order.</param>
    /// <returns>The ordered query.</returns>
    protected override IOrderedQueryable<Brand> ApplyOrder(IQueryable<Brand> query) =>
        query.OrderBy(b => b.DisplayOrder).ThenBy(b => b.Name).ThenBy(b => b.Id);
}
