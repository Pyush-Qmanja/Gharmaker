using Platform.Shared.Entities.Common;

namespace Platform.Shared.Entities.Inventory;

/// <summary>
/// How the business holds a warehouse.
/// </summary>
public enum WarehouseType
{
    /// <summary>Owned by the business.</summary>
    Owned = 0,

    /// <summary>Rented by the business and run by its staff.</summary>
    Leased = 1,

    /// <summary>Run by a logistics partner on the business's behalf.</summary>
    ThirdParty = 2,
}

/// <summary>
/// A stock-holding location. Warehouses are a scope type (P6): a user scoped
/// to a warehouse sees only that warehouse and what is in it. Nothing about a
/// warehouse is ever returned to a customer (P1).
/// </summary>
public class Warehouse : BaseEntity, IOrgScoped, ISoftDeletable
{
    /// <inheritdoc />
    public Guid OrgId { get; set; }

    /// <summary>Short unique code, e.g. <c>WH-PUNE-01</c>.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>How the warehouse is held.</summary>
    public WarehouseType Type { get; set; }

    /// <summary>Postal address.</summary>
    public Address Address { get; set; } = new();

    /// <summary>Latitude (WGS 84), optional.</summary>
    public double? Lat { get; set; }

    /// <summary>Longitude (WGS 84), optional.</summary>
    public double? Lng { get; set; }

    /// <summary>User responsible for the warehouse, optional.</summary>
    public Guid? OwnerUserId { get; set; }

    /// <inheritdoc />
    public bool IsActive { get; set; } = true;
}
