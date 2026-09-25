using System.Globalization;
using Platform.Api.Common;
using Platform.Api.Common.Exceptions;
using Platform.Api.Repositories;
using Platform.Shared.Common;
using Platform.Shared.Entities.Common;
using Platform.Shared.Entities.Inventory;

namespace Platform.Api.Services.Inventory.Stock;

/// <summary>
/// One change to on-hand stock that a posting will write as a ledger entry.
/// </summary>
/// <param name="EntryId">Id of the ledger entry (derived, see <see cref="StockPoster.EntryId"/>).</param>
/// <param name="WarehouseId">Warehouse.</param>
/// <param name="WarehouseCode">Warehouse code, for messages.</param>
/// <param name="Line">The document line that moves.</param>
/// <param name="SignedQuantity">Effect on on-hand in the SKU's base unit: positive in, negative out.</param>
/// <param name="Reason">Reason written on the entry.</param>
/// <param name="ReversesEntryId">The entry this one cancels, for a reversal.</param>
public sealed record StockMovement(
    Guid EntryId,
    Guid WarehouseId,
    string WarehouseCode,
    StockDocumentLine Line,
    decimal SignedQuantity,
    StockReason Reason,
    Guid? ReversesEntryId = null);

/// <summary>
/// What one posting writes: the document (new, or an existing one moving on),
/// its movements, and any other document it changes (e.g. the original of a reversal).
/// </summary>
public sealed class StockPosting
{
    /// <summary>The document being posted or moved on.</summary>
    public required StockDocument Document { get; init; }

    /// <summary>True when <see cref="Document"/> is new and needs a reference number.</summary>
    public bool IsNew { get; init; }

    /// <summary>Prefix of its reference number series, e.g. <c>GRN</c>; new documents only.</summary>
    public string? Series { get; init; }

    /// <summary>Stock movements to write.</summary>
    public List<StockMovement> Movements { get; } = new();

    /// <summary>Other documents read in the same transaction that change too.</summary>
    public List<StockDocument> Updates { get; } = new();
}

