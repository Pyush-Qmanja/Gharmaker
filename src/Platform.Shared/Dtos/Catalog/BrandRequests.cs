using Platform.Shared.Dtos.Common;

namespace Platform.Shared.Dtos.Catalog;

/// <summary>
/// Editable brand fields shared by create and update, so both requests are
/// validated by one set of rules (see <c>BrandFieldsValidator</c>).
/// </summary>
public interface IBrandFields
{
    /// <summary>Display name.</summary>
    string Name { get; }

    /// <summary>URL-safe identifier.</summary>
    string Slug { get; }

    /// <summary>Logo URL, optional.</summary>
    string? LogoUrl { get; }

    /// <summary>Manufacturer name, optional.</summary>
    string? ManufacturerName { get; }

    /// <summary>Sort position.</summary>
    int DisplayOrder { get; }
}

/// <summary>
/// Body of <c>POST /api/brands</c>.
/// </summary>
public class CreateBrandRequest : IBrandFields
{
    /// <inheritdoc />
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    public string Slug { get; set; } = string.Empty;

    /// <inheritdoc />
    public string? LogoUrl { get; set; }

    /// <inheritdoc />
    public string? ManufacturerName { get; set; }

    /// <inheritdoc />
    public int DisplayOrder { get; set; }
}

/// <summary>
/// Body of <c>PUT /api/brands/{id}</c>. Adds <see cref="IsActive"/> so a
/// deactivated brand can be restored.
/// </summary>
public class UpdateBrandRequest : IBrandFields, IActivatableRequest
{
    /// <inheritdoc />
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    public string Slug { get; set; } = string.Empty;

    /// <inheritdoc />
    public string? LogoUrl { get; set; }

    /// <inheritdoc />
    public string? ManufacturerName { get; set; }

    /// <inheritdoc />
    public int DisplayOrder { get; set; }

    /// <summary>False to deactivate, true to restore.</summary>
    public bool IsActive { get; set; } = true;
}
