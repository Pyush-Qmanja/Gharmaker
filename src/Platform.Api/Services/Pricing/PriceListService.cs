using Google.Cloud.Firestore;
using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Mapping;
using Platform.Api.Repositories;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Pricing;
using Platform.Shared.Entities.Pricing;
using Platform.Shared.Entities.Sales;

namespace Platform.Api.Services.Pricing;

/// <summary>
/// Price lists. Keeps three rules: codes are unique; exactly one retail list
/// is active (everyone without a tier or contract buys from it, so it can be
/// renamed but not switched off); a contract list names a real customer.
/// </summary>
public sealed class PriceListService : CrudService<PriceList, PriceListDto, CreatePriceListRequest, UpdatePriceListRequest>
{
    private static readonly string NameField = FirestoreNaming.Field(nameof(PriceList.Name));
    private static readonly string CodeField = FirestoreNaming.Field(nameof(PriceList.Code));
    private static readonly string TypeField = FirestoreNaming.Field(nameof(PriceList.Type));
    private static readonly string IsActiveField = FirestoreNaming.Field(nameof(PriceList.IsActive));

    private readonly IRepository<Customer> _customers;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="repository">Price list data access.</param>
    /// <param name="unitOfWork">Commits changes.</param>
    /// <param name="mapper">Maps entity and DTOs.</param>
    /// <param name="customers">Customer data access, for contract lists.</param>
    public PriceListService(
        IRepository<PriceList> repository,
        IUnitOfWork unitOfWork,
        IEntityMapper<PriceList, PriceListDto, CreatePriceListRequest, UpdatePriceListRequest> mapper,
        IRepository<Customer> customers)
        : base(repository, unitOfWork, mapper)
    {
        _customers = customers;
    }

    /// <inheritdoc />
    protected override string EntityName => "Price list";

    /// <inheritdoc />
    public override async Task<PagedResult<PriceListDto>> GetPagedAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        PagedResult<PriceListDto> page = await base.GetPagedAsync(request, cancellationToken);
        await AddCustomerNamesAsync(page.Items, cancellationToken);
        return page;
    }

    /// <inheritdoc />
    public override async Task<PriceListDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        PriceListDto list = await base.GetByIdAsync(id, cancellationToken);
        await AddCustomerNamesAsync(new[] { list }, cancellationToken);
        return list;
    }

    /// <summary>
    /// Searches by code prefix (codes are upper-case).
    /// </summary>
    /// <param name="query">Org-scoped query.</param>
    /// <param name="search">Text typed.</param>
    /// <returns>The filtered query.</returns>
    protected override Query ApplySearch(Query query, string search) =>
        query.WhereStartsWith(CodeField, search.ToUpperInvariant());

    /// <summary>
    /// Lists by name.
    /// </summary>
    /// <param name="query">Org-scoped query.</param>
    /// <returns>The ordered query.</returns>
    protected override Query ApplyOrder(Query query) => query.OrderBy(NameField);

    /// <inheritdoc />
    protected override Task EnsureUniqueAsync(PriceList entity, CancellationToken cancellationToken) =>
        RequireUniqueAsync(entity, nameof(PriceList.Code), entity.Code, cancellationToken);

    /// <summary>
    /// Checks the retail and contract rules before a list is saved.
    /// </summary>
    /// <param name="entity">List about to be saved.</param>
    /// <param name="isNew">True on create.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the checks pass.</returns>
    protected override async Task BeforeWriteAsync(PriceList entity, bool isNew, CancellationToken cancellationToken)
    {
        if (entity.Type == PriceListType.Retail)
        {
            if (!entity.IsActive)
            {
                throw new BusinessRuleException(
                    "The retail list stays active: customers without a tier or contract buy from it. Change its prices instead.");
            }

            IReadOnlyList<PriceList> retail = await Repository.ListAsync(
                Repository.Query()
                    .WhereEqualTo(TypeField, DocumentConverter.ToFirestoreValue(PriceListType.Retail))
                    .WhereEqualTo(IsActiveField, true)
                    .Limit(2),
                cancellationToken);
            if (retail.Any(r => r.Id != entity.Id))
            {
                throw new ConflictException(nameof(PriceList.Type), "There is already an active retail list. Use a tier list for a group of customers.");
            }
        }

        if (entity.Type == PriceListType.Contract
            && (entity.CustomerId is not { } customerId || await _customers.GetByIdAsync(customerId, cancellationToken) is null))
        {
            throw new FieldValidationException(nameof(PriceList.CustomerId), "Choose an existing customer for this contract list.");
        }
    }

    /// <summary>
    /// Blocks deactivating the retail list.
    /// </summary>
    /// <param name="entity">List about to be deactivated.</param>
    /// <param name="cancellationToken">Not used.</param>
    /// <returns>A completed task when allowed.</returns>
    protected override Task BeforeDeleteAsync(PriceList entity, CancellationToken cancellationToken) =>
        entity.Type == PriceListType.Retail
            ? throw new BusinessRuleException("The retail list stays active: customers without a tier or contract buy from it.")
            : Task.CompletedTask;

    /// <summary>
    /// Fills the customer name of contract lists.
    /// </summary>
    /// <param name="lists">Lists to fill.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when names are set.</returns>
    private async Task AddCustomerNamesAsync(IReadOnlyList<PriceListDto> lists, CancellationToken cancellationToken)
    {
        var ids = lists.Where(l => l.CustomerId.HasValue).Select(l => l.CustomerId!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var names = (await _customers.GetByIdsAsync(ids, cancellationToken)).ToDictionary(c => c.Id, c => c.Name);
        foreach (PriceListDto list in lists)
        {
            list.CustomerName = list.CustomerId is { } id && names.TryGetValue(id, out string? name) ? name : null;
        }
    }
}
