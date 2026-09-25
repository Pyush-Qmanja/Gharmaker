using Platform.Api.Repositories;
using Platform.Shared.Entities.Pricing;

namespace Platform.Api.Services.Pricing;

/// <summary>
/// Picks, from dated rows (P8), the one in force at an instant.
/// </summary>
public static class Dated
{
    /// <summary>
    /// Returns the row with the latest start that is not after the instant.
    /// </summary>
    /// <typeparam name="T">Row type.</typeparam>
    /// <param name="rows">Rows of one key (one SKU in one list, one HSN code).</param>
    /// <param name="validFrom">Reads a row's start.</param>
    /// <param name="at">Instant.</param>
    /// <returns>The row in force, or null when none has started.</returns>
    public static T? InForce<T>(IEnumerable<T> rows, Func<T, DateTime> validFrom, DateTime at) where T : class =>
        rows.Where(r => validFrom(r) <= at).MaxBy(validFrom);

    /// <summary>
    /// Returns the next row that starts after the instant.
    /// </summary>
    /// <typeparam name="T">Row type.</typeparam>
    /// <param name="rows">Rows of one key.</param>
    /// <param name="validFrom">Reads a row's start.</param>
    /// <param name="at">Instant.</param>
    /// <returns>The upcoming row, or null.</returns>
    public static T? Upcoming<T>(IEnumerable<T> rows, Func<T, DateTime> validFrom, DateTime at) where T : class =>
        rows.Where(r => validFrom(r) > at).MinBy(validFrom);
}

/// <summary>
/// Every GST rate of the organisation, for looking up a product's rate. A
/// product takes the rate of the longest HSN code that begins its own (8, then
/// 6, then 4 digits), in force at the instant asked.
/// </summary>
public sealed class TaxRateTable
{
    private readonly ILookup<string, TaxRate> _byHsn;

    /// <summary>
    /// Builds the table.
    /// </summary>
    /// <param name="rates">All rates, every date.</param>
    public TaxRateTable(IEnumerable<TaxRate> rates)
    {
        _byHsn = rates.ToLookup(r => r.HsnCode, StringComparer.Ordinal);
    }

    /// <summary>Every HSN code that has a rate row.</summary>
    public IEnumerable<string> HsnCodes => _byHsn.Select(g => g.Key);

    /// <summary>
    /// Finds the rate for a product's HSN code.
    /// </summary>
    /// <param name="hsnCode">Product HSN code.</param>
    /// <param name="at">Instant the rate must be in force.</param>
    /// <returns>The rate, or null when none applies (the product cannot be sold).</returns>
    public TaxRate? Find(string hsnCode, DateTime at)
    {
        for (int length = hsnCode.Length; length >= 4; length -= 2)
        {
            string prefix = hsnCode[..length];
            if (_byHsn.Contains(prefix) && Dated.InForce(_byHsn[prefix], r => r.ValidFrom, at) is { } rate)
            {
                return rate;
            }
        }

        return null;
    }

    /// <summary>
    /// Every rate row of one HSN code.
    /// </summary>
    /// <param name="hsnCode">HSN code exactly as the rates carry it.</param>
    /// <returns>The rows, in no order.</returns>
    public IEnumerable<TaxRate> RowsOf(string hsnCode) => _byHsn[hsnCode];
}

/// <summary>
/// Loads the organisation's tax rates once per request.
/// </summary>
public interface ITaxRateProvider
{
    /// <summary>
    /// Returns the table of rates.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The table.</returns>
    Task<TaxRateTable> GetAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Firestore-backed <see cref="ITaxRateProvider"/>. The table is small (one
/// row per HSN code and change), so it is read whole.
/// </summary>
public sealed class TaxRateProvider : ITaxRateProvider
{
    private readonly IRepository<TaxRate> _rates;
    private TaxRateTable? _table;

    /// <summary>
    /// Creates the provider.
    /// </summary>
    /// <param name="rates">Tax rate data access.</param>
    public TaxRateProvider(IRepository<TaxRate> rates)
    {
        _rates = rates;
    }

    /// <inheritdoc />
    public async Task<TaxRateTable> GetAsync(CancellationToken cancellationToken = default) =>
        _table ??= new TaxRateTable(await _rates.ListAsync(_rates.Query(), cancellationToken));
}
