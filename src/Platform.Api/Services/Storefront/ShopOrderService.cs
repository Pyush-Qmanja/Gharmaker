using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Mapping.Storefront;
using Platform.Api.Repositories;
using Platform.Api.Services.Sales;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Sales;
using Platform.Shared.Dtos.Storefront;
using Platform.Shared.Entities.Common;
using Platform.Shared.Entities.Sales;

namespace Platform.Api.Services.Storefront;

/// <summary>
/// The signed-in customer's own orders. Another customer's order does not
/// exist as far as they can tell (404, never 403 — P6).
/// </summary>
public interface IShopOrderService
{
    /// <summary>
    /// Lists the customer's orders, newest first.
    /// </summary>
    /// <param name="request">Page.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One page of orders.</returns>
    Task<PagedResult<ShopOrderSummaryDto>> ListAsync(PagedRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads one of the customer's orders.
    /// </summary>
    /// <param name="id">Order id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The order.</returns>
    Task<ShopOrderDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels one of the customer's orders while it is only placed.
    /// </summary>
    /// <param name="id">Order id.</param>
    /// <param name="request">Why.</param>
    /// <param name="cancellationToken">Cancels the transaction.</param>
    /// <returns>The cancelled order.</returns>
    Task<ShopOrderDto> CancelAsync(Guid id, CancelOrderRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Firestore-backed <see cref="IShopOrderService"/>.
/// </summary>
public sealed class ShopOrderService : IShopOrderService
{
    private static readonly string CustomerIdField = FirestoreNaming.Field(nameof(Order.CustomerId));
    private static readonly string CreatedAtField = FirestoreNaming.Field(nameof(BaseEntity.CreatedAt));

    private readonly ICustomerContext _customer;
    private readonly IRepository<Order> _orders;
    private readonly IOrderCloser _closer;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="customer">Signed-in customer.</param>
    /// <param name="orders">Order data access.</param>
    /// <param name="closer">Cancels orders.</param>
    public ShopOrderService(ICustomerContext customer, IRepository<Order> orders, IOrderCloser closer)
    {
        _customer = customer;
        _orders = orders;
        _closer = closer;
    }

    /// <inheritdoc />
    public async Task<PagedResult<ShopOrderSummaryDto>> ListAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        Customer customer = await _customer.RequireAsync(cancellationToken);
        PagedResult<Order> page = await _orders.GetPagedAsync(
            _orders.Query()
                .WhereEqualTo(CustomerIdField, DocumentConverter.ToFirestoreValue(customer.Id))
                .OrderByDescending(CreatedAtField),
            request,
            cancellationToken);
        return new PagedResult<ShopOrderSummaryDto>
        {
            Items = page.Items.Select(ShopMapping.ToOrderSummaryDto).ToList(),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
        };
    }

    /// <inheritdoc />
    public async Task<ShopOrderDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Customer customer = await _customer.RequireAsync(cancellationToken);
        Order order = await _orders.GetByIdAsync(id, cancellationToken) is { } found && found.CustomerId == customer.Id
            ? found
            : throw new NotFoundException("Order");
        return ShopMapping.ToOrderDto(order);
    }

    /// <inheritdoc />
    public async Task<ShopOrderDto> CancelAsync(Guid id, CancelOrderRequest request, CancellationToken cancellationToken = default)
    {
        Customer customer = await _customer.RequireAsync(cancellationToken);
        string remarks = request.Remarks?.Trim() is { Length: > 0 } why ? why : "Cancelled by the customer";
        Order order = await _closer.CloseAsync(id, OrderStatus.Cancelled, remarks, customer.Id, cancellationToken);
        return ShopMapping.ToOrderDto(order);
    }
}
