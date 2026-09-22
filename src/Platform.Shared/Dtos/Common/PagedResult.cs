namespace Platform.Shared.Dtos.Common;

/// <summary>
/// One page of results plus the numbers needed to render a pager.
/// </summary>
/// <typeparam name="T">Item type.</typeparam>
public class PagedResult<T>
{
    /// <summary>Rows on this page.</summary>
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    /// <summary>1-based page number these rows belong to.</summary>
    public int Page { get; set; }

    /// <summary>Requested page size.</summary>
    public int PageSize { get; set; }

    /// <summary>Total matching rows across all pages.</summary>
    public int TotalCount { get; set; }

    /// <summary>Total number of pages for <see cref="TotalCount"/>.</summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>True when a previous page exists.</summary>
    public bool HasPrevious => Page > 1;

    /// <summary>True when a next page exists.</summary>
    public bool HasNext => Page < TotalPages;
}
