using Microsoft.AspNetCore.WebUtilities;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Identity;
using Platform.Shared.Dtos.Inventory;
using Platform.Shared.Dtos.Pricing;
using Platform.Shared.Dtos.Sales;

namespace Platform.Web.Services.Api;

/// <summary>
/// Builds API URLs with the paging fields and any extra filters, leaving out empty values.
/// </summary>
public static class ApiQuery
{
    /// <summary>
    /// Builds a URL.
    /// </summary>
    /// <param name="path">API path.</param>
    /// <param name="paging">Page, size and search, if the endpoint pages.</param>
    /// <param name="filters">Extra query values; null or empty ones are left out.</param>
    /// <returns>The path with its query string.</returns>
    public static string Build(string path, PagedRequest? paging, params (string Name, string? Value)[] filters)
    {
        var query = new Dictionary<string, string?>();
        if (paging is not null)
        {
            query["page"] = paging.Page.ToString(System.Globalization.CultureInfo.InvariantCulture);
            query["pageSize"] = paging.PageSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
            query["search"] = paging.Search;
        }

        foreach (var (name, value) in filters)
        {
            query[name] = value;
        }

        return QueryHelpers.AddQueryString(path, query.Where(q => !string.IsNullOrEmpty(q.Value)));
    }
}

/// <summary>
/// Typed client for prices (<c>/api/prices</c>) and GST rates (<c>/api/tax-rates</c>).
/// Price lists themselves use the generic CRUD client.
/// </summary>
public interface IPricingApiClient
{
    /// <summary>One page of products with each SKU's price in a list.</summary>
    /// <param name="request">List, category, search, page.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The page.</returns>
    Task<ApiResult<PagedResult<PriceGridProductDto>>> GetGridAsync(PriceGridRequest request, CancellationToken cancellationToken = default);

    /// <summary>Every price a SKU has had in a list.</summary>
    /// <param name="priceListId">List.</param>
    /// <param name="skuId">SKU.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The rows, newest first.</returns>
    Task<ApiResult<List<SkuPriceDto>>> GetHistoryAsync(Guid priceListId, Guid skuId, CancellationToken cancellationToken = default);

    /// <summary>Sets a new price.</summary>
    /// <param name="request">Price.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new row.</returns>
    Task<ApiResult<SkuPriceDto>> SetPriceAsync(SetSkuPriceRequest request, CancellationToken cancellationToken = default);

    /// <summary>GST rates in force, or the history of one HSN code.</summary>
    /// <param name="request">Filter and page.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One page of rates.</returns>
    Task<ApiResult<PagedResult<TaxRateDto>>> GetTaxRatesAsync(TaxRateListRequest request, CancellationToken cancellationToken = default);

