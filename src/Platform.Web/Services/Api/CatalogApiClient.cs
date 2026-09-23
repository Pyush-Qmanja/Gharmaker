using Microsoft.AspNetCore.WebUtilities;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Catalog;
using Platform.Shared.Dtos.Common;

namespace Platform.Web.Services.Api;

/// <summary>
/// Calls the catalogue endpoints (<c>/api/catalog</c>): browsing, unit
/// conversion and import.
/// </summary>
public interface ICatalogApiClient
{
    /// <summary>
    /// Fetches the category tree.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The result.</returns>
    Task<ApiResult<List<CategoryNodeDto>>> GetCategoryTreeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches one page of products.
    /// </summary>
    /// <param name="request">Filters and paging.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The result.</returns>
    Task<ApiResult<PagedResult<ProductListItemDto>>> GetProductsAsync(CatalogBrowseRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a product with its SKUs.
    /// </summary>
    /// <param name="productId">Product id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The result.</returns>
    Task<ApiResult<ProductDetailDto>> GetProductAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts an amount of a SKU between units.
    /// </summary>
    /// <param name="skuId">SKU id.</param>
    /// <param name="request">Amount, from-unit and to-unit.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The result.</returns>
    Task<ApiResult<ConversionResultDto>> ConvertAsync(Guid skuId, SkuConversionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the Excel import template.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The file.</returns>
    Task<ApiResult<ApiFile>> GetImportTemplateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a file for an import preview.
    /// </summary>
    /// <param name="content">File content.</param>
    /// <param name="fileName">File name.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The preview.</returns>
    Task<ApiResult<ImportPreviewDto>> PreviewImportAsync(Stream content, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a previewed import.
    /// </summary>
    /// <param name="importId">Id from the preview.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>What changed.</returns>
    Task<ApiResult<ImportResultDto>> CommitImportAsync(Guid importId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="ICatalogApiClient"/> over <see cref="IApiClient"/>.
/// </summary>
public sealed class CatalogApiClient : ICatalogApiClient
{
    private readonly IApiClient _api;

    /// <summary>
    /// Creates the client.
    /// </summary>
    /// <param name="api">Underlying JSON client.</param>
    public CatalogApiClient(IApiClient api)
    {
        _api = api;
    }

    /// <inheritdoc />
    public Task<ApiResult<List<CategoryNodeDto>>> GetCategoryTreeAsync(CancellationToken cancellationToken = default) =>
        _api.GetAsync<List<CategoryNodeDto>>($"{ApiRoutes.Catalog}/categories", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<PagedResult<ProductListItemDto>>> GetProductsAsync(CatalogBrowseRequest request, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string?>
        {
            [nameof(request.Page)] = request.Page.ToString(),
            [nameof(request.PageSize)] = request.PageSize.ToString(),
            [nameof(request.Search)] = request.Search,
            [nameof(request.CategoryId)] = request.CategoryId?.ToString(),
            [nameof(request.BrandId)] = request.BrandId?.ToString(),
        };
        return _api.GetAsync<PagedResult<ProductListItemDto>>(
            QueryHelpers.AddQueryString($"{ApiRoutes.Catalog}/products", query.Where(q => q.Value is not null)), cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<ProductDetailDto>> GetProductAsync(Guid productId, CancellationToken cancellationToken = default) =>
        _api.GetAsync<ProductDetailDto>($"{ApiRoutes.Catalog}/products/{productId}", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<ConversionResultDto>> ConvertAsync(Guid skuId, SkuConversionRequest request, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string?>
        {
            [nameof(request.Value)] = request.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [nameof(request.From)] = request.From,
            [nameof(request.To)] = request.To,
        };
        return _api.GetAsync<ConversionResultDto>(QueryHelpers.AddQueryString($"{ApiRoutes.Catalog}/skus/{skuId}/convert", query), cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<ApiFile>> GetImportTemplateAsync(CancellationToken cancellationToken = default) =>
        _api.GetFileAsync($"{ApiRoutes.Catalog}/import/template", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<ImportPreviewDto>> PreviewImportAsync(Stream content, string fileName, CancellationToken cancellationToken = default) =>
        _api.PostFileAsync<ImportPreviewDto>($"{ApiRoutes.Catalog}/import/preview", content, fileName, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<ImportResultDto>> CommitImportAsync(Guid importId, CancellationToken cancellationToken = default) =>
        _api.PostAsync<object?, ImportResultDto>($"{ApiRoutes.Catalog}/import/{importId}/commit", null, cancellationToken);
}
