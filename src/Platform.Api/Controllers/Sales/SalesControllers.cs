using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.Security.Authorization;
using Platform.Api.Services.Sales;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Sales;

namespace Platform.Api.Controllers.Sales;

/// <summary>
/// Customer accounts (staff side): list, view, change tier or block.
/// </summary>
[ApiController]
[Authorize]
[Route(ApiRoutes.Customers)]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class CustomersController : ControllerBase
{
    private readonly ICustomerService _customers;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="customers">Customer service.</param>
    public CustomersController(ICustomerService customers)
    {
        _customers = customers;
    }

    /// <summary>
    /// Lists customers, newest first, or by email prefix.
    /// </summary>
    /// <param name="request">Search and page.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>One page.</returns>
    [HttpGet]
    [RequiresCapability(Capabilities.CustomersView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CustomerDto>>> List([FromQuery] PagedRequest request, CancellationToken cancellationToken) =>
        Ok(await _customers.ListAsync(request, cancellationToken));

    /// <summary>
    /// Loads one customer.
    /// </summary>
    /// <param name="id">Customer id.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The customer.</returns>
    [HttpGet("{id:guid}")]
    [RequiresCapability(Capabilities.CustomersView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _customers.GetAsync(id, cancellationToken));

    /// <summary>
    /// Changes a customer's details, price tier or access.
    /// </summary>
    /// <param name="id">Customer id.</param>
    /// <param name="request">New values.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The customer.</returns>
    [HttpPut("{id:guid}")]
    [RequiresCapability(Capabilities.CustomersManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDto>> Update(Guid id, [FromBody] UpdateCustomerRequest request, CancellationToken cancellationToken) =>
        Ok(await _customers.UpdateAsync(id, request, cancellationToken));
}

/// <summary>
/// Customer orders (staff side), including where their stock is held.
/// </summary>
[ApiController]
[Authorize]
[Route(ApiRoutes.Orders)]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="orders">Order service.</param>
    public OrdersController(IOrderService orders)
    {
        _orders = orders;
    }

    /// <summary>
    /// Lists orders, newest first.
    /// </summary>
    /// <param name="request">Status, customer, reference prefix and page.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>One page.</returns>
    [HttpGet]
    [RequiresCapability(Capabilities.OrdersView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OrderSummaryDto>>> List([FromQuery] OrderListRequest request, CancellationToken cancellationToken) =>
        Ok(await _orders.ListAsync(request, cancellationToken));

    /// <summary>
    /// Loads one order.
    /// </summary>
    /// <param name="id">Order id.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The order.</returns>
    [HttpGet("{id:guid}")]
    [RequiresCapability(Capabilities.OrdersView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _orders.GetAsync(id, cancellationToken));

    /// <summary>
    /// Cancels an order that is only placed and releases its held stock.
    /// </summary>
    /// <param name="id">Order id.</param>
    /// <param name="request">Why.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The cancelled order.</returns>
    [HttpPost("{id:guid}/cancel")]
    [RequiresCapability(Capabilities.OrdersManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrderDto>> Cancel(Guid id, [FromBody] CancelOrderRequest request, CancellationToken cancellationToken) =>
        Ok(await _orders.CancelAsync(id, request, cancellationToken));

    /// <summary>
    /// Confirms a placed order after speaking to the customer. Its stock stays held, without expiry, until dispatch.
    /// </summary>
    /// <param name="id">Order id.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>The confirmed order.</returns>
    [HttpPost("{id:guid}/confirm")]
    [RequiresCapability(Capabilities.OrdersManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrderDto>> Confirm(Guid id, CancellationToken cancellationToken) =>
        Ok(await _orders.ConfirmAsync(id, cancellationToken));
}
