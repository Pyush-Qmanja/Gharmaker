using Platform.Shared.Constants;
using Platform.Shared.Dtos.Common;

namespace Platform.Web.Services.Api;

/// <summary>
/// Helpers on <see cref="ICrudApiClient{TDto,TCreate,TUpdate}"/> for loading
/// lookup data (drop-downs, name maps) in screen controllers.
/// </summary>
public static class CrudApiClientExtensions
{
    /// <summary>
    /// Loads up to <see cref="Paging.MaxPageSize"/> records for a drop-down or
    /// name lookup. Returns an empty list when the user may not view them, so
    /// screens degrade gracefully instead of failing.
    /// </summary>
    /// <typeparam name="TDto">Read model.</typeparam>
    /// <typeparam name="TCreate">Create request.</typeparam>
    /// <typeparam name="TUpdate">Update request.</typeparam>
    /// <param name="api">API client for the resource.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The records, or an empty list.</returns>
    public static async Task<IReadOnlyList<TDto>> ListForLookupAsync<TDto, TCreate, TUpdate>(
        this ICrudApiClient<TDto, TCreate, TUpdate> api, CancellationToken cancellationToken = default)
    {
        var result = await api.GetPagedAsync(new PagedRequest { PageSize = Paging.MaxPageSize }, cancellationToken);
        return result.Value?.Items ?? Array.Empty<TDto>();
    }
}
