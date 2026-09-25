using Platform.Shared.Constants;
using Platform.Shared.Dtos.Auth;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Sales;
using Platform.Shared.Dtos.Storefront;

namespace Platform.Web.Services.Api;

/// <summary>
/// Typed client for the storefront API (<c>/api/storefront</c>). On /shop pages
/// the bearer token forwarded is the customer's (or none), never a staff token.
/// </summary>
public interface IShopApiClient
{
    /// <summary>Category menu.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Top-level categories.</returns>
    Task<ApiResult<List<ShopCategoryDto>>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>Brands on sale.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Brands.</returns>
    Task<ApiResult<List<ShopBrandDto>>> GetBrandsAsync(CancellationToken cancellationToken = default);

    /// <summary>One page of products.</summary>
    /// <param name="request">Filters and page.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Product cards.</returns>
    Task<ApiResult<PagedResult<ShopProductCardDto>>> GetProductsAsync(ShopProductRequest request, CancellationToken cancellationToken = default);

    /// <summary>One product.</summary>
    /// <param name="id">Product id.</param>
    /// <param name="pincode">PIN code, if known.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The product.</returns>
    Task<ApiResult<ShopProductDto>> GetProductAsync(Guid id, string? pincode, CancellationToken cancellationToken = default);

    /// <summary>Opens an account.</summary>
    /// <param name="request">Details.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The session.</returns>
    Task<ApiResult<ShopSessionDto>> RegisterAsync(ShopRegisterRequest request, CancellationToken cancellationToken = default);

    /// <summary>Signs in.</summary>
    /// <param name="request">Email and password.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The session.</returns>
    Task<ApiResult<ShopSessionDto>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>The customer's profile.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The profile.</returns>
    Task<ApiResult<ShopProfileDto>> GetProfileAsync(CancellationToken cancellationToken = default);

    /// <summary>Changes the profile.</summary>
    /// <param name="request">New values.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The profile.</returns>
    Task<ApiResult<ShopProfileDto>> UpdateProfileAsync(UpdateShopProfileRequest request, CancellationToken cancellationToken = default);

    /// <summary>The cart.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The cart.</returns>
    Task<ApiResult<ShopCartDto>> GetCartAsync(CancellationToken cancellationToken = default);

    /// <summary>Sets the cart's PIN code.</summary>
    /// <param name="pincode">PIN code.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The cart.</returns>
    Task<ApiResult<ShopCartDto>> SetPincodeAsync(string pincode, CancellationToken cancellationToken = default);

    /// <summary>Adds to the cart.</summary>
    /// <param name="request">SKU, amount, unit.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The cart.</returns>
    Task<ApiResult<ShopCartDto>> AddLineAsync(AddCartLineRequest request, CancellationToken cancellationToken = default);

    /// <summary>Changes a cart line.</summary>
    /// <param name="skuId">SKU of the line.</param>
    /// <param name="request">New amount and unit.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The cart.</returns>
    Task<ApiResult<ShopCartDto>> UpdateLineAsync(Guid skuId, UpdateCartLineRequest request, CancellationToken cancellationToken = default);

    /// <summary>Removes a cart line.</summary>
    /// <param name="skuId">SKU of the line.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome.</returns>
    Task<ApiResult> RemoveLineAsync(Guid skuId, CancellationToken cancellationToken = default);

