using Platform.Shared.Entities.Common;

namespace Platform.Shared.Entities.Sales;

/// <summary>
/// A customer of the online store. Separate from <c>User</c> (staff): a
/// customer signs in to the storefront only, holds no capabilities, and can
/// never open a staff screen. Firebase Authentication keeps the password.
/// </summary>
public class Customer : BaseEntity, IOrgScoped, ISoftDeletable
{
    /// <inheritdoc />
    public Guid OrgId { get; set; }

    /// <summary>Person's full name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Sign-in email, lower case, unique per organisation.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Mobile number in E.164 format.</summary>
    public string? Phone { get; set; }

    /// <summary>Firebase Authentication uid; links the sign-in to this customer.</summary>
    public string AuthUid { get; set; } = string.Empty;

    /// <summary>Business name, for a contractor or builder buying for a firm.</summary>
    public string? CompanyName { get; set; }

    /// <summary>The buyer's GSTIN, when they want a B2B tax invoice.</summary>
    public string? Gstin { get; set; }

    /// <summary>Tier price list set by staff (e.g. Contractors); null means retail prices.</summary>
    public Guid? TierPriceListId { get; set; }

    /// <summary>Delivery address offered first at checkout.</summary>
    public Address? Address { get; set; }

    /// <inheritdoc />
    public bool IsActive { get; set; } = true;
}
