using Platform.Shared.Common;
using Platform.Shared.Entities.Common;

namespace Platform.Shared.Entities.Sales;

/// <summary>
/// One line of a cart: a SKU and how much of it, in the unit the customer chose (P7).
/// Prices are never stored on a cart; they are worked out afresh every time it is shown.
/// </summary>
public class CartLine
{
    /// <summary>SKU wanted.</summary>
    public Guid SkuId { get; set; }

    /// <summary>Amount wanted.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Unit of <see cref="Quantity"/>.</summary>
    public string Uom { get; set; } = string.Empty;
}

/// <summary>
/// A customer's cart: one per customer. It holds no stock (P4) — stock is held
/// only when the order is placed — and no prices, so it can never show a stale one.
/// </summary>
public class Cart : BaseEntity, IOrgScoped, INotAudited
{
    /// <inheritdoc />
    public Guid OrgId { get; set; }

    /// <summary>Owner of the cart.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>PIN code the customer wants delivery to; decides availability.</summary>
    public string? Pincode { get; set; }

    /// <summary>Lines, one per SKU.</summary>
    public List<CartLine> Lines { get; set; } = new();

    /// <summary>
    /// Derives the id of a customer's cart.
    /// </summary>
    /// <param name="customerId">Customer.</param>
    /// <returns>The stable id.</returns>
    public static Guid IdFor(Guid customerId) => IdGenerator.FromName($"cart:{customerId:N}");
}