    /// <summary>Places the order.</summary>
    /// <param name="request">Address, phone, key.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The order.</returns>
    Task<ApiResult<ShopOrderDto>> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default);

    /// <summary>The customer's orders.</summary>
    /// <param name="request">Page.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One page.</returns>
    Task<ApiResult<PagedResult<ShopOrderSummaryDto>>> GetOrdersAsync(PagedRequest request, CancellationToken cancellationToken = default);

    /// <summary>One of the customer's orders.</summary>
    /// <param name="id">Order id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The order.</returns>
    Task<ApiResult<ShopOrderDto>> GetOrderAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Cancels one of the customer's orders.</summary>
    /// <param name="id">Order id.</param>
    /// <param name="request">Why.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The order.</returns>
    Task<ApiResult<ShopOrderDto>> CancelOrderAsync(Guid id, CancelOrderRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IShopApiClient"/>.
/// </summary>
public sealed class ShopApiClient : IShopApiClient
{
    private readonly IApiClient _api;

    /// <summary>
    /// Creates the client.
    /// </summary>
    /// <param name="api">Underlying JSON client.</param>
    public ShopApiClient(IApiClient api)
    {
        _api = api;
    }

    /// <inheritdoc />
    public Task<ApiResult<List<ShopCategoryDto>>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        _api.GetAsync<List<ShopCategoryDto>>($"{ApiRoutes.Storefront}/categories", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<List<ShopBrandDto>>> GetBrandsAsync(CancellationToken cancellationToken = default) =>
        _api.GetAsync<List<ShopBrandDto>>($"{ApiRoutes.Storefront}/brands", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<PagedResult<ShopProductCardDto>>> GetProductsAsync(ShopProductRequest request, CancellationToken cancellationToken = default) =>
        _api.GetAsync<PagedResult<ShopProductCardDto>>(ApiQuery.Build($"{ApiRoutes.Storefront}/products", request,
            (nameof(request.CategoryId), request.CategoryId?.ToString()),
            (nameof(request.BrandId), request.BrandId?.ToString()),
            (nameof(request.Pincode), request.Pincode)), cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<ShopProductDto>> GetProductAsync(Guid id, string? pincode, CancellationToken cancellationToken = default) =>
        _api.GetAsync<ShopProductDto>(ApiQuery.Build($"{ApiRoutes.Storefront}/products/{id}", null, ("pincode", pincode)), cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<ShopSessionDto>> RegisterAsync(ShopRegisterRequest request, CancellationToken cancellationToken = default) =>
        _api.PostAsync<ShopRegisterRequest, ShopSessionDto>($"{ApiRoutes.Storefront}/auth/register", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<ShopSessionDto>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) =>
        _api.PostAsync<LoginRequest, ShopSessionDto>($"{ApiRoutes.Storefront}/auth/login", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<ShopProfileDto>> GetProfileAsync(CancellationToken cancellationToken = default) =>
        _api.GetAsync<ShopProfileDto>($"{ApiRoutes.Storefront}/me", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<ShopProfileDto>> UpdateProfileAsync(UpdateShopProfileRequest request, CancellationToken cancellationToken = default) =>
        _api.PutAsync<UpdateShopProfileRequest, ShopProfileDto>($"{ApiRoutes.Storefront}/me", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<ShopCartDto>> GetCartAsync(CancellationToken cancellationToken = default) =>
        _api.GetAsync<ShopCartDto>($"{ApiRoutes.Storefront}/cart", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<ShopCartDto>> SetPincodeAsync(string pincode, CancellationToken cancellationToken = default) =>
        _api.PutAsync<SetPincodeRequest, ShopCartDto>($"{ApiRoutes.Storefront}/cart/pincode", new SetPincodeRequest { Pincode = pincode }, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<ShopCartDto>> AddLineAsync(AddCartLineRequest request, CancellationToken cancellationToken = default) =>
        _api.PostAsync<AddCartLineRequest, ShopCartDto>($"{ApiRoutes.Storefront}/cart/lines", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<ShopCartDto>> UpdateLineAsync(Guid skuId, UpdateCartLineRequest request, CancellationToken cancellationToken = default) =>
        _api.PutAsync<UpdateCartLineRequest, ShopCartDto>($"{ApiRoutes.Storefront}/cart/lines/{skuId}", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult> RemoveLineAsync(Guid skuId, CancellationToken cancellationToken = default) =>
        _api.DeleteAsync($"{ApiRoutes.Storefront}/cart/lines/{skuId}", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<ShopOrderDto>> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default) =>
        _api.PostAsync<CheckoutRequest, ShopOrderDto>($"{ApiRoutes.Storefront}/checkout", request, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<PagedResult<ShopOrderSummaryDto>>> GetOrdersAsync(PagedRequest request, CancellationToken cancellationToken = default) =>
        _api.GetAsync<PagedResult<ShopOrderSummaryDto>>(ApiQuery.Build($"{ApiRoutes.Storefront}/orders", request), cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<ShopOrderDto>> GetOrderAsync(Guid id, CancellationToken cancellationToken = default) =>
        _api.GetAsync<ShopOrderDto>($"{ApiRoutes.Storefront}/orders/{id}", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<ShopOrderDto>> CancelOrderAsync(Guid id, CancelOrderRequest request, CancellationToken cancellationToken = default) =>
        _api.PostAsync<CancelOrderRequest, ShopOrderDto>($"{ApiRoutes.Storefront}/orders/{id}/cancel", request, cancellationToken);
}
