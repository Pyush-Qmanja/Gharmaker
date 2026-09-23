using Platform.Shared.Entities.Common;

namespace Platform.Shared.Entities.Catalog;

/// <summary>
/// A node in the catalogue tree, e.g. Steel › TMT Bars. Top-level categories
/// have no parent.
/// </summary>
public class Category : BaseEntity, IOrgScoped, ISoftDeletable
{
    /// <inheritdoc />
    public Guid OrgId { get; set; }

    /// <summary>Parent category; null for a top-level category.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Display name, unique among its siblings.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>URL-safe identifier, unique per organisation (includes the parent path).</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Ascending sort position among siblings.</summary>
    public int DisplayOrder { get; set; }

    /// <inheritdoc />
    public bool IsActive { get; set; } = true;
}
