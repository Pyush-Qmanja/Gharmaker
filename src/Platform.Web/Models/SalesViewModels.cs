using Microsoft.AspNetCore.Mvc.Rendering;
using Platform.Shared.Dtos.Catalog;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Identity;
using Platform.Shared.Dtos.Inventory;
using Platform.Shared.Dtos.Pricing;
using Platform.Shared.Dtos.Sales;
using Platform.Shared.Entities.Sales;

namespace Platform.Web.Models;

/// <summary>
/// The price grid of one list: products with each SKU's current and upcoming price.
/// </summary>
/// <param name="List">Price list shown.</param>
/// <param name="Products">One page of products.</param>
/// <param name="CanManage">True when the user may set prices.</param>
public sealed record PriceGridViewModel(PriceListDto List, ListViewModel<PriceGridProductDto> Products, bool CanManage);

/// <summary>
/// Form for a new price: unit, up to five quantity slabs, an optional start and a note.
/// </summary>
public sealed class SetPriceForm
{
    /// <summary>Rows offered for slabs.</summary>
    public const int SlabRows = 5;

    /// <summary>Unit the price is for.</summary>
    public string Uom { get; set; } = string.Empty;

    /// <summary>Slabs; the first starts at 0, blank rows are ignored.</summary>
    public List<PriceSlabDto> Slabs { get; set; } = new();

    /// <summary>Start in India time; empty for now.</summary>
    public DateTime? ValidFromIst { get; set; }

    /// <summary>Why the price is set.</summary>
    public string? Remarks { get; set; }

    /// <summary>
    /// Pads the slab rows to <see cref="SlabRows"/> and fixes the first at 0.
    /// </summary>
    /// <returns>This form.</returns>
    public SetPriceForm WithRows()
    {
        while (Slabs.Count < SlabRows)
        {
            Slabs.Add(new PriceSlabDto());
        }

        Slabs[0].MinQuantity = 0;
        return this;
    }
}

/// <summary>
/// The page for setting one SKU's price in a list.
/// </summary>
/// <param name="List">Price list.</param>
/// <param name="Product">Product of the SKU.</param>
/// <param name="Sku">SKU priced.</param>
/// <param name="Form">The form.</param>
/// <param name="History">Every earlier price, newest first.</param>
public sealed record SetPriceViewModel(PriceListDto List, ProductDetailDto Product, SkuDto Sku, SetPriceForm Form, IReadOnlyList<SkuPriceDto> History);

/// <summary>
/// GST rates page: rates in force (or one HSN code's history) and HSN codes still without a rate.
/// </summary>
/// <param name="Rates">One page of rates.</param>
/// <param name="Missing">HSN codes of active products without a rate.</param>
/// <param name="HsnCode">The HSN code whose history is shown, if any.</param>
/// <param name="CanManage">True when the user may add rates.</param>
public sealed record TaxRatesViewModel(ListViewModel<TaxRateDto> Rates, IReadOnlyList<MissingTaxRateDto> Missing, string? HsnCode, bool CanManage);

/// <summary>
/// Form for a new GST rate.
/// </summary>
public sealed class TaxRateForm
{
    /// <summary>HSN code.</summary>
    public string HsnCode { get; set; } = string.Empty;

    /// <summary>GST rate in percent.</summary>
    public decimal RatePercent { get; set; }

    /// <summary>Cess in percent.</summary>
    public decimal CessPercent { get; set; }

    /// <summary>Start in India time; empty for now.</summary>
    public DateTime? ValidFromIst { get; set; }

    /// <summary>Source of the rate.</summary>
    public string? Remarks { get; set; }
}

/// <summary>
/// Delivery areas page: warehouse filter, PIN code search and the list.
/// </summary>
/// <param name="Areas">One page of areas.</param>
/// <param name="WarehouseOptions">Warehouses for the filter.</param>
/// <param name="WarehouseId">Chosen warehouse.</param>
/// <param name="CanManage">True when the user may change areas.</param>
public sealed record DeliveryAreasViewModel(
    ListViewModel<DeliveryAreaDto> Areas, IReadOnlyList<SelectListItem> WarehouseOptions, Guid? WarehouseId, bool CanManage);

/// <summary>
/// Form for adding PIN codes to a warehouse.
/// </summary>
public sealed class AddDeliveryAreasForm
{
    /// <summary>Warehouse that delivers.</summary>
    public Guid WarehouseId { get; set; }

    /// <summary>PIN codes, separated by commas, spaces or new lines.</summary>
    public string Pincodes { get; set; } = string.Empty;

    /// <summary>Days to deliver.</summary>
    public int LeadTimeDays { get; set; } = 1;
}

/// <summary>
/// Page for one delivery area.
/// </summary>
/// <param name="Area">The area.</param>
/// <param name="Form">Lead time and status.</param>
public sealed record EditDeliveryAreaViewModel(DeliveryAreaDto Area, UpdateDeliveryAreaRequest Form);

/// <summary>
/// Page for one customer: details, tier and status.
/// </summary>
/// <param name="Customer">The customer.</param>
/// <param name="Form">Editable values.</param>
/// <param name="TierOptions">Tier price lists.</param>
/// <param name="Orders">Their latest orders.</param>
public sealed record CustomerEditViewModel(
    CustomerDto Customer, UpdateCustomerRequest Form, IReadOnlyList<SelectListItem> TierOptions, IReadOnlyList<OrderSummaryDto> Orders);

/// <summary>
/// Orders page: status tabs, reference search and the list.
/// </summary>
/// <param name="Orders">One page of orders.</param>
/// <param name="Status">Chosen status, or null for all.</param>
public sealed record OrdersViewModel(ListViewModel<OrderSummaryDto> Orders, OrderStatus? Status);

/// <summary>
/// One order for staff.
/// </summary>
/// <param name="Order">The order.</param>
/// <param name="CanManage">True when the user may cancel it.</param>
public sealed record OrderDetailsViewModel(OrderDto Order, bool CanManage);

/// <summary>
/// Business settings page.
/// </summary>
/// <param name="Form">Legal name, GSTIN, address.</param>
/// <param name="IsComplete">True when the store can charge GST.</param>
/// <param name="CanManage">True when the user may change them.</param>
public sealed record BusinessSettingsViewModel(UpdateBusinessSettingsRequest Form, bool IsComplete, bool CanManage);

/// <summary>
/// Drop-down options shared by sales screens.
/// </summary>
public static class SalesOptions
{
    /// <summary>
    /// India's states and union territories for an address drop-down.
    /// </summary>
    /// <param name="selected">State already chosen.</param>
    /// <returns>The options.</returns>
    public static IReadOnlyList<SelectListItem> States(string? selected) =>
        Shared.Constants.IndianStates.All
            .Select(s => new SelectListItem(s.Name, s.Name, string.Equals(s.Name, selected, StringComparison.OrdinalIgnoreCase)))
            .ToList();
}