    /// <summary>HSN codes of active products with no GST rate.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The HSN codes.</returns>
    Task<ApiResult<List<MissingTaxRateDto>>> GetMissingTaxRatesAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds a GST rate.</summary>
    /// <param name="request">Rate.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new rate.</returns>
    Task<ApiResult<TaxRateDto>> CreateTaxRateAsync(CreateTaxRateRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IPricingApiClient"/>.
/// </summary>
public sealed class PricingApiClient : IPricingApiClient
{
    private readonly IApiClient _api;

    /// <summary>
    /// Creates the client.
    /// </summary>
    /// <param name="api">Underlying JSON client.</param>
    public PricingApiClient(IApiClient api)
    {
        _api = api;
    }

    /// <inheritdoc />
    public Task<ApiResult<PagedResult<PriceGridProductDto>>> GetGridAsync(PriceGridRequest request, CancellationToken cancellationToken = default) =>
        _api.GetAsync<PagedResult<PriceGridProductDto>>(ApiQuery.Build(ApiRoutes.Prices, request,
            (nameof(request.PriceListId), request.PriceListId.ToString()),
            (nameof(request.CategoryId), request.CategoryId?.ToString())), cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<List<SkuPriceDto>>> GetHistoryAsync(Guid priceListId, Guid skuId, CancellationToken cancellationToken = default) =>
        _api.GetAsync<List<SkuPriceDto>>(ApiQuery.Build($"{ApiRoutes.Prices}/history", null,
            ("priceListId", priceListId.ToString()), ("skuId", skuId.ToString())), cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<SkuPriceDto>> SetPriceAsync(SetSkuPriceRequest request, CancellationToken cancellationToken = default) =>
        _api.PostAsync<SetSkuPriceRequest, SkuPriceDto>(ApiRoutes.Prices, request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<PagedResult<TaxRateDto>>> GetTaxRatesAsync(TaxRateListRequest request, CancellationToken cancellationToken = default) =>
        _api.GetAsync<PagedResult<TaxRateDto>>(ApiQuery.Build(ApiRoutes.TaxRates, request, (nameof(request.HsnCode), request.HsnCode)), cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<List<MissingTaxRateDto>>> GetMissingTaxRatesAsync(CancellationToken cancellationToken = default) =>
        _api.GetAsync<List<MissingTaxRateDto>>($"{ApiRoutes.TaxRates}/missing", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<TaxRateDto>> CreateTaxRateAsync(CreateTaxRateRequest request, CancellationToken cancellationToken = default) =>
        _api.PostAsync<CreateTaxRateRequest, TaxRateDto>(ApiRoutes.TaxRates, request, cancellationToken);
}

/// <summary>
/// Typed client for delivery areas (<c>/api/delivery-areas</c>).
/// </summary>
public interface IDeliveryAreaApiClient
{
    /// <summary>Lists areas.</summary>
    /// <param name="request">Warehouse, PIN prefix, page.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One page.</returns>
    Task<ApiResult<PagedResult<DeliveryAreaDto>>> ListAsync(DeliveryAreaListRequest request, CancellationToken cancellationToken = default);

    /// <summary>Adds PIN codes to a warehouse.</summary>
    /// <param name="request">Warehouse, PIN codes, lead time.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Counts added and updated.</returns>
    Task<ApiResult<AddDeliveryAreasResult>> AddAsync(AddDeliveryAreasRequest request, CancellationToken cancellationToken = default);

    /// <summary>Changes one area.</summary>
    /// <param name="id">Area id.</param>
    /// <param name="request">New values.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The area.</returns>
    Task<ApiResult<DeliveryAreaDto>> UpdateAsync(Guid id, UpdateDeliveryAreaRequest request, CancellationToken cancellationToken = default);

    /// <summary>Removes one area.</summary>
    /// <param name="id">Area id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome.</returns>
    Task<ApiResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IDeliveryAreaApiClient"/>.
/// </summary>
public sealed class DeliveryAreaApiClient : IDeliveryAreaApiClient
{
    private readonly IApiClient _api;

    /// <summary>
    /// Creates the client.
    /// </summary>
    /// <param name="api">Underlying JSON client.</param>
    public DeliveryAreaApiClient(IApiClient api)
    {
        _api = api;
    }

    /// <inheritdoc />
    public Task<ApiResult<PagedResult<DeliveryAreaDto>>> ListAsync(DeliveryAreaListRequest request, CancellationToken cancellationToken = default) =>
        _api.GetAsync<PagedResult<DeliveryAreaDto>>(ApiQuery.Build(ApiRoutes.DeliveryAreas, request,
            (nameof(request.WarehouseId), request.WarehouseId?.ToString())), cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<AddDeliveryAreasResult>> AddAsync(AddDeliveryAreasRequest request, CancellationToken cancellationToken = default) =>
        _api.PostAsync<AddDeliveryAreasRequest, AddDeliveryAreasResult>(ApiRoutes.DeliveryAreas, request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<DeliveryAreaDto>> UpdateAsync(Guid id, UpdateDeliveryAreaRequest request, CancellationToken cancellationToken = default) =>
        _api.PutAsync<UpdateDeliveryAreaRequest, DeliveryAreaDto>($"{ApiRoutes.DeliveryAreas}/{id}", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _api.DeleteAsync($"{ApiRoutes.DeliveryAreas}/{id}", cancellationToken);
}

/// <summary>
/// Typed client for customers (<c>/api/customers</c>), orders (<c>/api/orders</c>)
/// and business settings (<c>/api/settings/business</c>).
/// </summary>
public interface ISalesApiClient
{
    /// <summary>Lists customers.</summary>
    /// <param name="request">Search and page.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One page.</returns>
    Task<ApiResult<PagedResult<CustomerDto>>> GetCustomersAsync(PagedRequest request, CancellationToken cancellationToken = default);

    /// <summary>Loads a customer.</summary>
    /// <param name="id">Customer id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The customer.</returns>
    Task<ApiResult<CustomerDto>> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Changes a customer.</summary>
    /// <param name="id">Customer id.</param>
    /// <param name="request">New values.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The customer.</returns>
    Task<ApiResult<CustomerDto>> UpdateCustomerAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default);

    /// <summary>Lists orders.</summary>
    /// <param name="request">Status, customer, search, page.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One page.</returns>
    Task<ApiResult<PagedResult<OrderSummaryDto>>> GetOrdersAsync(OrderListRequest request, CancellationToken cancellationToken = default);

    /// <summary>Loads an order.</summary>
    /// <param name="id">Order id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The order.</returns>
    Task<ApiResult<OrderDto>> GetOrderAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Cancels an order.</summary>
    /// <param name="id">Order id.</param>
    /// <param name="request">Why.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The order.</returns>
    Task<ApiResult<OrderDto>> CancelOrderAsync(Guid id, CancelOrderRequest request, CancellationToken cancellationToken = default);

    /// <summary>Confirms a placed order after the phone call.</summary>
    /// <param name="id">Order id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The order.</returns>
    Task<ApiResult<OrderDto>> ConfirmOrderAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Reads the business details.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The details.</returns>
    Task<ApiResult<BusinessSettingsDto>> GetBusinessSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves the business details.</summary>
    /// <param name="request">New details.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The saved details.</returns>
    Task<ApiResult<BusinessSettingsDto>> UpdateBusinessSettingsAsync(UpdateBusinessSettingsRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="ISalesApiClient"/>.
/// </summary>
public sealed class SalesApiClient : ISalesApiClient
{
    private readonly IApiClient _api;

    /// <summary>
    /// Creates the client.
    /// </summary>
    /// <param name="api">Underlying JSON client.</param>
    public SalesApiClient(IApiClient api)
    {
        _api = api;
    }

    /// <inheritdoc />
    public Task<ApiResult<PagedResult<CustomerDto>>> GetCustomersAsync(PagedRequest request, CancellationToken cancellationToken = default) =>
        _api.GetAsync<PagedResult<CustomerDto>>(ApiQuery.Build(ApiRoutes.Customers, request), cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<CustomerDto>> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default) =>
        _api.GetAsync<CustomerDto>($"{ApiRoutes.Customers}/{id}", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<CustomerDto>> UpdateCustomerAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default) =>
        _api.PutAsync<UpdateCustomerRequest, CustomerDto>($"{ApiRoutes.Customers}/{id}", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<PagedResult<OrderSummaryDto>>> GetOrdersAsync(OrderListRequest request, CancellationToken cancellationToken = default) =>
        _api.GetAsync<PagedResult<OrderSummaryDto>>(ApiQuery.Build(ApiRoutes.Orders, request,
            (nameof(request.Status), request.Status?.ToString()),
            (nameof(request.CustomerId), request.CustomerId?.ToString())), cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<OrderDto>> GetOrderAsync(Guid id, CancellationToken cancellationToken = default) =>
        _api.GetAsync<OrderDto>($"{ApiRoutes.Orders}/{id}", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<OrderDto>> CancelOrderAsync(Guid id, CancelOrderRequest request, CancellationToken cancellationToken = default) =>
        _api.PostAsync<CancelOrderRequest, OrderDto>($"{ApiRoutes.Orders}/{id}/cancel", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<OrderDto>> ConfirmOrderAsync(Guid id, CancellationToken cancellationToken = default) =>
        _api.PostAsync<object, OrderDto>($"{ApiRoutes.Orders}/{id}/confirm", new object(), cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<BusinessSettingsDto>> GetBusinessSettingsAsync(CancellationToken cancellationToken = default) =>
        _api.GetAsync<BusinessSettingsDto>(ApiRoutes.BusinessSettings, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<BusinessSettingsDto>> UpdateBusinessSettingsAsync(UpdateBusinessSettingsRequest request, CancellationToken cancellationToken = default) =>
        _api.PutAsync<UpdateBusinessSettingsRequest, BusinessSettingsDto>(ApiRoutes.BusinessSettings, request, cancellationToken);
}
