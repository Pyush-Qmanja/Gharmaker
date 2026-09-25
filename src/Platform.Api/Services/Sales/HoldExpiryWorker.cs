using Google.Cloud.Firestore;
using Microsoft.Extensions.Options;
using Platform.Api.Common;
using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Services.Storefront;
using Platform.Shared.Entities.Common;
using Platform.Shared.Entities.Sales;

namespace Platform.Api.Services.Sales;

/// <summary>
/// Gives back the stock of orders nobody confirmed in time (P4: holds expire).
/// Runs in the background, finds placed orders whose hold has expired in any
/// organisation, and closes each as that organisation through <see cref="IOrderCloser"/>.
/// </summary>
public sealed class HoldExpiryWorker : BackgroundService
{
    /// <summary>Orders handled per sweep.</summary>
    private const int BatchSize = 50;

    private static readonly string StatusField = FirestoreNaming.Field(nameof(Order.Status));
    private static readonly string HoldExpiresAtField = FirestoreNaming.Field(nameof(Order.HoldExpiresAt));
    private static readonly string OrgIdField = FirestoreNaming.Field(nameof(IOrgScoped.OrgId));

    private readonly IServiceScopeFactory _scopes;
    private readonly IFirestoreContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly StorefrontOptions _options;
    private readonly ILogger<HoldExpiryWorker> _logger;

    /// <summary>
    /// Creates the worker.
    /// </summary>
    /// <param name="scopes">Creates a service scope per order.</param>
    /// <param name="context">Firestore, for the cross-organisation query.</param>
    /// <param name="timeProvider">Clock.</param>
    /// <param name="options">Sweep interval.</param>
    /// <param name="logger">Logs each expiry and failure.</param>
    public HoldExpiryWorker(
        IServiceScopeFactory scopes,
        IFirestoreContext context,
        TimeProvider timeProvider,
        IOptions<StorefrontOptions> options,
        ILogger<HoldExpiryWorker> logger)
    {
        _scopes = scopes;
        _context = context;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Sweeps until the app stops.
    /// </summary>
    /// <param name="stoppingToken">Signals shutdown.</param>
    /// <returns>A task that ends at shutdown.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.ExpirySweepSeconds));
        do
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Hold expiry sweep failed; will retry.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// Closes every placed order whose hold has expired.
    /// </summary>
    /// <param name="cancellationToken">Signals shutdown.</param>
    /// <returns>A task that completes after one sweep.</returns>
    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        QuerySnapshot due = await _context.Collection<Order>()
            .WhereEqualTo(StatusField, DocumentConverter.ToFirestoreValue(OrderStatus.Placed))
            .WhereLessThanOrEqualTo(HoldExpiresAtField, DocumentConverter.ToFirestoreValue(now))
            .Limit(BatchSize)
            .GetSnapshotAsync(cancellationToken);

        foreach (DocumentSnapshot document in due.Documents)
        {
            Guid orgId = Guid.Parse(document.GetValue<string>(OrgIdField));
            Guid orderId = Guid.Parse(document.Id);
            using (SystemIdentity.Use(orgId, userId: null))
            {
                await using AsyncServiceScope scope = _scopes.CreateAsyncScope();
                try
                {
                    Order order = await scope.ServiceProvider.GetRequiredService<IOrderCloser>()
                        .CloseAsync(orderId, OrderStatus.Expired, "Not confirmed in time; the held stock was released.", customerId: null, cancellationToken);
                    _logger.LogInformation("Order {ReferenceNo} expired; its stock hold was released.", order.ReferenceNo);
                }
                catch (AppException ex)
                {
                    // Closed by someone else between the query and the transaction.
                    _logger.LogInformation("Order {OrderId} not expired: {Reason}", orderId, ex.Message);
                }
            }
        }
    }
}
