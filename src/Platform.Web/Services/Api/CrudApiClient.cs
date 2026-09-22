using Microsoft.AspNetCore.WebUtilities;
using Platform.Shared.Dtos.Common;

namespace Platform.Web.Services.Api;

/// <summary>
/// Calls the five standard endpoints of one API resource. One generic class
/// serves every resource; only the route differs.
/// </summary>
/// <typeparam name="TDto">Read model.</typeparam>
/// <typeparam name="TCreate">Create request body.</typeparam>
/// <typeparam name="TUpdate">Update request body.</typeparam>
public interface ICrudApiClient<TDto, in TCreate, in TUpdate>
{
    /// <summary>
    /// Fetches one page.
    /// </summary>
    /// <param name="request">Paging and search parameters.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The result.</returns>
    Task<ApiResult<PagedResult<TDto>>> GetPagedAsync(PagedRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches one record.
    /// </summary>
    /// <param name="id">Primary key.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The result.</returns>
    Task<ApiResult<TDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a record.
    /// </summary>
    /// <param name="request">Create body.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The result.</returns>
    Task<ApiResult<TDto>> CreateAsync(TCreate request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a record.
    /// </summary>
    /// <param name="id">Primary key.</param>
    /// <param name="request">Update body.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The result.</returns>
    Task<ApiResult<TDto>> UpdateAsync(Guid id, TUpdate request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates a record.
    /// </summary>
    /// <param name="id">Primary key.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The result.</returns>
    Task<ApiResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of <see cref="ICrudApiClient{TDto,TCreate,TUpdate}"/> over <see cref="IApiClient"/>.
/// </summary>
/// <typeparam name="TDto">Read model.</typeparam>
/// <typeparam name="TCreate">Create request body.</typeparam>
/// <typeparam name="TUpdate">Update request body.</typeparam>
public sealed class CrudApiClient<TDto, TCreate, TUpdate> : ICrudApiClient<TDto, TCreate, TUpdate>
{
    private readonly IApiClient _api;
    private readonly string _route;

    /// <summary>
    /// Creates the client for one resource.
    /// </summary>
    /// <param name="api">Underlying JSON client.</param>
    /// <param name="route">Resource route from <c>ApiRoutes</c>.</param>
    public CrudApiClient(IApiClient api, string route)
    {
        _api = api;
        _route = route.TrimEnd('/');
    }

    /// <inheritdoc />
    public Task<ApiResult<PagedResult<TDto>>> GetPagedAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string?>
        {
            [nameof(PagedRequest.Page)] = request.Page.ToString(),
            [nameof(PagedRequest.PageSize)] = request.PageSize.ToString(),
            [nameof(PagedRequest.Search)] = request.Search,
        };
        return _api.GetAsync<PagedResult<TDto>>(QueryHelpers.AddQueryString(_route, query), cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<TDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _api.GetAsync<TDto>($"{_route}/{id}", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<TDto>> CreateAsync(TCreate request, CancellationToken cancellationToken = default) =>
        _api.PostAsync<TCreate, TDto>(_route, request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<TDto>> UpdateAsync(Guid id, TUpdate request, CancellationToken cancellationToken = default) =>
        _api.PutAsync<TUpdate, TDto>($"{_route}/{id}", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _api.DeleteAsync($"{_route}/{id}", cancellationToken);
}
