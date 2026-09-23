namespace Platform.Shared.Entities.Common;

/// <summary>
/// A postal address, embedded in the entity that owns it (warehouse, site,
/// customer address). One shape everywhere so address fields are always
/// named the same.
/// </summary>
public class Address
{
    /// <summary>Building, street.</summary>
    public string Line1 { get; set; } = string.Empty;

    /// <summary>Area, landmark; optional.</summary>
    public string? Line2 { get; set; }

    /// <summary>City or town.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>State or union territory.</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>Six-digit Indian PIN code.</summary>
    public string Pincode { get; set; } = string.Empty;
}
