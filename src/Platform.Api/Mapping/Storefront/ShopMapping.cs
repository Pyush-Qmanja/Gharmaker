using Platform.Api.Services.Storefront;
using Platform.Shared.Common;
using Platform.Shared.Dtos.Storefront;
using Platform.Shared.Entities.Common;
using Platform.Shared.Entities.Sales;

namespace Platform.Api.Mapping.Storefront;

/// <summary>
/// Builds every customer-facing DTO. Only this class maps into
/// <c>Platform.Shared.Dtos.Storefront</c>, and it copies only what a customer
/// may see: quantities, prices, tax and date ranges — never a warehouse,
/// allocation or hold (P1). Internal plans stay on <see cref="PricedLine"/>.
/// </summary>
public static class ShopMapping
{
    /// <summary>
    /// Maps a priced cart.
    /// </summary>
    /// <param name="cart">Priced cart.</param>
    /// <param name="pincode">PIN code it was priced for.</param>
    /// <returns>The customer's cart.</returns>
    public static ShopCartDto ToCartDto(PricedCart cart, string? pincode) => new()
    {
        Pincode = pincode,
        Currency = Money.Inr,
        Lines = cart.Lines.Select(ToCartLineDto).ToList(),
        Totals = ToTaxDto(cart.Totals, new ShopTaxDto()),
        IsInterState = cart.IsInterState,
        Delivery = cart.Window is { } window ? new ShopDeliveryDto { EarliestOn = window.Earliest, LatestOn = window.Latest } : null,
        CanCheckout = cart.CanCheckout,
        Problems = cart.Problems.ToList(),
    };

    /// <summary>
    /// Maps an order for the customer's list.
    /// </summary>
    /// <param name="order">Order.</param>
    /// <returns>The row.</returns>
    public static ShopOrderSummaryDto ToOrderSummaryDto(Order order) => FillSummary(new ShopOrderSummaryDto(), order);

    /// <summary>
    /// Maps an order in full for its customer.
    /// </summary>
    /// <param name="order">Order.</param>
    /// <returns>The order.</returns>
    public static ShopOrderDto ToOrderDto(Order order)
    {
        ShopOrderDto dto = FillSummary(new ShopOrderDto(), order);
        dto.Address = ToAddressDto(order.Address);
        dto.Phone = order.Phone;
        dto.Gstin = order.CustomerGstin;
        dto.IsInterState = order.IsInterState;
        dto.Totals = ToTaxDto(order.Totals, new ShopTaxDto());
        dto.CanCancel = order.Status == OrderStatus.Placed;
        dto.ClosedAt = order.ClosedAt;
        dto.Lines = order.Lines.Select(line => ToTaxDto(line, new ShopOrderLineDto
        {
            SkuCode = line.SkuCode,
            Name = line.Name,
            HsnCode = line.HsnCode,
            Quantity = line.Quantity,
            Uom = line.Uom,
            UnitPrice = line.UnitPrice,
            TaxRatePercent = line.TaxRatePercent,
        })).ToList();
        return dto;
    }

    /// <summary>
    /// Maps an address for the customer.
    /// </summary>
    /// <param name="address">Stored address.</param>
    /// <returns>The DTO.</returns>
    public static ShopAddressDto ToAddressDto(Address address) => new()
    {
        Line1 = address.Line1,
        Line2 = address.Line2,
        City = address.City,
        State = address.State,
        Pincode = address.Pincode,
    };

    /// <summary>
    /// Maps an address the customer entered, with the state spelt officially.
    /// </summary>
    /// <param name="address">Entered address.</param>
    /// <returns>The address to store.</returns>
    public static Address ToAddress(ShopAddressDto address) => new()
    {
        Line1 = address.Line1.Trim(),
        Line2 = string.IsNullOrWhiteSpace(address.Line2) ? null : address.Line2.Trim(),
        City = address.City.Trim(),
        State = Shared.Constants.IndianStates.Find(address.State)?.Name ?? address.State.Trim(),
        Pincode = address.Pincode.Trim(),
    };

    /// <summary>
    /// Maps a cart line.
    /// </summary>
    /// <param name="line">Priced line.</param>
    /// <returns>The DTO.</returns>
    private static ShopCartLineDto ToCartLineDto(PricedLine line)
    {
        var dto = new ShopCartLineDto
        {
            SkuId = line.Line.SkuId,
            ProductId = line.Product?.Id ?? Guid.Empty,
            SkuCode = line.Sku?.Code ?? string.Empty,
            Name = line.Name,
            Quantity = line.Line.Quantity,
            Uom = line.Line.Uom,
            Units = line.Units.ToList(),
            UnitPrice = line.UnitPrice,
            TaxRatePercent = line.Tax?.RatePercent,
            Problem = line.Problem,
        };
        return line.Amounts is null ? dto : ToTaxDto(line.Amounts, dto);
    }

    /// <summary>
    /// Copies the tax columns.
    /// </summary>
    /// <typeparam name="T">Target type.</typeparam>
    /// <param name="source">Amounts.</param>
    /// <param name="target">Object to fill.</param>
    /// <returns>The target.</returns>
    private static T ToTaxDto<T>(TaxAmounts source, T target) where T : ShopTaxDto
    {
        target.TaxableAmount = source.TaxableAmount;
        target.CgstAmount = source.CgstAmount;
        target.SgstAmount = source.SgstAmount;
        target.IgstAmount = source.IgstAmount;
        target.CessAmount = source.CessAmount;
        target.TotalAmount = source.TotalAmount;
        return target;
    }

    /// <summary>
    /// Fills the summary fields.
    /// </summary>
    /// <typeparam name="T">Summary or full DTO.</typeparam>
    /// <param name="dto">DTO to fill.</param>
    /// <param name="order">Order.</param>
    /// <returns>The DTO.</returns>
    private static T FillSummary<T>(T dto, Order order) where T : ShopOrderSummaryDto
    {
        dto.Id = order.Id;
        dto.ReferenceNo = order.ReferenceNo;
        dto.Status = order.Status switch
        {
            OrderStatus.Cancelled => ShopOrderStatus.Cancelled,
            OrderStatus.Expired => ShopOrderStatus.Expired,
            OrderStatus.Confirmed => ShopOrderStatus.Confirmed,
            _ => ShopOrderStatus.Placed,
        };
        dto.PlacedAt = order.CreatedAt;
        dto.LineCount = order.Lines.Count;
        dto.TotalAmount = order.Totals.TotalAmount;
        dto.Currency = order.Currency;
        dto.Delivery = new ShopDeliveryDto { EarliestOn = order.EarliestDeliveryOn, LatestOn = order.LatestDeliveryOn };
        return dto;
    }
}
