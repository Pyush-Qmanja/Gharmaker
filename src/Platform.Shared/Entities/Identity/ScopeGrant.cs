namespace Platform.Shared.Entities.Identity;

/// <summary>
/// One area of access granted to a user, stored inside the user document.
/// </summary>
public class ScopeGrant
{
    /// <summary>Kind of object the grant covers.</summary>
    public ScopeType ScopeType { get; set; }

    /// <summary>The object granted; null only for <see cref="ScopeType.Global"/>.</summary>
    public Guid? ScopeId { get; set; }
}
