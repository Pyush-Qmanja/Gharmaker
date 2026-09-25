using Platform.Shared.Dtos.Common;

namespace Platform.Shared.Dtos.Sales;

/// <summary>
/// A customer account as shown to staff.
/// </summary>
public class CustomerDto : EntityDto
{
    /// <summary>Full name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Sign-in email.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Mobile number.</summary>
    public string? Phone { get; set; }

    /// <summary>Business name.</summary>
    public string? CompanyName { get; set; }

    /// <summary>Buyer's GSTIN.</summary>
    public string? Gstin { get; set; }

    /// <summary>Tier price list; null means retail.</summary>
    public Guid? TierPriceListId { get; set; }

    /// <summary>That list's name.</summary>
    public string? TierPriceListName { get; set; }

    /// <summary>Default delivery address.</summary>
    public AddressDto? Address { get; set; }

    /// <summary>False when blocked from signing in and ordering.</summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// Body of <c>PUT /api/customers/{id}</c>. Customers create their own accounts;
/// staff change the price tier, business details, or block the account.
/// </summary>
public class UpdateCustomerRequest : IActivatableRequest
{
    /// <summary>Full name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mobile number.</summary>
    public string? Phone { get; set; }

    /// <summary>Business name.</summary>
    public string? CompanyName { get; set; }

    /// <summary>Buyer's GSTIN.</summary>
    public string? Gstin { get; set; }

    /// <summary>Tier price list; null for retail.</summary>
    public Guid? TierPriceListId { get; set; }

    /// <inheritdoc />
    public bool IsActive { get; set; } = true;
}
