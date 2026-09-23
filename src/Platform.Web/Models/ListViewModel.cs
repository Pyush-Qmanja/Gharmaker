using Platform.Shared.Dtos.Common;

namespace Platform.Web.Models;

/// <summary>
/// What the shared list page (<c>Views/Shared/CrudIndex.cshtml</c>) needs,
/// without knowing the row type. The row type is only known to the entity's
/// own <c>_Table</c> partial.
/// </summary>
public interface IListViewModel
{
    /// <summary>Page heading, e.g. "Brands".</summary>
    string Title { get; }

    /// <summary>One row's name in lower case, e.g. "brand", for "New brand" and "12 brands".</summary>
    string ItemName { get; }

    /// <summary>Current search text.</summary>
    string? Search { get; }

    /// <summary>Current 1-based page.</summary>
    int Page { get; }

    /// <summary>Rows per page.</summary>
    int PageSize { get; }

    /// <summary>Action the pager links to (usually <c>Index</c>).</summary>
    string PageAction { get; }

    /// <summary>Total number of pages.</summary>
    int TotalPages { get; }

    /// <summary>Total matching rows.</summary>
    int TotalCount { get; }

    /// <summary>True when a previous page exists.</summary>
    bool HasPrevious { get; }

    /// <summary>True when a next page exists.</summary>
    bool HasNext { get; }

    /// <summary>The rows, passed as the model of the entity's <c>_Table</c> partial.</summary>
    object Items { get; }

    /// <summary>
    /// Extra filters (e.g. category, brand) that the pager and search box must
    /// carry along so changing page or search keeps them.
    /// </summary>
    IReadOnlyDictionary<string, string?> RouteValues { get; }
}

/// <summary>
/// Typed list page model.
/// </summary>
/// <typeparam name="T">Row type.</typeparam>
public sealed class ListViewModel<T> : IListViewModel
{
    private readonly PagedResult<T> _result;

    /// <summary>
    /// Creates the model.
    /// </summary>
    /// <param name="title">Page heading.</param>
    /// <param name="result">Page of rows from the API.</param>
    /// <param name="search">Search text that produced the page.</param>
    /// <param name="routeValues">Extra filters to keep when paging or searching.</param>
    /// <param name="itemName">One row's name, e.g. "brand"; defaults to the title in lower case without a trailing "s".</param>
    /// <param name="pageAction">Action the pager links to; defaults to <c>Index</c>.</param>
    public ListViewModel(
        string title,
        PagedResult<T> result,
        string? search,
        IReadOnlyDictionary<string, string?>? routeValues = null,
        string? itemName = null,
        string pageAction = "Index")
    {
        PageAction = pageAction;
        Title = title;
        ItemName = (itemName ?? title.TrimEnd('s')).ToLowerInvariant();
        _result = result;
        Search = search;
        RouteValues = routeValues ?? new Dictionary<string, string?>();
    }

    /// <inheritdoc />
    public string Title { get; }

    /// <inheritdoc />
    public string? Search { get; }

    /// <inheritdoc />
    public int Page => _result.Page;

    /// <inheritdoc />
    public string ItemName { get; }

    /// <inheritdoc />
    public int PageSize => _result.PageSize;

    /// <inheritdoc />
    public string PageAction { get; }

    /// <inheritdoc />
    public int TotalPages => _result.TotalPages;

    /// <inheritdoc />
    public int TotalCount => _result.TotalCount;

    /// <inheritdoc />
    public bool HasPrevious => _result.HasPrevious;

    /// <inheritdoc />
    public bool HasNext => _result.HasNext;

    /// <inheritdoc />
    public object Items => _result.Items;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string?> RouteValues { get; }
}
