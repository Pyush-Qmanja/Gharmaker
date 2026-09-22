using Google.Cloud.Firestore;
using Platform.Api.Firestore;
using Platform.Api.Mapping;
using Platform.Api.Repositories;
using Platform.Shared.Dtos.Catalog;
using Platform.Shared.Entities.Catalog;

namespace Platform.Api.Services.Catalog;

/// <summary>
/// CRUD for brands. Inherits everything from <see cref="CrudService{TEntity,TDto,TCreate,TUpdate}"/>
/// and only defines how brands are searched, sorted and kept unique.
/// </summary>
public sealed class BrandService : CrudService<Brand, BrandDto, CreateBrandRequest, UpdateBrandRequest>
{
    /// <summary>Stored name of <see cref="Brand.Slug"/>.</summary>
    private static readonly string SlugField = FirestoreNaming.Field(nameof(Brand.Slug));

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
    /// Prefix match on the slug. Slugs are always lower-case, so turning the
    /// search text into slug form makes the match case-insensitive.
    /// </summary>
    /// <param name="query">Org-scoped query.</param>
    /// <param name="search">Trimmed search text.</param>
    /// <returns>The filtered query, ordered by slug.</returns>
    protected override Query ApplySearch(Query query, string search)
    {
        string prefix = search.ToLowerInvariant().Replace(' ', '-');
        return query
            .WhereGreaterThanOrEqualTo(SlugField, prefix)
            .WhereLessThan(SlugField, prefix + '')
            .OrderBy(SlugField);
    }

    /// <summary>
    /// Sorts by display order, then name.
    /// </summary>
    /// <param name="query">Org-scoped query.</param>
    /// <returns>The ordered query.</returns>
    protected override Query ApplyOrder(Query query) =>
        query.OrderBy(FirestoreNaming.Field(nameof(Brand.DisplayOrder)))
             .OrderBy(FirestoreNaming.Field(nameof(Brand.Name)));

    /// <summary>
    /// A slug is unique within an organisation.
    /// </summary>
    /// <param name="entity">Brand about to be written.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task that completes when the slug is free.</returns>
    protected override Task EnsureUniqueAsync(Brand entity, CancellationToken cancellationToken) =>
        RequireUniqueAsync(entity, nameof(Brand.Slug), entity.Slug, cancellationToken);
}
