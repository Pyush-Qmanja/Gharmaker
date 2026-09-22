using Platform.Shared.Entities.Common;

namespace Platform.Shared.Entities.Catalog;

/// <summary>
/// A brand of material (e.g. a cement or steel maker). Brand is a first-class
/// entity so brand-wise display and brand-wise stocking both fall out of it.
/// </summary>
public class Brand : BaseEntity, IOrgScoped, ISoftDeletable
{
    /// <inheritdoc />
    public Guid OrgId { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>URL-safe identifier, unique per organisation.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Absolute URL of the brand logo, optional.</summary>
    public string? LogoUrl { get; set; }

    /// <summary>Legal manufacturer name when it differs from the brand name.</summary>
    public string? ManufacturerName { get; set; }

    /// <summary>Ascending sort position in brand listings.</summary>
    public int DisplayOrder { get; set; }

    /// <inheritdoc />
    public bool IsActive { get; set; } = true;
}
