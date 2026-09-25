using Platform.Shared.Common;
using Platform.Shared.Entities.Common;

namespace Platform.Shared.Entities.Inventory;

/// <summary>
/// A PIN code one warehouse delivers to, and how many days delivery takes
/// (the blueprint's <c>serviceability</c>). Availability for a customer is the
/// stock of every warehouse serving their PIN code (P1: the customer sees only
/// the total and a date range). One row per warehouse and PIN code.
/// </summary>
public class DeliveryArea : BaseEntity, IOrgScoped, ISoftDeletable
{
    /// <inheritdoc />
    public Guid OrgId { get; set; }

    /// <summary>Warehouse that delivers.</summary>
    public Guid WarehouseId { get; set; }

    /// <summary>Six-digit PIN code delivered to.</summary>
    public string Pincode { get; set; } = string.Empty;

    /// <summary>Days from order to delivery from this warehouse (0 = same day).</summary>
    public int LeadTimeDays { get; set; }

    /// <inheritdoc />
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Derives the id of the row for a warehouse and PIN code, so adding the same
    /// PIN code twice updates one row instead of creating a duplicate.
    /// </summary>
    /// <param name="warehouseId">Warehouse.</param>
    /// <param name="pincode">PIN code.</param>
    /// <returns>The stable id.</returns>
    public static Guid IdFor(Guid warehouseId, string pincode) =>
        IdGenerator.FromName($"delivery_area:{warehouseId:N}:{pincode}");
}
