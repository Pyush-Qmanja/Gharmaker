namespace Platform.Shared.Entities.Common;

/// <summary>
/// Marks a business entity that belongs to one organisation. The data layer
/// fills <see cref="OrgId"/> on insert and filters every query by it, so one
/// organisation can never read the rows of another.
/// </summary>
public interface IOrgScoped
{
    /// <summary>Owning organisation.</summary>
    Guid OrgId { get; set; }
}
