using Microsoft.AspNetCore.Mvc.Rendering;
using Platform.Shared.Dtos.Inventory;

namespace Platform.Web.Models;

/// <summary>
/// The stock list: filters, what the user may do, and one page of balances.
/// </summary>
public sealed class StockIndexViewModel
{
    /// <summary>Warehouses the user can see, for the filter.</summary>
    public IReadOnlyList<SelectListItem> WarehouseOptions { get; init; } = Array.Empty<SelectListItem>();

    /// <summary>Chosen warehouse.</summary>
    public Guid? WarehouseId { get; init; }

    /// <summary>Warehouses whose stock the user may see, to name the chosen one.</summary>
    public IReadOnlyList<StockWarehouseDto> Warehouses { get; init; } = Array.Empty<StockWarehouseDto>();

    /// <summary>True to hide rows with nothing on hand.</summary>
    public bool InStockOnly { get; init; }

    /// <summary>The page of balances.</summary>
    public IListViewModel Balances { get; init; } = default!;

    /// <summary>What the user may do somewhere.</summary>
    public StockAbilities Can { get; init; } = new();
}

/// <summary>
/// Which stock actions to offer, worked out from the user's warehouses.
/// </summary>
public sealed class StockAbilities
{
    /// <summary>May post receipts somewhere.</summary>
    public bool Receive { get; init; }

    /// <summary>May send transfers somewhere.</summary>
    public bool Transfer { get; init; }

    /// <summary>May adjust stock somewhere.</summary>
    public bool Adjust { get; init; }

    /// <summary>
    /// Works out the abilities from the user's stock warehouses.
    /// </summary>
    /// <param name="warehouses">Warehouses with the user's flags.</param>
    /// <returns>The abilities.</returns>
    public static StockAbilities From(IEnumerable<StockWarehouseDto> warehouses)
    {
        List<StockWarehouseDto> list = warehouses.ToList();
        return new StockAbilities
        {
            Receive = list.Any(w => w.CanReceive),
            Transfer = list.Any(w => w.CanTransfer),
            Adjust = list.Any(w => w.CanAdjust),
        };
    }
}

/// <summary>
/// One SKU's stock page: where it is and its movements.
/// </summary>
/// <param name="Stock">Balances by warehouse.</param>
/// <param name="Movements">A page of its movements.</param>
public sealed record SkuStockViewModel(SkuStockDto Stock, IListViewModel Movements);

/// <summary>
/// The movement history page.
/// </summary>
/// <param name="WarehouseOptions">Warehouses for the filter.</param>
/// <param name="WarehouseId">Chosen warehouse.</param>
/// <param name="Movements">The page.</param>
public sealed record MovementsViewModel(IReadOnlyList<SelectListItem> WarehouseOptions, Guid? WarehouseId, IListViewModel Movements);

/// <summary>
/// The create page of a stock document: the request being filled in, and the pickers it needs.
/// </summary>
public sealed class StockDocumentFormViewModel
{
    /// <summary>Page heading, e.g. "Receive goods".</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Line under the heading.</summary>
    public string Subtitle { get; init; } = string.Empty;

    /// <summary>The request (receipt, transfer or adjustment); model of the controller's <c>_Header</c> partial.</summary>
    public object Form { get; init; } = default!;

    /// <summary>Lines to show: those entered, then blank rows.</summary>
    public IReadOnlyList<StockLineRequest> Lines { get; init; } = Array.Empty<StockLineRequest>();

    /// <summary>Warehouses the user may post this document in.</summary>
    public IReadOnlyList<SelectListItem> WarehouseOptions { get; init; } = Array.Empty<SelectListItem>();

    /// <summary>Warehouses a transfer can go to.</summary>
    public IReadOnlyList<SelectListItem> DestinationOptions { get; init; } = Array.Empty<SelectListItem>();

    /// <summary>SKUs for the line picker.</summary>
    public IReadOnlyList<StockSkuOptionDto> SkuOptions { get; init; } = Array.Empty<StockSkuOptionDto>();

    /// <summary>
    /// The units a SKU can be entered in, its base unit first, for a line's
    /// unit drop-down. Empty for an unknown or blank SKU code.
    /// </summary>
    /// <param name="skuCode">SKU code as typed (any case).</param>
    /// <returns>Unit codes.</returns>
    public IReadOnlyList<string> UnitsOf(string? skuCode) =>
        SkuOptions.FirstOrDefault(s => string.Equals(s.Code, skuCode?.Trim(), StringComparison.OrdinalIgnoreCase)) is { } sku
            ? OrderedUnits(sku)
            : Array.Empty<string>();

    /// <summary>
    /// A SKU's units with its base unit first, then the rest as listed.
    /// </summary>
    /// <param name="sku">SKU option.</param>
    /// <returns>Unit codes.</returns>
    public static IReadOnlyList<string> OrderedUnits(StockSkuOptionDto sku) =>
        sku.Units.Where(u => u != sku.BaseUom).Prepend(sku.BaseUom).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Words on the submit button, e.g. "Post receipt".</summary>
    public string SubmitText { get; init; } = "Post";

    /// <summary>Help for the amount column (e.g. adjustments may be negative).</summary>
    public string QuantityHint { get; init; } = string.Empty;
}
