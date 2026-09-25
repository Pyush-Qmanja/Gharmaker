using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Sales;
using Platform.Web.Models;
using Platform.Web.Services.Api;
using Platform.Web.Services.Auth;

namespace Platform.Web.Controllers;

/// <summary>
/// Online store orders for staff: list by status, one order with its GST and
/// where its stock is held, and cancelling an order that is only placed.
/// </summary>
public sealed class OrdersController : PlatformControllerBase
{
    private readonly ISalesApiClient _sales;
    private readonly IUserAccess _access;
    private readonly IValidator<CancelOrderRequest> _cancelValidator;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="sales">Sales API client.</param>
    /// <param name="access">Current user's capabilities.</param>
    /// <param name="cancelValidator">Shared cancel validator.</param>
    public OrdersController(ISalesApiClient sales, IUserAccess access, IValidator<CancelOrderRequest> cancelValidator)
    {
        _sales = sales;
        _access = access;
        _cancelValidator = cancelValidator;
    }

    /// <summary>
    /// Lists orders.
    /// </summary>
    /// <param name="request">Status, reference search and page.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The page.</returns>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] OrderListRequest request, CancellationToken cancellationToken)
    {
        var orders = await _sales.GetOrdersAsync(request, cancellationToken);
        if (HandleAccess(orders) is { } denied)
        {
            return denied;
        }

        return View(new OrdersViewModel(
            new ListViewModel<OrderSummaryDto>(
                "Orders",
                orders.Value ?? new PagedResult<OrderSummaryDto> { Page = request.Page, PageSize = request.PageSize },
                request.Search,
                new Dictionary<string, string?> { ["status"] = request.Status?.ToString(), ["customerId"] = request.CustomerId?.ToString() },
                itemName: "order"),
            request.Status));
    }

    /// <summary>
    /// Shows one order.
    /// </summary>
    /// <param name="id">Order id.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The page.</returns>
    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var order = await _sales.GetOrderAsync(id, cancellationToken);
        if (HandleAccess(order) is { } denied)
        {
            return denied;
        }

        return order.Value is null
            ? NotFound()
            : View(new OrderDetailsViewModel(order.Value, await _access.CanAsync(Capabilities.OrdersManage)));
    }

    /// <summary>
    /// Cancels an order and releases its held stock.
    /// </summary>
    /// <param name="id">Order id.</param>
    /// <param name="form">Reason.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>Back to the order.</returns>
    [HttpPost]
    public async Task<IActionResult> Cancel(Guid id, CancelOrderRequest form, CancellationToken cancellationToken)
    {
        if (!await ValidateAsync(_cancelValidator, form, cancellationToken))
        {
            FlashError(ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage);
            return RedirectToAction(nameof(Details), new { id });
        }

        var result = await _sales.CancelOrderAsync(id, form, cancellationToken);
        if (HandleAccess(result) is { } denied)
        {
            return denied;
        }

        if (result.IsSuccess)
        {
            FlashSuccess($"Order {result.Value!.ReferenceNo} cancelled; its stock is free again.");
        }
        else
        {
            FlashError(result.ErrorMessage ?? "Could not cancel the order.");
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Confirms a placed order after the phone call with the customer.
    /// </summary>
    /// <param name="id">Order id.</param>
    /// <param name="cancellationToken">Aborted when the client disconnects.</param>
    /// <returns>Back to the order with the outcome.</returns>
    [HttpPost]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sales.ConfirmOrderAsync(id, cancellationToken);
        if (HandleAccess(result) is { } denied)
        {
            return denied;
        }

        if (result.IsSuccess)
        {
            FlashSuccess($"Order {result.Value!.ReferenceNo} confirmed. Its stock stays held until dispatch.");
        }
        else
        {
            FlashError(result.ErrorMessage ?? "Could not confirm the order.");
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
