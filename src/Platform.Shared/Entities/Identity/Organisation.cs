using Platform.Shared.Entities.Common;

namespace Platform.Shared.Entities.Identity;

/// <summary>
/// A tenant of the platform. Every business row carries its <c>OrgId</c>.
/// </summary>
public class Organisation : BaseEntity, ISoftDeletable
{
    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Registered legal name printed on tax documents; null until business settings are filled in.</summary>
    public string? LegalName { get; set; }

    /// <summary>The business's GSTIN.</summary>
    public string? Gstin { get; set; }

    /// <summary>
    /// The one registered address the business invoices from (decision 1). Its state
    /// decides CGST + SGST or IGST; warehouses are internal stock points and never appear on a tax document.
    /// </summary>
    public Address? Address { get; set; }

    /// <inheritdoc />
    public bool IsActive { get; set; } = true;
}
