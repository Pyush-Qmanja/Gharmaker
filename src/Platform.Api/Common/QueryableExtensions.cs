using Microsoft.EntityFrameworkCore;
using Platform.Shared.Dtos.Common;

namespace Platform.Api.Common;

/// <summary>
/// Query helpers shared by every list endpoint.
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Counts the query, fetches one page and maps the rows.
    /// </summary>
    /// <typeparam name="TSource">Row type in the database query.</typeparam>
    /// <typeparam name="TResult">Row type returned to the caller.</typeparam>
    /// <param name="query">An already filtered and ordered query.</param>
    /// <param name="request">Page number and size.</param>
    /// <param name="map">Converts one row to the result type.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <returns>The requested page with totals.</returns>
    public static async Task<PagedResult<TResult>> ToPagedResultAsync<TSource, TResult>(
        this IQueryable<TSource> query,
        PagedRequest request,
        Func<TSource, TResult> map,
        CancellationToken cancellationToken = default)
    {
        int totalCount = await query.CountAsync(cancellationToken);
        List<TSource> rows = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TResult>
        {
            Items = rows.Select(map).ToList(),
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
        };
    }
}
