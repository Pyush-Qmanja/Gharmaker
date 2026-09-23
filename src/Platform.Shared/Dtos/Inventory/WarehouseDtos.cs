using Platform.Shared.Dtos.Common;
using Platform.Shared.Entities.Inventory;

namespace Platform.Shared.Dtos.Inventory;

/// <summary>
/// Read model of a warehouse. Internal only — never returned by a customer-facing endpoint (P1).
/// </summary>
public class WarehouseDto : EntityDto
{
    /// <summary>Short unique code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>How the warehouse is held.</summary>
    public WarehouseType Type { get; set; }

    /// <summary>Postal address.</summary>
    public AddressDto Address { get; set; } = new();

    /// <summary>Latitude, if set.</summary>
    public double? Lat { get; set; }

    /// <summary>Longitude, if set.</summary>
    public double? Lng { get; set; }

    /// <summary>Responsible user, if set.</summary>
    public Guid? OwnerUserId { get; set; }

    /// <summary>False once deactivated.</summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// Editable warehouse fields shared by create and update.
/// </summary>
public interface IWarehouseFields
{
    /// <summary>Short unique code.</summary>
    string Code { get; }

    /// <summary>Display name.</summary>
    string Name { get; }

    /// <summary>How the warehouse is held.</summary>
    WarehouseType Type { get; }

    /// <summary>Postal address.</summary>
    AddressDto Address { get; }

    /// <summary>Latitude, optional.</summary>
    double? Lat { get; }

    /// <summary>Longitude, optional.</summary>
    double? Lng { get; }

    /// <summary>Responsible user, optional.</summary>
    Guid? OwnerUserId { get; }
}

/// <summary>
/// Body of <c>POST /api/warehouses</c>. Only a user with global scope may create one.
/// </summary>
public class CreateWarehouseRequest : IWarehouseFields
{
    /// <inheritdoc />
    public string Code { get; set; } = string.Empty;

    /// <inheritdoc />
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    public WarehouseType Type { get; set; }

    /// <inheritdoc />
    public AddressDto Address { get; set; } = new();

    /// <inheritdoc />
    public double? Lat { get; set; }

    /// <inheritdoc />
    public double? Lng { get; set; }

    /// <inheritdoc />
    public Guid? OwnerUserId { get; set; }
}

/// <summary>
/// Body of <c>PUT /api/warehouses/{id}</c>.
/// </summary>
public class UpdateWarehouseRequest : IWarehouseFields, IActivatableRequest
{
    /// <inheritdoc />
    public string Code { get; set; } = string.Empty;

    /// <inheritdoc />
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    public WarehouseType Type { get; set; }

    /// <inheritdoc />
    public AddressDto Address { get; set; } = new();

    /// <inheritdoc />
    public double? Lat { get; set; }

    /// <inheritdoc />
    public double? Lng { get; set; }

    /// <inheritdoc />
    public Guid? OwnerUserId { get; set; }

    /// <summary>False to deactivate, true to restore.</summary>
    public bool IsActive { get; set; } = true;
}
