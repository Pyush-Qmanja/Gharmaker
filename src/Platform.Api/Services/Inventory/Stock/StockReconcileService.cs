using Platform.Api.Common.Exceptions;
using Platform.Api.Repositories;
using Platform.Api.Security.Authorization;
using Platform.Shared.Common;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Inventory;
using Platform.Shared.Entities.Inventory;

namespace Platform.Api.Services.Inventory.Stock;

/// <summary>
/// Proves the Phase 2 gate — "the ledger always agrees": adds up the whole
/// ledger per SKU per warehouse and compares it with every balance. Rebuild
/// rewrites any balance that disagrees from the ledger, which is always right (P2).
/// Both need stock-adjust access in every warehouse.
/// </summary>
public interface IStockReconcileService
{
    /// <summary>
    /// Compares every balance with its ledger. Changes nothing.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>What was checked and any mismatches.</returns>
    Task<StockReconcileResultDto> CheckAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rewrites every balance that disagrees with its ledger, then checks again.
    /// </summary>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The check after rebuilding, with how many balances were rewritten.</returns>
    Task<StockReconcileResultDto> RebuildAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IStockReconcileService"/>.
/// </summary>
/// <remarks>
/// Reads the whole ledger in one pass; fine for the pilot's volumes. When the
/// ledger grows into millions of entries this becomes a scheduled job that
/// checks one warehouse at a time.
/// </remarks>
public sealed class StockReconcileService : IStockReconcileService
{
    private readonly IRepository<StockLedgerEntry> _ledger;
    private readonly IRepository<StockBalance> _balances;
    private readonly IRepository<Warehouse> _warehouses;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPermissionService _permissions;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="ledger">Ledger data access.</param>
    /// <param name="balances">Balance data access.</param>
    /// <param name="warehouses">Warehouse data access, for codes.</param>
    /// <param name="unitOfWork">Commits rebuilt balances.</param>
    /// <param name="permissions">Caller's access.</param>
    /// <param name="timeProvider">Clock.</param>
    public StockReconcileService(
        IRepository<StockLedgerEntry> ledger,
        IRepository<StockBalance> balances,
        IRepository<Warehouse> warehouses,
        IUnitOfWork unitOfWork,
        IPermissionService permissions,
        TimeProvider timeProvider)
    {
        _ledger = ledger;
        _balances = balances;
        _warehouses = warehouses;
        _unitOfWork = unitOfWork;
        _permissions = permissions;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<StockReconcileResultDto> CheckAsync(CancellationToken cancellationToken = default)
    {
        await RequireAccessAsync(cancellationToken);
        return (await CompareAsync(cancellationToken)).Result;
    }

    /// <inheritdoc />
    public async Task<StockReconcileResultDto> RebuildAsync(CancellationToken cancellationToken = default)
    {
        await RequireAccessAsync(cancellationToken);
        Comparison comparison = await CompareAsync(cancellationToken);

        foreach (Guid key in comparison.Wrong)
        {
            (decimal onHand, StockLedgerEntry last, DateTime lastAt) = comparison.Ledger[key];
            if (comparison.Balances.TryGetValue(key, out StockBalance? balance))
            {
                balance.OnHand = onHand;
                balance.IsInStock = onHand > 0;
                balance.LastMovedAt = lastAt;
                _balances.Update(balance);
            }
            else
            {
                _balances.Add(new StockBalance
                {
                    Id = key,
                    WarehouseId = last.WarehouseId,
                    SkuId = last.SkuId,
                    SkuCode = string.Empty,
                    Uom = last.Uom,
                    OnHand = onHand,
                    IsInStock = onHand > 0,
                    LastMovedAt = lastAt,
                });
            }
        }

        foreach (Guid orphan in comparison.Orphans)
        {
            StockBalance balance = comparison.Balances[orphan];
            balance.OnHand = 0;
            balance.IsInStock = false;
            _balances.Update(balance);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        StockReconcileResultDto after = (await CompareAsync(cancellationToken)).Result;
        after.BalancesRebuilt = comparison.Wrong.Count + comparison.Orphans.Count;
        return after;
    }

    /// <summary>
    /// Adds up the ledger and compares it with the balances.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The comparison.</returns>
    private async Task<Comparison> CompareAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<StockLedgerEntry> entries = await _ledger.ListAsync(_ledger.Query(), cancellationToken);
        Dictionary<Guid, StockBalance> balances = (await _balances.ListAsync(_balances.Query(), cancellationToken)).ToDictionary(b => b.Id);
        Dictionary<Guid, string> codes = (await _warehouses.ListAsync(_warehouses.Query(), cancellationToken)).ToDictionary(w => w.Id, w => w.Code);

        var ledger = new Dictionary<Guid, (decimal OnHand, StockLedgerEntry Last, DateTime LastAt)>();
        foreach (StockLedgerEntry entry in entries)
        {
            Guid key = StockBalance.IdFor(entry.WarehouseId, entry.SkuId);
            (decimal sum, StockLedgerEntry last, DateTime at) = ledger.TryGetValue(key, out var seen) ? seen : (0m, entry, DateTime.MinValue);
            ledger[key] = (sum + entry.SignedQuantity(), entry.CreatedAt >= at ? entry : last, entry.CreatedAt >= at ? entry.CreatedAt : at);
        }

        var result = new StockReconcileResultDto
        {
            EntriesChecked = entries.Count,
            BalancesChecked = balances.Count,
            CheckedAt = _timeProvider.GetUtcNow().UtcDateTime,
        };

        var wrong = new List<Guid>();
        foreach (var (key, (sum, last, _)) in ledger)
        {
            decimal onHand = Quantity.Normalise(sum);
            balances.TryGetValue(key, out StockBalance? balance);
            if (balance is null || balance.OnHand != onHand)
            {
                wrong.Add(key);
                result.Mismatches.Add(new StockMismatchDto
                {
                    WarehouseCode = codes.GetValueOrDefault(last.WarehouseId, "?"),
                    SkuCode = balance?.SkuCode ?? last.SkuId.ToString(),
                    BalanceOnHand = balance?.OnHand ?? 0,
                    LedgerOnHand = onHand,
                    Uom = last.Uom,
                });
            }
        }

        // A balance with stock but no ledger entry at all cannot be justified.
        List<Guid> orphans = balances.Values.Where(b => !ledger.ContainsKey(b.Id) && b.OnHand != 0).Select(b => b.Id).ToList();
        foreach (Guid orphan in orphans)
        {
            StockBalance balance = balances[orphan];
            result.Mismatches.Add(new StockMismatchDto
            {
                WarehouseCode = codes.GetValueOrDefault(balance.WarehouseId, "?"),
                SkuCode = balance.SkuCode,
                BalanceOnHand = balance.OnHand,
                LedgerOnHand = 0,
                Uom = balance.Uom,
            });
        }

        return new Comparison(result, ledger, balances, wrong, orphans);
    }

    /// <summary>
    /// Requires stock-adjust access everywhere: a partial check would miss warehouses.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when allowed.</returns>
    /// <exception cref="ForbiddenException">The caller lacks global stock-adjust access.</exception>
    private async Task RequireAccessAsync(CancellationToken cancellationToken)
    {
        if (!await _permissions.HasGlobalAsync(Capabilities.StockAdjust, cancellationToken))
        {
            throw new ForbiddenException("Checking the ledger needs stock management in every warehouse.");
        }
    }

    /// <summary>
    /// Everything a check found.
    /// </summary>
    /// <param name="Result">What is reported.</param>
    /// <param name="Ledger">Ledger totals by balance id.</param>
    /// <param name="Balances">Balances by id.</param>
    /// <param name="Wrong">Balances that disagree with (or are missing for) their ledger.</param>
    /// <param name="Orphans">Balances with stock but no ledger entries.</param>
    private sealed record Comparison(
        StockReconcileResultDto Result,
        Dictionary<Guid, (decimal OnHand, StockLedgerEntry Last, DateTime LastAt)> Ledger,
        Dictionary<Guid, StockBalance> Balances,
        List<Guid> Wrong,
        List<Guid> Orphans);
}
