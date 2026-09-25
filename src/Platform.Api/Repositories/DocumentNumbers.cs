using System.Globalization;
using Platform.Shared.Entities.Common;

namespace Platform.Api.Repositories;

/// <summary>
/// Hands out human reference numbers (<c>GRN-2026-000012</c>, <c>ORD-2627-000042</c>)
/// inside a transaction, so two documents can never share a number. Each
/// series has one <see cref="DocumentCounter"/> per organisation.
/// </summary>
public static class DocumentNumbers
{
    /// <summary>
    /// Reads the series' counter, stages its increment and returns the next reference.
    /// Call it with the transaction's other reads, before anything else is staged.
    /// </summary>
    /// <param name="session">Open transaction.</param>
    /// <param name="orgId">Organisation.</param>
    /// <param name="series">Series, e.g. <c>ORD-2627</c>.</param>
    /// <returns>The reference, e.g. <c>ORD-2627-000042</c>.</returns>
    public static async Task<string> NextAsync(ITransactionSession session, Guid orgId, string series)
    {
        Guid counterId = DocumentCounter.IdFor(orgId, series);
        DocumentCounter? counter = await session.GetAsync<DocumentCounter>(counterId);
        if (counter is null)
        {
            counter = new DocumentCounter { Id = counterId, Code = series, LastValue = 1 };
            session.Add(counter);
        }
        else
        {
            counter.LastValue++;
            session.Update(counter);
        }

        return $"{series}-{counter.LastValue.ToString("D6", CultureInfo.InvariantCulture)}";
    }
}
