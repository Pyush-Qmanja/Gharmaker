using Platform.Shared.Constants;

namespace Platform.Shared.Dtos.Common;

/// <summary>
/// Query parameters accepted by every list endpoint. All lists paginate.
/// </summary>
public class PagedRequest
{
    /// <summary>1-based page number.</summary>
    public int Page { get; set; } = 1;

    /// <summary>Rows per page, capped at <see cref="Paging.MaxPageSize"/>.</summary>
    public int PageSize { get; set; } = Paging.DefaultPageSize;

    /// <summary>Optional free-text filter; each list decides which fields it searches.</summary>
    public string? Search { get; set; }
}
