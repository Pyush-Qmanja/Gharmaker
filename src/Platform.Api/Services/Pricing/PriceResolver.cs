using Platform.Api.Firestore;
using Platform.Api.Repositories;
using Platform.Shared.Entities.Pricing;
using Platform.Shared.Entities.Sales;

namespace Platform.Api.Services.Pricing;

/// <summary>
/// A SKU's price for one customer, and the list it came from.
/// </summary>
/// <param name="PriceListId">List the price came from.</param>
/// <param name="Uom">Unit the price is for.</param>
/// <param name="Currency">Currency (ISO 4217).</param>
/// <param name="Slabs">Rates by quantity, ascending.</param>
public sealed record ResolvedPrice(Guid PriceListId, string Uom, string Currency, IReadOnlyList<PriceSlab> Slabs);

/// <summary>
/// Finds the price each SKU has for a customer, first match wins: the
/// customer's contract list, their tier list, then the retail list. A SKU with
/// no price in any of them is simply absent — never priced at zero.
/// </summary>
public interface IPriceResolver
{
    /// <summary>
    /// Resolves prices.
    /// </summary>
    /// <param name="skuIds">SKUs to price.</param>
    /// <param name="customer">Signed-in customer, or null for a visitor (retail).</param>
    /// <param name="at">Instant the price must be in force (P8).</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Price by SKU id, for the SKUs that have one.</returns>
    Task<IReadOnlyDictionary<Guid, ResolvedPrice>> ResolveAsync(
        IReadOnlyCollection<Guid> skuIds, Customer? customer, DateTime at, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads every price row of some SKUs in one list, all dates.
    /// </summary>
    /// <param name="priceListId">List.</param>
    /// <param name="skuIds">SKUs.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The rows.</returns>
    Task<IReadOnlyList<SkuPrice>> ListRowsAsync(Guid priceListId, IEnumerable<Guid> skuIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Firestore-backed <see cref="IPriceResolver"/>.
/// </summary>
public sealed class PriceResolver : IPriceResolver
{
    /// <summary>Firestore's limit on values in one "in" filter.</summary>
    private const int MaxInValues = 30;

    private static readonly string TypeField = FirestoreNaming.Field(nameof(PriceList.Type));
    private static readonly string IsActiveField = FirestoreNaming.Field(nameof(PriceList.IsActive));
    private static readonly string CustomerIdField = FirestoreNaming.Field(nameof(PriceList.CustomerId));
    private static readonly string PriceListIdField = FirestoreNaming.Field(nameof(SkuPrice.PriceListId));
    private static readonly string SkuIdField = FirestoreNaming.Field(nameof(SkuPrice.SkuId));

    private readonly IRepository<PriceList> _lists;
    private readonly IRepository<SkuPrice> _prices;

    /// <summary>
    /// Creates the resolver.
    /// </summary>
    /// <param name="lists">Price list data access.</param>
    /// <param name="prices">Price data access.</param>
    public PriceResolver(IRepository<PriceList> lists, IRepository<SkuPrice> prices)
    {
        _lists = lists;
        _prices = prices;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, ResolvedPrice>> ResolveAsync(
        IReadOnlyCollection<Guid> skuIds, Customer? customer, DateTime at, CancellationToken cancellationToken = default)
    {
        var resolved = new Dictionary<Guid, ResolvedPrice>();
        foreach (PriceList list in await CandidateListsAsync(customer, cancellationToken))
        {
            List<Guid> remaining = skuIds.Where(id => !resolved.ContainsKey(id)).Distinct().ToList();
            if (remaining.Count == 0)
            {
                break;
            }

            foreach (IGrouping<Guid, SkuPrice> rows in (await ListRowsAsync(list.Id, remaining, cancellationToken)).GroupBy(p => p.SkuId))
            {
                if (Dated.InForce(rows, r => r.ValidFrom, at) is { } price)
                {
                    resolved[rows.Key] = new ResolvedPrice(list.Id, price.Uom, price.Currency, price.Slabs);
                }
            }
        }

        return resolved;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SkuPrice>> ListRowsAsync(Guid priceListId, IEnumerable<Guid> skuIds, CancellationToken cancellationToken = default)
    {
        var rows = new List<SkuPrice>();
        foreach (Guid[] chunk in skuIds.Distinct().Chunk(MaxInValues))
        {
            rows.AddRange(await _prices.ListAsync(
                _prices.Query()
                    .WhereEqualTo(PriceListIdField, DocumentConverter.ToFirestoreValue(priceListId))
                    .WhereIn(SkuIdField, chunk.Select(id => DocumentConverter.ToFirestoreValue(id))),
                cancellationToken));
        }

        return rows;
    }

    /// <summary>
    /// Lists the price lists to try, in order: contract lists, the tier list, the retail list.
    /// </summary>
    /// <param name="customer">Customer, or null for a visitor.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Active lists in resolution order.</returns>
    private async Task<List<PriceList>> CandidateListsAsync(Customer? customer, CancellationToken cancellationToken)
    {
        var lists = new List<PriceList>();
        if (customer is not null)
        {
            lists.AddRange((await _lists.ListAsync(
                    _lists.Query()
                        .WhereEqualTo(TypeField, DocumentConverter.ToFirestoreValue(PriceListType.Contract))
                        .WhereEqualTo(CustomerIdField, DocumentConverter.ToFirestoreValue(customer.Id)),
                    cancellationToken))
                .Where(l => l.IsActive)
                .OrderBy(l => l.Code, StringComparer.Ordinal));

            if (customer.TierPriceListId is { } tierId
                && await _lists.GetByIdAsync(tierId, cancellationToken) is { IsActive: true } tier)
            {
                lists.Add(tier);
            }
        }

        lists.AddRange(await _lists.ListAsync(
            _lists.Query()
                .WhereEqualTo(TypeField, DocumentConverter.ToFirestoreValue(PriceListType.Retail))
                .WhereEqualTo(IsActiveField, true)
                .Limit(1),
            cancellationToken));
        return lists;
    }
}
