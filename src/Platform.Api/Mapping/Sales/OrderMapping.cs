using Platform.Shared.Dtos.Sales;
using Platform.Shared.Entities.Inventory;
using Platform.Shared.Entities.Sales;

namespace Platform.Api.Mapping.Sales;

/// <summary>
/// Maps orders for staff screens. Staff may see where stock is held; the
/// storefront has its own mapping (<c>ShopMapping</c>) that never does (P1).
/// </summary>
public static class OrderMapping
{
    /// <summary>
    /// Maps an order for a list.
    /// </summary>
    /// <param name="order">Order.</param>
    /// <returns>The row.</returns>
    public static OrderSummaryDto ToSummaryDto(Order order) => Fill(new OrderSummaryDto(), order);

    /// <summary>
    /// Maps an order in full, with its holds.
    /// </summary>
    /// <param name="order">Order.</param>
    /// <param name="holds">Its holds.</param>
    /// <param name="warehouseCodes">Warehouse code by id.</param>
    /// <returns>The DTO.</returns>
    public static OrderDto ToDto(Order order, IEnumerable<StockHold> holds, IReadOnlyDictionary<Guid, string> warehouseCodes)
    {
        OrderDto dto = Fill(new OrderDto(), order);
        dto.CustomerGstin = order.CustomerGstin;
        dto.Phone = order.Phone;
        dto.Address = DtoMapping.ToAddressDto(order.Address);
        dto.SellerState = order.SellerState;
        dto.IsInterState = order.IsInterState;
        dto.Totals = CopyAmounts(order.Totals, new TaxAmountsDto());
        dto.ConfirmedAt = order.ConfirmedAt;
        dto.ClosedAt = order.ClosedAt;
        dto.Remarks = order.Remarks;
        dto.Lines = order.Lines.Select(line => CopyAmounts(line, new OrderLineDto
        {
            SkuId = line.SkuId,
            SkuCode = line.SkuCode,
            Name = line.Name,
            HsnCode = line.HsnCode,
            Quantity = line.Quantity,
            Uom = line.Uom,
            BaseQuantity = line.BaseQuantity,
            BaseUom = line.BaseUom,
            UnitPrice = line.UnitPrice,
            TaxRatePercent = line.TaxRatePercent,
            CessPercent = line.CessPercent,
        })).ToList();
        dto.Holds = holds
            .OrderBy(h => h.SkuCode, StringComparer.Ordinal)
            .Select(h => new StockHoldDto
            {
                WarehouseId = h.WarehouseId,
                WarehouseCode = warehouseCodes.GetValueOrDefault(h.WarehouseId, string.Empty),
                SkuCode = h.SkuCode,
                Quantity = h.Quantity,
                Uom = h.Uom,
                Status = h.Status,
                ExpiresAt = h.ExpiresAt,
            })
            .ToList();
        return dto;
    }

    /// <summary>
    /// Copies the tax columns.
    /// </summary>
    /// <typeparam name="T">Target type.</typeparam>
    /// <param name="source">Amounts.</param>
    /// <param name="target">Object to fill.</param>
    /// <returns>The target.</returns>
    private static T CopyAmounts<T>(TaxAmounts source, T target) where T : TaxAmountsDto
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
    private static T Fill<T>(T dto, Order order) where T : OrderSummaryDto
    {
        dto.ReferenceNo = order.ReferenceNo;
        dto.CustomerId = order.CustomerId;
        dto.CustomerName = order.CustomerName;
        dto.Status = order.Status;
        dto.Pincode = order.Address.Pincode;
        dto.City = order.Address.City;
        dto.LineCount = order.Lines.Count;
        dto.TotalAmount = order.Totals.TotalAmount;
        dto.Currency = order.Currency;
        dto.EarliestDeliveryOn = order.EarliestDeliveryOn;
        dto.LatestDeliveryOn = order.LatestDeliveryOn;
        dto.HoldExpiresAt = order.HoldExpiresAt;
        return dto.WithAuditFrom(order);
    }
}
