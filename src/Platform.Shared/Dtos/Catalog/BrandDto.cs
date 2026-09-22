using Platform.Shared.Dtos.Common;

namespace Platform.Shared.Dtos.Catalog;

/// <summary>
/// Read model of a brand, as returned by the API.
/// </summary>
public class BrandDto : EntityDto
{
    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>URL-safe identifier.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Logo URL, if any.</summary>
    public string? LogoUrl { get; set; }

    /// <summary>Manufacturer name, if different from the brand.</summary>
    public string? ManufacturerName { get; set; }

    /// <summary>Sort position.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>False once deactivated.</summary>
    public bool IsActive { get; set; }
}
