using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Api.Security;
using Platform.Api.Security.Authorization;
using Platform.Api.Services.Storefront;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Auth;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Sales;
using Platform.Shared.Dtos.Storefront;

namespace Platform.Api.Controllers.Storefront;

// Customer-facing endpoints (P1). Every action here returns only types from
// Platform.Shared.Dtos.Storefront, and each one is listed in the opacity test
// suite (tests/Platform.Tests.Opacity): adding an endpoint means adding it there
// in the same change.

/// <summary>
/// Base of every storefront controller: JSON, and every action runs as the
/// store's organisation (<see cref="StorefrontIdentityFilter"/>).
/// </summary>
[ApiController]
[Produces("application/json")]
[TypeFilter(typeof(StorefrontIdentityFilter))]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public abstract class StorefrontControllerBase : ControllerBase
{
}

/// <summary>
/// The store's catalogue, open to everyone. Prices follow the signed-in
/// customer's tier or contract; availability needs a PIN code.
/// </summary>
[Route(ApiRoutes.Storefront)]
public sealed class ShopCatalogController : StorefrontControllerBase
{
    private readonly IShopCatalogService _catalog;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="catalog">Storefront catalogue service.</param>
    public ShopCatalogController(IShopCatalogService catalog)
    {
        _catalog = catalog;
    }

