namespace Platform.Shared.Entities.Identity;

/// <summary>
/// Kinds of object a user's access can be limited to (P6). A capability says
/// what a user may do; a scope says where.
/// </summary>
public enum ScopeType
{
    /// <summary>Everything in the organisation. Carries no scope id.</summary>
    Global = 0,

    /// <summary>One warehouse.</summary>
    Warehouse = 1,

    /// <summary>A group of warehouses.</summary>
    WarehouseGroup = 2,

    /// <summary>One construction site.</summary>
    Site = 3,

    /// <summary>One project (and its sites).</summary>
    Project = 4,

    /// <summary>One customer account.</summary>
    Customer = 5,
}
