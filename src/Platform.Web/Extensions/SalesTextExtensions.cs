using System.Globalization;
using Platform.Shared.Common;
using Platform.Shared.Dtos.Storefront;
using Platform.Shared.Entities.Inventory;
using Platform.Shared.Entities.Sales;

namespace Platform.Web.Extensions;

/// <summary>
/// How money, delivery dates and order states are written on screen, in one
/// place for the admin app and the store alike.
/// </summary>
public static class SalesTextExtensions
{
    /// <summary>Indian number grouping (1,23,456.00).</summary>
    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");

    /// <summary>
    /// Writes an amount in rupees with paise, e.g. ₹1,23,456.50.
    /// </summary>
    /// <param name="amount">Amount.</param>
    /// <returns>The text.</returns>
    public static string ToRupees(this decimal amount) => "₹" + amount.ToString("#,##,##0.00", India);

    /// <summary>
    /// Writes an optional amount in rupees, or a dash.
    /// </summary>
    /// <param name="amount">Amount, if any.</param>
    /// <returns>The text.</returns>
    public static string ToRupees(this decimal? amount) => amount is { } value ? value.ToRupees() : "—";

    /// <summary>
    /// Writes a unit price: paise always, and up to four decimals when the price has them.
    /// </summary>
    /// <param name="unitPrice">Price of one unit.</param>
    /// <returns>The text, e.g. ₹65.2475.</returns>
    public static string ToUnitPrice(this decimal unitPrice) => "₹" + unitPrice.ToString("#,##,##0.00##", India);

    /// <summary>
    /// Writes a percentage without trailing zeros, e.g. 18% or 0.25%.
    /// </summary>
    /// <param name="percent">Percent.</param>
    /// <returns>The text.</returns>
    public static string ToPercent(this decimal percent) =>
        Quantity.Normalise(percent).ToString("0.###", CultureInfo.InvariantCulture) + "%";

    /// <summary>
    /// Writes a delivery date, e.g. "Fri 25 Sep".
    /// </summary>
    /// <param name="date">Date.</param>
    /// <returns>The text.</returns>
    public static string ToDayText(this DateOnly date) => date.ToString("ddd d MMM", CultureInfo.InvariantCulture);

    /// <summary>
    /// Writes a delivery window, e.g. "Fri 25 Sep" or "Fri 25 – Sun 27 Sep".
    /// </summary>
    /// <param name="earliest">Earliest date.</param>
    /// <param name="latest">Latest date.</param>
    /// <returns>The text.</returns>
    public static string ToWindowText(DateOnly earliest, DateOnly latest) =>
        earliest == latest ? earliest.ToDayText()
        : earliest.Month == latest.Month ? $"{earliest.ToString("ddd d", CultureInfo.InvariantCulture)} – {latest.ToDayText()}"
        : $"{earliest.ToDayText()} – {latest.ToDayText()}";

    /// <summary>
    /// Writes a delivery window from the store's DTO.
    /// </summary>
    /// <param name="delivery">Window.</param>
    /// <returns>The text.</returns>
    public static string ToWindowText(this ShopDeliveryDto delivery) => ToWindowText(delivery.EarliestOn, delivery.LatestOn);

    /// <summary>
    /// Words staff see for an order state.
    /// </summary>
    /// <param name="status">State.</param>
    /// <returns>The label.</returns>
    public static string ToLabel(this OrderStatus status) => status switch
    {
        OrderStatus.Placed => "Awaiting confirmation",
        OrderStatus.Confirmed => "Confirmed",
        OrderStatus.Cancelled => "Cancelled",
        _ => "Expired",
    };

    /// <summary>
    /// Status-badge modifier for an order state.
    /// </summary>
    /// <param name="status">State.</param>
    /// <returns>A <c>status-badge--*</c> modifier.</returns>
    public static string ToBadge(this OrderStatus status) => status switch
    {
        OrderStatus.Placed => "status-badge--update",
        OrderStatus.Confirmed => "status-badge--new",
        OrderStatus.Cancelled => "status-badge--error",
        _ => "status-badge--inactive",
    };

    /// <summary>
    /// Words a customer sees for their order state.
    /// </summary>
    /// <param name="status">State.</param>
    /// <returns>The label.</returns>
    public static string ToLabel(this ShopOrderStatus status) => status switch
    {
        ShopOrderStatus.Placed => "Order placed",
        ShopOrderStatus.Confirmed => "Confirmed",
        ShopOrderStatus.Cancelled => "Cancelled",
        _ => "Closed",
    };

    /// <summary>
    /// Status-badge modifier for a customer's order state.
    /// </summary>
    /// <param name="status">State.</param>
    /// <returns>A <c>status-badge--*</c> modifier.</returns>
    public static string ToBadge(this ShopOrderStatus status) => status switch
    {
        ShopOrderStatus.Placed => "status-badge--new",
        ShopOrderStatus.Confirmed => "status-badge--active",
        ShopOrderStatus.Cancelled => "status-badge--error",
        _ => "status-badge--inactive",
    };

    /// <summary>
    /// Words staff see for where a stock hold stands.
    /// </summary>
    /// <param name="status">Hold state.</param>
    /// <returns>The label.</returns>
    public static string ToLabel(this StockHoldStatus status) => status switch
    {
        StockHoldStatus.Active => "Holding",
        StockHoldStatus.Confirmed => "Held for dispatch",
        StockHoldStatus.Released => "Released",
        _ => "Expired",
    };

    /// <summary>
    /// Status-badge modifier for a stock hold.
    /// </summary>
    /// <param name="status">Hold state.</param>
    /// <returns>A <c>status-badge--*</c> modifier.</returns>
    public static string ToBadge(this StockHoldStatus status) => status switch
    {
        StockHoldStatus.Active => "status-badge--update",
        StockHoldStatus.Confirmed => "status-badge--new",
        _ => "status-badge--inactive",
    };

    /// <summary>
    /// Converts a time typed in IST (a datetime-local field) to UTC for the API.
    /// </summary>
    /// <param name="ist">Wall-clock time in India.</param>
    /// <returns>The UTC instant.</returns>
    public static DateTime FromIstToUtc(this DateTime ist) =>
        DateTime.SpecifyKind(DateTime.SpecifyKind(ist, DateTimeKind.Unspecified) - FinancialYear.IstOffset, DateTimeKind.Utc);
}