    /// <summary>
    /// The category menu.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>Top-level categories with children.</returns>
    [HttpGet("categories")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ShopCategoryDto>>> Categories(CancellationToken cancellationToken) =>
        Ok(await _catalog.GetCategoriesAsync(cancellationToken));

    /// <summary>
    /// The brands on sale.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>Brands.</returns>
    [HttpGet("brands")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ShopBrandDto>>> Brands(CancellationToken cancellationToken) =>
        Ok(await _catalog.GetBrandsAsync(cancellationToken));

    /// <summary>
    /// One page of products.
    /// </summary>
    /// <param name="request">Category, brand, search, PIN code and page.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>Product cards.</returns>
    [HttpGet("products")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ShopProductCardDto>>> Products([FromQuery] ShopProductRequest request, CancellationToken cancellationToken) =>
        Ok(await _catalog.GetProductsAsync(request, cancellationToken));

    /// <summary>
    /// One product page.
    /// </summary>
    /// <param name="id">Product id.</param>
    /// <param name="pincode">Customer's PIN code, for availability.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The product.</returns>
    [HttpGet("products/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShopProductDto>> Product(Guid id, [FromQuery] string? pincode, CancellationToken cancellationToken) =>
        Ok(await _catalog.GetProductAsync(id, pincode, cancellationToken));
}

/// <summary>
/// Customer accounts: register, sign in, profile.
/// </summary>
[Route(ApiRoutes.Storefront)]
public sealed class ShopAccountController : StorefrontControllerBase
{
    private readonly IShopAccountService _accounts;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="accounts">Account service.</param>
    public ShopAccountController(IShopAccountService accounts)
    {
        _accounts = accounts;
    }

    /// <summary>
    /// Opens a customer account and signs in.
    /// </summary>
    /// <param name="request">Name, email, password.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The session.</returns>
    [HttpPost("auth/register")]
    [EnableRateLimiting(RateLimitPolicies.Register)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ShopSessionDto>> Register([FromBody] ShopRegisterRequest request, CancellationToken cancellationToken) =>
        Ok(await _accounts.RegisterAsync(request, cancellationToken));

    /// <summary>
    /// Signs a customer in.
    /// </summary>
    /// <param name="request">Email and password.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The session.</returns>
    [HttpPost("auth/login")]
    [EnableRateLimiting(RateLimitPolicies.SignIn)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ShopSessionDto>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken) =>
        Ok(await _accounts.LoginAsync(request, cancellationToken));

    /// <summary>
    /// The signed-in customer's profile.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The profile.</returns>
    [HttpGet("me")]
    [CustomerOnly]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ShopProfileDto>> Me(CancellationToken cancellationToken) =>
        Ok(await _accounts.GetProfileAsync(cancellationToken));

    /// <summary>
    /// Changes the signed-in customer's profile.
    /// </summary>
    /// <param name="request">New values.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The profile.</returns>
    [HttpPut("me")]
    [CustomerOnly]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ShopProfileDto>> UpdateMe([FromBody] UpdateShopProfileRequest request, CancellationToken cancellationToken) =>
        Ok(await _accounts.UpdateProfileAsync(request, cancellationToken));
}

/// <summary>
/// The signed-in customer's cart and checkout.
/// </summary>
[Route(ApiRoutes.Storefront)]
[CustomerOnly]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public sealed class ShopCartController : StorefrontControllerBase
{
    private readonly IShopCartService _cart;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="cart">Cart service.</param>
    public ShopCartController(IShopCartService cart)
    {
        _cart = cart;
    }

    /// <summary>
    /// The cart, priced now.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The cart.</returns>
    [HttpGet("cart")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ShopCartDto>> Get(CancellationToken cancellationToken) =>
        Ok(await _cart.GetAsync(cancellationToken));

    /// <summary>
    /// Sets the delivery PIN code.
    /// </summary>
    /// <param name="request">PIN code.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The cart.</returns>
    [HttpPut("cart/pincode")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ShopCartDto>> SetPincode([FromBody] SetPincodeRequest request, CancellationToken cancellationToken) =>
        Ok(await _cart.SetPincodeAsync(request, cancellationToken));

    /// <summary>
    /// Adds an amount of a SKU.
    /// </summary>
    /// <param name="request">SKU, amount, unit.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The cart.</returns>
    [HttpPost("cart/lines")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShopCartDto>> AddLine([FromBody] AddCartLineRequest request, CancellationToken cancellationToken) =>
        Ok(await _cart.AddLineAsync(request, cancellationToken));

    /// <summary>
    /// Changes a line.
    /// </summary>
    /// <param name="skuId">SKU of the line.</param>
    /// <param name="request">New amount and unit.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The cart.</returns>
    [HttpPut("cart/lines/{skuId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShopCartDto>> UpdateLine(Guid skuId, [FromBody] UpdateCartLineRequest request, CancellationToken cancellationToken) =>
        Ok(await _cart.UpdateLineAsync(skuId, request, cancellationToken));

    /// <summary>
    /// Removes a line.
    /// </summary>
    /// <param name="skuId">SKU of the line.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The cart.</returns>
    [HttpDelete("cart/lines/{skuId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShopCartDto>> RemoveLine(Guid skuId, CancellationToken cancellationToken) =>
        Ok(await _cart.RemoveLineAsync(skuId, cancellationToken));

    /// <summary>
    /// Places the order for the cart and holds its stock.
    /// </summary>
    /// <param name="request">Address, phone and checkout key.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The order.</returns>
    [HttpPost("checkout")]
    [EnableRateLimiting(RateLimitPolicies.Checkout)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ShopOrderDto>> Checkout([FromBody] CheckoutRequest request, CancellationToken cancellationToken) =>
        Ok(await _cart.CheckoutAsync(request, cancellationToken));
}

/// <summary>
/// The signed-in customer's orders.
/// </summary>
[Route(ApiRoutes.Storefront + "/orders")]
[CustomerOnly]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public sealed class ShopOrdersController : StorefrontControllerBase
{
    private readonly IShopOrderService _orders;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="orders">Order service.</param>
    public ShopOrdersController(IShopOrderService orders)
    {
        _orders = orders;
    }

    /// <summary>
    /// The customer's orders, newest first.
    /// </summary>
    /// <param name="request">Page.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>One page.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ShopOrderSummaryDto>>> List([FromQuery] PagedRequest request, CancellationToken cancellationToken) =>
        Ok(await _orders.ListAsync(request, cancellationToken));

    /// <summary>
    /// One of the customer's orders.
    /// </summary>
    /// <param name="id">Order id.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The order.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShopOrderDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _orders.GetAsync(id, cancellationToken));

    /// <summary>
    /// Cancels one of the customer's orders while it is only placed.
    /// </summary>
    /// <param name="id">Order id.</param>
    /// <param name="request">Why.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The cancelled order.</returns>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ShopOrderDto>> Cancel(Guid id, [FromBody] CancelOrderRequest request, CancellationToken cancellationToken) =>
        Ok(await _orders.CancelAsync(id, request, cancellationToken));
}