/// <summary>
/// The only code that writes stock (P2). In one Firestore transaction it
/// reads the balances a posting touches, refuses to take any below zero,
/// numbers a new document, and writes the document, one append-only ledger
/// entry per movement, and the updated balances — all or nothing. Firestore
/// retries the transaction if a balance changed meanwhile, so two postings on
/// the same stock can never both spend it.
/// </summary>
public interface IStockPoster
{
    /// <summary>
    /// Posts stock movements.
    /// </summary>
    /// <param name="plan">
    /// Reads anything it must re-check inside the transaction (e.g. the transfer
    /// being received) and returns what to write. May run more than once.
    /// </param>
    /// <param name="cancellationToken">Cancels the transaction.</param>
    /// <returns>The posted document.</returns>
    /// <exception cref="BusinessRuleException">A movement would take stock below zero.</exception>
    Task<StockDocument> PostAsync(Func<ITransactionSession, Task<StockPosting>> plan, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IStockPoster"/>.
/// </summary>
public sealed class StockPoster : IStockPoster
{
    /// <summary><see cref="StockLedgerEntry.RefType"/> of entries written for stock documents.</summary>
    public const string DocumentRefType = nameof(StockDocument);

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates the poster.
    /// </summary>
    /// <param name="unitOfWork">Runs the transaction.</param>
    /// <param name="currentUser">Caller's organisation.</param>
    /// <param name="timeProvider">Clock, for the number series year and last-moved time.</param>
    public StockPoster(IUnitOfWork unitOfWork, ICurrentUser currentUser, TimeProvider timeProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// The id of the ledger entry a document writes for one line in one phase.
    /// Derived, so a reversal can name the entries it cancels without a query,
    /// and a posting repeated by mistake fails instead of writing twice.
    /// </summary>
    /// <param name="documentId">Document.</param>
    /// <param name="lineIndex">Line position.</param>
    /// <param name="phase">"post", "out" or "in".</param>
    /// <returns>The entry id.</returns>
    public static Guid EntryId(Guid documentId, int lineIndex, string phase) =>
        IdGenerator.FromName($"stock_ledger:{documentId:N}:{lineIndex}:{phase}");

    /// <inheritdoc />
    public Task<StockDocument> PostAsync(Func<ITransactionSession, Task<StockPosting>> plan, CancellationToken cancellationToken = default)
    {
        Guid orgId = _currentUser.OrgId ?? throw new InvalidOperationException("Stock can only be posted for an organisation.");

        return _unitOfWork.RunInTransactionAsync(async session =>
        {
            StockPosting posting = await plan(session);
            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            StockDocument document = posting.Document;

            // All reads first: the number counter, then every balance touched.
            if (posting.IsNew)
            {
                document.ReferenceNo = await DocumentNumbers.NextAsync(
                    session, orgId, $"{posting.Series}-{now.Year.ToString(CultureInfo.InvariantCulture)}");
            }

            IReadOnlyDictionary<Guid, StockBalance> stored = await session.GetManyAsync<StockBalance>(
                posting.Movements.Select(m => StockBalance.IdFor(m.WarehouseId, m.Line.SkuId)));

            if (posting.IsNew)
            {
                session.Add(document);
            }
            else
            {
                session.Update(document);
            }

            var balances = new Dictionary<Guid, (StockBalance Balance, bool IsNew)>();
            var shortages = new List<string>();
            foreach (StockMovement movement in posting.Movements)
            {
                Guid balanceId = StockBalance.IdFor(movement.WarehouseId, movement.Line.SkuId);
                if (!balances.TryGetValue(balanceId, out var slot))
                {
                    slot = stored.TryGetValue(balanceId, out StockBalance? existing)
                        ? (existing, false)
                        : (new StockBalance { Id = balanceId, WarehouseId = movement.WarehouseId, SkuId = movement.Line.SkuId, Uom = movement.Line.BaseUom }, true);
                }

                StockBalance balance = slot.Balance;
                if (!string.Equals(balance.Uom, movement.Line.BaseUom, StringComparison.OrdinalIgnoreCase))
                {
                    throw new BusinessRuleException(
                        $"{movement.Line.SkuCode} is held in {balance.Uom} in {movement.WarehouseCode} but its base unit is now {movement.Line.BaseUom}. Correct the SKU before moving it.");
                }

                decimal before = balance.OnHand;
                balance.OnHand = Quantity.Normalise(before + movement.SignedQuantity);

                // Stock held for customer orders (P4) is promised: an outward move may
                // use only what is free, and never takes on-hand below zero.
                if (movement.SignedQuantity < 0 && balance.OnHand < balance.Reserved)
                {
                    string held = balance.Reserved > 0 ? $", {Show(balance.Reserved)} held for orders" : string.Empty;
                    shortages.Add($"{movement.Line.SkuCode} in {movement.WarehouseCode}: {Show(before)} {balance.Uom} on hand{held}, {Show(-movement.SignedQuantity)} needed");
                }
                else if (balance.OnHand < 0)
                {
                    shortages.Add($"{movement.Line.SkuCode} in {movement.WarehouseCode}: {Show(before)} {balance.Uom} on hand, {Show(-movement.SignedQuantity)} needed");
                }

                balance.IsInStock = balance.OnHand > 0;
                balance.LastMovedAt = now;
                balance.SkuCode = movement.Line.SkuCode;
                balance.Name = movement.Line.Name;
                balances[balanceId] = slot;

                session.Add(new StockLedgerEntry
                {
                    Id = movement.EntryId,
                    WarehouseId = movement.WarehouseId,
                    SkuId = movement.Line.SkuId,
                    Quantity = Math.Abs(movement.SignedQuantity),
                    Uom = movement.Line.BaseUom,
                    Direction = movement.SignedQuantity >= 0 ? StockDirection.In : StockDirection.Out,
                    Reason = movement.Reason,
                    RefType = DocumentRefType,
                    RefId = document.Id,
                    ReferenceNo = document.ReferenceNo,
                    ReversesEntryId = movement.ReversesEntryId,
                    BalanceAfter = balance.OnHand,
                });
            }

            if (shortages.Count > 0)
            {
                throw new BusinessRuleException($"Not enough stock — {string.Join("; ", shortages)}.");
            }

            foreach (var (balance, isNew) in balances.Values)
            {
                if (isNew)
                {
                    session.Add(balance);
                }
                else
                {
                    session.Update(balance);
                }
            }

            foreach (StockDocument other in posting.Updates)
            {
                session.Update(other);
            }

            return document;
        }, cancellationToken);
    }

    /// <summary>
    /// Formats a quantity for a message.
    /// </summary>
    /// <param name="value">Amount.</param>
    /// <returns>Invariant text without trailing zeros.</returns>
    private static string Show(decimal value) => Quantity.Normalise(value).ToString(CultureInfo.InvariantCulture);
}
