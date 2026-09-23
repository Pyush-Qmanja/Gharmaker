using Google.Cloud.Firestore;
using Platform.Api.Firestore;
using Platform.Api.Mapping;
using Platform.Api.Repositories;
using Platform.Shared.Dtos.Catalog;
using Platform.Shared.Entities.Catalog;

namespace Platform.Api.Services.Catalog;

/// <summary>
/// CRUD for units of measure. Codes are unique per organisation.
/// </summary>
public sealed class UomService : CrudService<Uom, UomDto, CreateUomRequest, UpdateUomRequest>
{
    /// <summary>Stored name of <see cref="Uom.Code"/>.</summary>
    private static readonly string CodeField = FirestoreNaming.Field(nameof(Uom.Code));

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="repository">Unit data access.</param>
    /// <param name="unitOfWork">Commits staged changes.</param>
    /// <param name="mapper">Unit mapping.</param>
    public UomService(
        IRepository<Uom> repository,
        IUnitOfWork unitOfWork,
        IEntityMapper<Uom, UomDto, CreateUomRequest, UpdateUomRequest> mapper)
        : base(repository, unitOfWork, mapper)
    {
    }

    /// <summary>
    /// Sorts units by code.
    /// </summary>
    /// <param name="query">Org-scoped query.</param>
    /// <returns>The ordered query.</returns>
    protected override Query ApplyOrder(Query query) => query.OrderBy(CodeField);

    /// <summary>
    /// Prefix match on the (upper-case) code.
    /// </summary>
    /// <param name="query">Org-scoped query.</param>
    /// <param name="search">Trimmed search text.</param>
    /// <returns>The filtered query.</returns>
    protected override Query ApplySearch(Query query, string search) =>
        query.WhereStartsWith(CodeField, search.ToUpperInvariant());

    /// <summary>
    /// A unit code is unique within an organisation.
    /// </summary>
    /// <param name="entity">Unit about to be written.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task that completes when the code is free.</returns>
    protected override Task EnsureUniqueAsync(Uom entity, CancellationToken cancellationToken) =>
        RequireUniqueAsync(entity, nameof(Uom.Code), entity.Code, cancellationToken);
}
