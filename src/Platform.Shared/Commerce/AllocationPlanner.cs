namespace Platform.Shared.Commerce;

/// <summary>
/// Free stock of one SKU in one warehouse that serves the customer's PIN code.
/// Internal only (P1): never leaves the API.
/// </summary>
/// <param name="WarehouseId">Warehouse.</param>
/// <param name="LeadTimeDays">Days to deliver from it to the PIN code.</param>
/// <param name="Available">On hand minus reserved, in the SKU's base unit.</param>
public sealed record WarehouseStock(Guid WarehouseId, int LeadTimeDays, decimal Available);

/// <summary>
/// What one order line needs, in the SKU's base unit.
/// </summary>
/// <param name="SkuId">SKU.</param>
/// <param name="Quantity">Amount wanted, in the SKU's base unit; greater than zero.</param>
public sealed record LineNeed(Guid SkuId, decimal Quantity);

/// <summary>
/// The one warehouse chosen to supply a whole order.
/// </summary>
/// <param name="WarehouseId">Warehouse.</param>
/// <param name="LeadTimeDays">Days to deliver from it to the PIN code.</param>
public sealed record OrderSource(Guid WarehouseId, int LeadTimeDays);

/// <summary>
/// Decides where an order ships from. Decision #2: an order is never split —
/// one warehouse supplies every line, so there is one shipment and one invoice.
/// Among serving warehouses that hold enough of every line, the fastest wins,
/// then the one left with most stock. Used both to promise a delivery date and
/// to place stock holds, so the promise and the hold always agree.
/// </summary>
public static class AllocationPlanner
{
    /// <summary>
    /// Chooses the one warehouse that can supply every line of an order.
    /// </summary>
    /// <param name="lines">What each line needs.</param>
    /// <param name="stock">Free stock of serving warehouses, by SKU.</param>
    /// <returns>The warehouse, or null when no single serving warehouse holds enough of everything.</returns>
    public static OrderSource? PlanOrder(IReadOnlyList<LineNeed> lines, IReadOnlyDictionary<Guid, List<WarehouseStock>> stock)
    {
        if (lines.Count == 0)
        {
            return null;
        }

        IEnumerable<WarehouseStock> candidates = stock.GetValueOrDefault(lines[0].SkuId) ?? new List<WarehouseStock>();
        return candidates
            .Select(c => (Source: new OrderSource(c.WarehouseId, c.LeadTimeDays), Spare: SpareAfter(c.WarehouseId, lines, stock)))
            .Where(c => c.Spare is not null)
            .OrderBy(c => c.Source.LeadTimeDays)
            .ThenByDescending(c => c.Spare)
            .ThenBy(c => c.Source.WarehouseId)
            .Select(c => c.Source)
            .FirstOrDefault();
    }

    /// <summary>
    /// The most of one SKU a single order can get: the largest free stock in any one serving warehouse.
    /// </summary>
    /// <param name="stock">Free stock of serving warehouses for the SKU.</param>
    /// <returns>The amount, in the SKU's base unit; 0 when none.</returns>
    public static decimal MostFromOnePlace(IEnumerable<WarehouseStock> stock) =>
        stock.Select(s => s.Available).DefaultIfEmpty(0m).Max();

    /// <summary>
    /// The quickest delivery for a SKU on its own: the fastest serving warehouse that has any.
    /// </summary>
    /// <param name="stock">Free stock of serving warehouses for the SKU.</param>
    /// <returns>Lead time in days, or null when none has stock.</returns>
    public static int? FastestLeadTime(IEnumerable<WarehouseStock> stock) =>
        stock.Where(s => s.Available > 0).Select(s => (int?)s.LeadTimeDays).Min();

    /// <summary>
    /// The delivery date from a warehouse's lead time. One warehouse means one
    /// date; the window's earliest and latest are the same.
    /// </summary>
    /// <param name="today">Today's date in IST.</param>
    /// <param name="leadTimeDays">Days to deliver.</param>
    /// <returns>Earliest and latest delivery dates.</returns>
    public static (DateOnly Earliest, DateOnly Latest) Window(DateOnly today, int leadTimeDays) =>
        (today.AddDays(leadTimeDays), today.AddDays(leadTimeDays));

    /// <summary>
    /// How much stock a warehouse would have left after supplying every line.
    /// </summary>
    /// <param name="warehouseId">Warehouse.</param>
    /// <param name="lines">What each line needs.</param>
    /// <param name="stock">Free stock of serving warehouses, by SKU.</param>
    /// <returns>Total left over across the lines, or null when any line is short there.</returns>
    private static decimal? SpareAfter(Guid warehouseId, IReadOnlyList<LineNeed> lines, IReadOnlyDictionary<Guid, List<WarehouseStock>> stock)
    {
        decimal spare = 0;
        foreach (IGrouping<Guid, LineNeed> sku in lines.GroupBy(l => l.SkuId))
        {
            decimal need = sku.Sum(l => l.Quantity);
            decimal available = stock.GetValueOrDefault(sku.Key)?.FirstOrDefault(s => s.WarehouseId == warehouseId)?.Available ?? 0m;
            if (available < need)
            {
                return null;
            }

            spare += available - need;
        }

        return spare;
    }
}
