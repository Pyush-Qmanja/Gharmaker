using Platform.Shared.Dtos.Common;

namespace Platform.Shared.Dtos.Identity;

/// <summary>
/// The business details used on tax documents and for GST (decision 1: one registered address).
/// </summary>
public class BusinessSettingsDto
{
    /// <summary>Organisation's display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Registered legal name.</summary>
    public string? LegalName { get; set; }

    /// <summary>GSTIN.</summary>
    public string? Gstin { get; set; }

    /// <summary>Registered address; its state decides CGST + SGST or IGST.</summary>
    public AddressDto? Address { get; set; }

    /// <summary>True when everything the store needs to charge GST is filled in.</summary>
    public bool IsComplete { get; set; }
}

/// <summary>
/// Body of <c>PUT /api/settings/business</c>.
/// </summary>
public class UpdateBusinessSettingsRequest
{
    /// <summary>Registered legal name.</summary>
    public string LegalName { get; set; } = string.Empty;

    /// <summary>GSTIN; its first two digits must be the state's GST code.</summary>
    public string Gstin { get; set; } = string.Empty;

    /// <summary>Registered address.</summary>
    public AddressDto Address { get; set; } = new();
}
