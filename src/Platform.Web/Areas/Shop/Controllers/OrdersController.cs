using Microsoft.AspNetCore.Mvc;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Sales;
using Platform.Shared.Dtos.Storefront;
using Platform.Web.Areas.Shop.Models;
using Platform.Web.Models;
using Platform.Web.Services.Api;

namespace Platform.Web.Areas.Shop.Controllers;

/// <summary>
/// The customer's orders: list, one order with its GST and delivery window, and cancelling.
/// </summary>
[CustomerRequired]
public sealed class OrdersController : ShopControllerBase
{
    private readonly IShopApiClient _shop;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="shop">Storefront API client.</param>
    public OrdersController(IShopApiClient shop)
    {
        _shop = shop;
    }

    /// <summary>
    /// Lists the customer's orders.
    /// </summary>
    /// <param name="page">Page.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The list.</returns>
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var request = new PagedRequest { Page = Math.Max(page, 1) };
        var orders = await _shop.GetOrdersAsync(request, cancellationToken);
        if (await SessionEndedAsync(orders) is { } ended)
        {
            return ended;
        }

        return View(new ListViewModel<ShopOrderSummaryDto>(
            "My orders",
            orders.Value ?? new PagedResult<ShopOrderSummaryDto> { Page = request.Page, PageSize = request.PageSize },
            search: null,
            itemName: "order"));
    }

    /// <summary>
    /// Shows one of the customer's orders.
    /// </summary>
    /// <param name="id">Order id.</param>
    /// <param name="placed">True right after checkout.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The order, or 404.</returns>
    [HttpGet]
    public async Task<IActionResult> Details(Guid id, bool placed = false, CancellationToken cancellationToken = default)
    {
        var order = await _shop.GetOrderAsync(id, cancellationToken);
        if (await SessionEndedAsync(order) is { } ended)
        {
            return ended;
        }

        return order.Value is null ? NotFound() : View(new ShopOrderViewModel(order.Value, placed));
    }

    /// <summary>
    /// Cancels one of the customer's orders.
    /// </summary>
    /// <param name="id">Order id.</param>
    /// <param name="form">Reason.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>Back to the order.</returns>
    [HttpPost]
    public async Task<IActionResult> Cancel(Guid id, CancelOrderRequest form, CancellationToken cancellationToken)
    {
        var result = await _shop.CancelOrderAsync(id, form, cancellationToken);
        if (await SessionEndedAsync(result) is { } ended)
        {
            return ended;
        }

        if (result.IsSuccess)
        {
            FlashSuccess($"Order {result.Value!.ReferenceNo} is cancelled.");
        }
        else
        {
            FlashError(result.ErrorMessage ?? "Could not cancel the order.");
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
