namespace Platform.Shared.Dtos.Common;

/// <summary>
/// Postal address in requests and responses. Same fields as the embedded
/// <c>Address</c> value object.
/// </summary>
public class AddressDto
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
