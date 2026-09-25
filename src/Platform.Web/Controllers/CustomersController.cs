using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Pricing;
using Platform.Shared.Dtos.Sales;
using Platform.Shared.Entities.Pricing;
using Platform.Web.Models;
using Platform.Web.Services.Api;

namespace Platform.Web.Controllers;

/// <summary>
/// Online store customers: list and search, and one customer's details, price
/// tier and status with their latest orders. Customers open their own accounts.
/// </summary>
public sealed class CustomersController : PlatformControllerBase
{
    /// <summary>Orders shown on a customer's page.</summary>
    private const int RecentOrders = 5;

    private readonly ISalesApiClient _sales;
    private readonly ICrudApiClient<PriceListDto, CreatePriceListRequest, UpdatePriceListRequest> _lists;
    private readonly IValidator<UpdateCustomerRequest> _validator;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="sales">Sales API client.</param>
    /// <param name="lists">Price list API client, for tiers.</param>
    /// <param name="validator">Shared customer validator.</param>
    public CustomersController(
        ISalesApiClient sales,
        ICrudApiClient<PriceListDto, CreatePriceListRequest, UpdatePriceListRequest> lists,
        IValidator<UpdateCustomerRequest> validator)
    {
        _sales = sales;
        _lists = lists;
        _validator = validator;
    }

    /// <summary>
    /// Lists customers.
    /// </summary>
    /// <param name="request">Email search and page.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The page.</returns>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        var customers = await _sales.GetCustomersAsync(request, cancellationToken);
        if (HandleAccess(customers) is { } denied)
        {
            return denied;
        }

        return View(new ListViewModel<CustomerDto>(
            "Customers",
            customers.Value ?? new PagedResult<CustomerDto> { Page = request.Page, PageSize = request.PageSize },
            request.Search,
            itemName: "customer"));
    }

    /// <summary>
    /// Shows one customer.
    /// </summary>
    /// <param name="id">Customer id.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The page.</returns>
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var customer = await _sales.GetCustomerAsync(id, cancellationToken);
        if (HandleAccess(customer) is { } denied)
        {
            return denied;
        }

        if (customer.Value is not { } found)
        {
            return NotFound();
        }

        var form = new UpdateCustomerRequest
        {
            Name = found.Name,
            Phone = found.Phone,
            CompanyName = found.CompanyName,
            Gstin = found.Gstin,
            TierPriceListId = found.TierPriceListId,
            IsActive = found.IsActive,
        };
        return await PageAsync(found, form, cancellationToken);
    }

    /// <summary>
    /// Saves a customer.
    /// </summary>
    /// <param name="id">Customer id.</param>
    /// <param name="form">Posted values.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>Back to the list, or the page with errors.</returns>
    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, UpdateCustomerRequest form, CancellationToken cancellationToken)
    {
        if (await ValidateAsync(_validator, form, cancellationToken, prefix: "Form"))
        {
            var result = await _sales.UpdateCustomerAsync(id, form, cancellationToken);
            if (HandleAccess(result) is { } denied)
            {
                return denied;
            }

            if (result.IsSuccess)
            {
                FlashSuccess(result.Value!.IsActive ? "Customer saved." : "Customer blocked: they can no longer sign in or order.");
                return RedirectToAction(nameof(Index));
            }

            AddApiErrors(result, prefix: "Form");
        }

        var customer = await _sales.GetCustomerAsync(id, cancellationToken);
        return customer.Value is null ? NotFound() : await PageAsync(customer.Value, form, cancellationToken);
    }

    /// <summary>
    /// Builds the customer page with tier options and latest orders.
    /// </summary>
    /// <param name="customer">Customer.</param>
    /// <param name="form">Form values.</param>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>The page.</returns>
    private async Task<IActionResult> PageAsync(CustomerDto customer, UpdateCustomerRequest form, CancellationToken cancellationToken)
    {
        var lists = await _lists.GetPagedAsync(new PagedRequest { PageSize = Paging.MaxPageSize }, cancellationToken);
        var tiers = (lists.Value?.Items ?? new List<PriceListDto>())
            .Where(l => l.Type == PriceListType.Tier && l.IsActive)
            .Select(l => new SelectListItem(l.Name, l.Id.ToString(), l.Id == form.TierPriceListId))
            .ToList();
        var orders = await _sales.GetOrdersAsync(new OrderListRequest { CustomerId = customer.Id, PageSize = RecentOrders }, cancellationToken);
        return View(nameof(Edit), new CustomerEditViewModel(customer, form, tiers, orders.Value?.Items ?? new List<OrderSummaryDto>()));
    }
}
