using Platform.Shared.Entities.Common;

namespace Platform.Shared.Entities.Catalog;

/// <summary>
/// A product of one brand in one category, e.g. "Tata Tiscon 550D TMT Bar".
/// What is actually stocked and sold are its <see cref="Sku"/>s (8 mm, 10 mm...).
/// </summary>
public class Product : BaseEntity, IOrgScoped, ISoftDeletable
{
    /// <inheritdoc />
    public Guid OrgId { get; set; }

    /// <summary>Brand that makes the product.</summary>
    public Guid BrandId { get; set; }

    /// <summary>Category the product sits in (a leaf or any node).</summary>
    public Guid CategoryId { get; set; }

    /// <summary>
    /// The category and all its ancestors, root first. Stored so "everything
    /// under Steel" is one indexed query instead of a tree walk.
    /// </summary>
    public List<Guid> CategoryPath { get; set; } = new();

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>URL-safe identifier, unique per organisation.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>GST HSN code (4, 6 or 8 digits) — drives the tax rate.</summary>
    public string HsnCode { get; set; } = string.Empty;

    /// <summary>
    /// Lower-case words and prefixes of the name, brand and SKU codes, maintained
    /// by the API so search can find any word ("tisc" → Tata Tiscon).
    /// </summary>
    public List<string> SearchTerms { get; set; } = new();

    /// <inheritdoc />
    public bool IsActive { get; set; } = true;
}
