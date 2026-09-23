using Platform.Shared.Common;

namespace Platform.Shared.Entities.Common;

/// <summary>
/// The last number issued for one series of human references (e.g. goods
/// receipts in 2026), per organisation. Incremented inside the same
/// transaction as the document that takes the number, so numbers are never
/// skipped or reused.
/// </summary>
public class DocumentCounter : BaseEntity, IOrgScoped, INotAudited
{
    /// <inheritdoc />
    public Guid OrgId { get; set; }

    /// <summary>The series, e.g. <c>GRN-2026</c>.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Last number issued in the series.</summary>
    public int LastValue { get; set; }

    /// <summary>
    /// The id of an organisation's counter for one series.
    /// </summary>
    /// <param name="orgId">Organisation.</param>
    /// <param name="code">Series, e.g. <c>GRN-2026</c>.</param>
    /// <returns>The derived id.</returns>
    public static Guid IdFor(Guid orgId, string code) => IdGenerator.FromName($"document_counter:{orgId:N}:{code}");
}
