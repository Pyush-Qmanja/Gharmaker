using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Repositories;
using Platform.Shared.Common;
using Platform.Shared.Entities.Inventory;
using Platform.Shared.Entities.Sales;

namespace Platform.Api.Services.Sales;

/// <summary>
/// Ends an order that is only placed — cancelled by the customer or staff, or
/// expired because nobody confirmed it — and gives its held stock back (P4),
/// all in one transaction so <c>Reserved</c> always equals the active holds.
/// </summary>
public interface IOrderCloser
{
    /// <summary>
    /// Closes an order and releases its holds.
    /// </summary>
    /// <param name="orderId">Order.</param>
    /// <param name="status"><see cref="OrderStatus.Cancelled"/> or <see cref="OrderStatus.Expired"/>.</param>
    /// <param name="remarks">Why.</param>
    /// <param name="customerId">When a customer asks, their id: someone else's order is "not found".</param>
    /// <param name="cancellationToken">Cancels the transaction.</param>
    /// <returns>The closed order.</returns>
    Task<Order> CloseAsync(Guid orderId, OrderStatus status, string? remarks, Guid? customerId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Firestore-backed <see cref="IOrderCloser"/>.
/// </summary>
public sealed class OrderCloser : IOrderCloser
{
    private static readonly string OrderIdField = FirestoreNaming.Field(nameof(StockHold.OrderId));

    private readonly IRepository<StockHold> _holds;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="holds">Stock hold data access.</param>
    /// <param name="unitOfWork">Runs the transaction.</param>
    /// <param name="timeProvider">Clock.</param>
    public OrderCloser(IRepository<StockHold> holds, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _holds = holds;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<Order> CloseAsync(Guid orderId, OrderStatus status, string? remarks, Guid? customerId, CancellationToken cancellationToken = default)
    {
        if (status is not (OrderStatus.Cancelled or OrderStatus.Expired))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Only cancelled or expired closes an order.");
        }

        // Which holds exist is fixed at checkout, so they can be found before the transaction and re-read inside it.
        List<Guid> holdIds = (await _holds.ListAsync(
                _holds.Query().WhereEqualTo(OrderIdField, DocumentConverter.ToFirestoreValue(orderId)), cancellationToken))
            .Select(h => h.Id)
            .ToList();

        return await _unitOfWork.RunInTransactionAsync(async session =>
        {
            Order? order = await session.GetAsync<Order>(orderId);
            if (order is null || (customerId is { } owner && order.CustomerId != owner))
            {
                throw new NotFoundException("Order");
            }

            // Staff may cancel a confirmed order until it is dispatched; a customer
            // must call, and only a merely placed order can expire.
            bool staffCancelsConfirmed = order.Status == OrderStatus.Confirmed && status == OrderStatus.Cancelled && customerId is null;
            if (order.Status == OrderStatus.Confirmed && customerId is not null)
            {
                throw new BusinessRuleException($"Order {order.ReferenceNo} is confirmed. Please call us to change or cancel it.");
            }

            if (order.Status != OrderStatus.Placed && !staffCancelsConfirmed)
            {
                throw new BusinessRuleException($"Order {order.ReferenceNo} is already {order.Status.ToString().ToLowerInvariant()}.");
            }

            List<StockHold> active = (await session.GetManyAsync<StockHold>(holdIds)).Values
                .Where(h => h.Status is StockHoldStatus.Active or StockHoldStatus.Confirmed)
                .ToList();
            IReadOnlyDictionary<Guid, StockBalance> balances = await session.GetManyAsync<StockBalance>(
                active.Select(h => StockBalance.IdFor(h.WarehouseId, h.SkuId)));

            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            foreach (StockHold hold in active)
            {
                if (balances.TryGetValue(StockBalance.IdFor(hold.WarehouseId, hold.SkuId), out StockBalance? balance))
                {
                    balance.Reserved = Math.Max(0, Quantity.Normalise(balance.Reserved - hold.Quantity));
                    session.Update(balance);
                }

                hold.Status = status == OrderStatus.Expired ? StockHoldStatus.Expired : StockHoldStatus.Released;
                hold.ReleasedAt = now;
                session.Update(hold);
            }

            order.Status = status;
            order.ClosedAt = now;
            order.Remarks = remarks;
            session.Update(order);
            return order;
        }, cancellationToken);
    }
}
