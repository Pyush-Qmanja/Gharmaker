using Google.Cloud.Firestore;
using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Mapping.Sales;
using Platform.Api.Repositories;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Sales;
using Platform.Shared.Entities.Common;
using Platform.Shared.Entities.Inventory;
using Platform.Shared.Entities.Sales;

namespace Platform.Api.Services.Sales;

/// <summary>
/// Customer orders as staff see them: the list, one order with where its
/// stock is held, and cancelling.
/// </summary>
public interface IOrderService
{
    /// <summary>
    /// Lists orders, newest first.
    /// </summary>
    /// <param name="request">Status, customer, reference prefix and page.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One page of orders.</returns>
    Task<PagedResult<OrderSummaryDto>> ListAsync(OrderListRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads one order in full.
    /// </summary>
    /// <param name="id">Order id.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The order.</returns>
    Task<OrderDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an order that is only placed and releases its stock.
    /// </summary>
    /// <param name="id">Order id.</param>
    /// <param name="request">Why.</param>
    /// <param name="cancellationToken">Cancels the transaction.</param>
    /// <returns>The cancelled order.</returns>
    Task<OrderDto> CancelAsync(Guid id, CancelOrderRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms a placed order after speaking to the customer: its stock stays
    /// held, without expiry, until dispatch. Refused once the hold has run out.
    /// </summary>
    /// <param name="id">Order id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The confirmed order.</returns>
    Task<OrderDto> ConfirmAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Firestore-backed <see cref="IOrderService"/>.
/// </summary>
public sealed class OrderService : IOrderService
{
    private static readonly string CreatedAtField = FirestoreNaming.Field(nameof(BaseEntity.CreatedAt));
    private static readonly string StatusField = FirestoreNaming.Field(nameof(Order.Status));
    private static readonly string CustomerIdField = FirestoreNaming.Field(nameof(Order.CustomerId));
    private static readonly string ReferenceNoField = FirestoreNaming.Field(nameof(Order.ReferenceNo));
    private static readonly string OrderIdField = FirestoreNaming.Field(nameof(StockHold.OrderId));

    private readonly IRepository<Order> _orders;
    private readonly IRepository<StockHold> _holds;
    private readonly IRepository<Warehouse> _warehouses;
    private readonly IOrderCloser _closer;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="orders">Order data access.</param>
    /// <param name="holds">Stock hold data access.</param>
    /// <param name="warehouses">Warehouse data access, for hold codes.</param>
    /// <param name="closer">Cancels orders.</param>
    /// <param name="unitOfWork">Runs the confirmation as one transaction.</param>
    /// <param name="timeProvider">Current time.</param>
    public OrderService(IRepository<Order> orders, IRepository<StockHold> holds, IRepository<Warehouse> warehouses, IOrderCloser closer, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _orders = orders;
        _holds = holds;
        _warehouses = warehouses;
        _closer = closer;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<PagedResult<OrderSummaryDto>> ListAsync(OrderListRequest request, CancellationToken cancellationToken = default)
    {
        Query query = _orders.Query();
        if (request.Status is { } status)
        {
            query = query.WhereEqualTo(StatusField, DocumentConverter.ToFirestoreValue(status));
        }

        if (request.CustomerId is { } customerId)
        {
            query = query.WhereEqualTo(CustomerIdField, DocumentConverter.ToFirestoreValue(customerId));
        }

        query = string.IsNullOrWhiteSpace(request.Search)
            ? query.OrderByDescending(CreatedAtField)
            : query.WhereStartsWith(ReferenceNoField, request.Search.Trim().ToUpperInvariant());

        PagedResult<Order> page = await _orders.GetPagedAsync(query, request, cancellationToken);
        return new PagedResult<OrderSummaryDto>
        {
            Items = page.Items.Select(OrderMapping.ToSummaryDto).ToList(),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
        };
    }

    /// <inheritdoc />
    public async Task<OrderDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Order order = await _orders.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Order");
        return await ToDtoAsync(order, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OrderDto> CancelAsync(Guid id, CancelOrderRequest request, CancellationToken cancellationToken = default)
    {
        Order order = await _closer.CloseAsync(id, OrderStatus.Cancelled, request.Remarks?.Trim() is { Length: > 0 } why ? why : "Cancelled by staff", customerId: null, cancellationToken);
        return await ToDtoAsync(order, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OrderDto> ConfirmAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Which holds exist is fixed at checkout, so they can be found before the transaction and re-read inside it.
        List<Guid> holdIds = (await _holds.ListAsync(
                _holds.Query().WhereEqualTo(OrderIdField, DocumentConverter.ToFirestoreValue(id)), cancellationToken))
            .Select(h => h.Id)
            .ToList();

        Order confirmed = await _unitOfWork.RunInTransactionAsync(async session =>
        {
            Order order = await session.GetAsync<Order>(id) ?? throw new NotFoundException("Order");
            if (order.Status != OrderStatus.Placed)
            {
                throw new BusinessRuleException($"Order {order.ReferenceNo} is {order.Status.ToString().ToLowerInvariant()}, not waiting for confirmation.");
            }

            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            if (now >= order.HoldExpiresAt)
            {
                throw new BusinessRuleException($"The stock hold for {order.ReferenceNo} has run out, so it is being closed. Ask the customer to place the order again.");
            }

            foreach (StockHold hold in (await session.GetManyAsync<StockHold>(holdIds)).Values.Where(h => h.Status == StockHoldStatus.Active))
            {
                hold.Status = StockHoldStatus.Confirmed;
                session.Update(hold);
            }

            order.Status = OrderStatus.Confirmed;
            order.ConfirmedAt = now;
            session.Update(order);
            return order;
        }, cancellationToken);

        return await ToDtoAsync(confirmed, cancellationToken);
    }

    /// <summary>
    /// Maps an order with its holds and their warehouse codes.
    /// </summary>
    /// <param name="order">Order.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The DTO.</returns>
    private async Task<OrderDto> ToDtoAsync(Order order, CancellationToken cancellationToken)
    {
        IReadOnlyList<StockHold> holds = await _holds.ListAsync(
            _holds.Query().WhereEqualTo(OrderIdField, DocumentConverter.ToFirestoreValue(order.Id)), cancellationToken);
        var codes = (await _warehouses.GetByIdsAsync(holds.Select(h => h.WarehouseId), cancellationToken)).ToDictionary(w => w.Id, w => w.Code);
        return OrderMapping.ToDto(order, holds, codes);
    }
}
