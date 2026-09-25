using Platform.Shared.Dtos.Common;

namespace Platform.Shared.Dtos.Inventory;

/// <summary>
/// A PIN code a warehouse delivers to (staff only — P1).
/// </summary>
public class DeliveryAreaDto : EntityDto
{
    /// <summary>Warehouse.</summary>
    public Guid WarehouseId { get; set; }

    /// <summary>Warehouse code.</summary>
    public string WarehouseCode { get; set; } = string.Empty;

    /// <summary>Warehouse name.</summary>
    public string WarehouseName { get; set; } = string.Empty;

    /// <summary>PIN code.</summary>
    public string Pincode { get; set; } = string.Empty;

    /// <summary>Days to deliver.</summary>
    public int LeadTimeDays { get; set; }

    /// <summary>False when removed.</summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// Query of <c>GET /api/delivery-areas</c>. <see cref="PagedRequest.Search"/> is a PIN code prefix.
/// </summary>
public class DeliveryAreaListRequest : PagedRequest
{
    /// <summary>Only this warehouse.</summary>
    public Guid? WarehouseId { get; set; }
}

/// <summary>
/// Body of <c>POST /api/delivery-areas</c>: adds (or updates) many PIN codes for one warehouse.
/// </summary>
public class AddDeliveryAreasRequest : INormalisable
{
    /// <summary>Warehouse that delivers.</summary>
    public Guid WarehouseId { get; set; }

    /// <summary>PIN codes. An entry may hold several separated by commas, spaces or new lines (a pasted list).</summary>
    public List<string> Pincodes { get; set; } = new();

    /// <summary>Days to deliver to each of them.</summary>
    public int LeadTimeDays { get; set; } = 1;

    /// <summary>
    /// Splits pasted lists into single PIN codes, trims them and removes duplicates.
    /// </summary>
    public void Normalise() =>
        Pincodes = Pincodes
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .SelectMany(p => p.Split(new[] { ',', ';', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            .Select(p => p.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
}

/// <summary>
/// Body of <c>PUT /api/delivery-areas/{id}</c>.
/// </summary>
public class UpdateDeliveryAreaRequest : IActivatableRequest
{
    /// <summary>Days to deliver.</summary>
    public int LeadTimeDays { get; set; }

    /// <inheritdoc />
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// What <c>POST /api/delivery-areas</c> did.
/// </summary>
public class AddDeliveryAreasResult
{
    /// <summary>PIN codes newly added.</summary>
    public int Added { get; set; }

    /// <summary>PIN codes already there, now updated (lead time, or restored).</summary>
    public int Updated { get; set; }
}
