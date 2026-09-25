using Google.Cloud.Firestore;
using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Mapping;
using Platform.Api.Repositories;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Pricing;
using Platform.Shared.Entities.Catalog;
using Platform.Shared.Entities.Pricing;

namespace Platform.Api.Services.Pricing;

/// <summary>
/// GST rates by HSN code. A rate is never edited (P8): a change is a new row
/// with its own start, so orders keep the rate that was true when placed.
/// </summary>
public interface ITaxRateService
{
    /// <summary>
    /// Lists the rates in force now (one per HSN code, filtered by an HSN prefix),
    /// or every rate of one HSN code when <see cref="TaxRateListRequest.HsnCode"/> is set.
    /// </summary>
    /// <param name="request">Filter and page.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One page of rates.</returns>
    Task<PagedResult<TaxRateDto>> ListAsync(TaxRateListRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a rate for an HSN code from a date.
    /// </summary>
    /// <param name="request">Rate and start.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The new rate.</returns>
    Task<TaxRateDto> CreateAsync(CreateTaxRateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists HSN codes of active products that have no rate in force, so those products cannot be sold.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The HSN codes, most products first.</returns>
    Task<List<MissingTaxRateDto>> GetMissingAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Firestore-backed <see cref="ITaxRateService"/>.
/// </summary>
public sealed class TaxRateService : ITaxRateService
{
    /// <summary>How far in the past a "now" start may be, to allow for clock drift between UI and API.</summary>
    private static readonly TimeSpan PastTolerance = TimeSpan.FromMinutes(5);

    /// <summary>Product names shown per missing HSN code.</summary>
    private const int ExampleCount = 3;

    private static readonly string IsActiveField = FirestoreNaming.Field(nameof(Product.IsActive));
    private static readonly string HsnField = FirestoreNaming.Field(nameof(Product.HsnCode));
    private static readonly string NameField = FirestoreNaming.Field(nameof(Product.Name));

    private readonly IRepository<TaxRate> _rates;
    private readonly IRepository<Product> _products;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="rates">Tax rate data access.</param>
    /// <param name="products">Product data access, for counts.</param>
    /// <param name="unitOfWork">Commits the new rate.</param>
    /// <param name="timeProvider">Clock.</param>
    public TaxRateService(IRepository<TaxRate> rates, IRepository<Product> products, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _rates = rates;
        _products = products;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<PagedResult<TaxRateDto>> ListAsync(TaxRateListRequest request, CancellationToken cancellationToken = default)
    {
        DateTime now = Now();
        var table = new TaxRateTable(await _rates.ListAsync(_rates.Query(), cancellationToken));
        Dictionary<string, int> productsByRateHsn = CountProductsByRateHsn(await ActiveProductsAsync(cancellationToken), table, now);

        List<TaxRateDto> rows;
        if (!string.IsNullOrEmpty(request.HsnCode))
        {
            TaxRate? inForce = Dated.InForce(table.RowsOf(request.HsnCode), r => r.ValidFrom, now);
            rows = table.RowsOf(request.HsnCode)
                .OrderByDescending(r => r.ValidFrom)
                .Select(r => ToDto(r, ReferenceEquals(r, inForce), productsByRateHsn.GetValueOrDefault(r.HsnCode)))
                .ToList();
        }
        else
        {
            string prefix = request.Search?.Trim() ?? string.Empty;
            rows = table.HsnCodes
                .Where(hsn => hsn.StartsWith(prefix, StringComparison.Ordinal))
                .Select(hsn =>
                {
                    TaxRate? inForce = Dated.InForce(table.RowsOf(hsn), r => r.ValidFrom, now);
                    TaxRate shown = inForce ?? Dated.Upcoming(table.RowsOf(hsn), r => r.ValidFrom, now)!;
                    return ToDto(shown, inForce is not null, productsByRateHsn.GetValueOrDefault(hsn));
                })
                .OrderBy(r => r.HsnCode, StringComparer.Ordinal)
                .ToList();
        }

        return new PagedResult<TaxRateDto>
        {
            Items = rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList(),
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = rows.Count,
        };
    }

    /// <inheritdoc />
    public async Task<TaxRateDto> CreateAsync(CreateTaxRateRequest request, CancellationToken cancellationToken = default)
    {
        DateTime now = Now();
        DateTime validFrom = request.ValidFrom?.ToUniversalTime() ?? now;
        if (validFrom < now - PastTolerance)
        {
            throw new FieldValidationException(nameof(CreateTaxRateRequest.ValidFrom),
                "A rate cannot start in the past: orders already placed keep their rate. Leave it empty to start now.");
        }

        var rate = new TaxRate
        {
            HsnCode = request.HsnCode.Trim(),
            RatePercent = request.RatePercent,
            CessPercent = request.CessPercent,
            ValidFrom = validFrom < now ? now : validFrom,
            Remarks = DtoMapping.CleanOptional(request.Remarks),
        };
        _rates.Add(rate);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(rate, rate.ValidFrom <= Now(), productCount: 0);
    }

    /// <inheritdoc />
    public async Task<List<MissingTaxRateDto>> GetMissingAsync(CancellationToken cancellationToken = default)
    {
        DateTime now = Now();
        var table = new TaxRateTable(await _rates.ListAsync(_rates.Query(), cancellationToken));
        return (await ActiveProductsAsync(cancellationToken))
            .Where(p => table.Find(p.HsnCode, now) is null)
            .GroupBy(p => p.HsnCode, StringComparer.Ordinal)
            .Select(g => new MissingTaxRateDto
            {
                HsnCode = g.Key,
                ProductCount = g.Count(),
                ExampleProducts = g.Select(p => p.Name).Order(StringComparer.OrdinalIgnoreCase).Take(ExampleCount).ToList(),
            })
            .OrderByDescending(m => m.ProductCount)
            .ThenBy(m => m.HsnCode, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Reads the HSN code and name of every active product (a projection, so it stays light).
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Products with only those two fields filled.</returns>
    private Task<IReadOnlyList<Product>> ActiveProductsAsync(CancellationToken cancellationToken) =>
        _products.ListAsync(_products.Query().WhereEqualTo(IsActiveField, true).Select(HsnField, NameField), cancellationToken);

    /// <summary>
    /// Counts, for each rate row's HSN code, the active products that take it.
    /// </summary>
    /// <param name="products">Active products.</param>
    /// <param name="table">Rates.</param>
    /// <param name="now">Instant.</param>
    /// <returns>Products per rate HSN code.</returns>
    private static Dictionary<string, int> CountProductsByRateHsn(IEnumerable<Product> products, TaxRateTable table, DateTime now) =>
        products
            .Select(p => table.Find(p.HsnCode, now)?.HsnCode)
            .Where(hsn => hsn is not null)
            .GroupBy(hsn => hsn!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

    /// <summary>
    /// Maps a rate row.
    /// </summary>
    /// <param name="rate">Row.</param>
    /// <param name="isCurrent">True when in force now.</param>
    /// <param name="productCount">Active products that take it.</param>
    /// <returns>The DTO.</returns>
    private static TaxRateDto ToDto(TaxRate rate, bool isCurrent, int productCount) => new TaxRateDto
    {
        HsnCode = rate.HsnCode,
        RatePercent = rate.RatePercent,
        CessPercent = rate.CessPercent,
        ValidFrom = rate.ValidFrom,
        Remarks = rate.Remarks,
        IsCurrent = isCurrent,
        ProductCount = productCount,
    }.WithAuditFrom(rate);

    /// <summary>
    /// Current UTC instant.
    /// </summary>
    /// <returns>Now.</returns>
    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;
}
