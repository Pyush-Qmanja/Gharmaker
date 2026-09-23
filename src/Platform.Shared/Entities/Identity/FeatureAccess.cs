namespace Platform.Shared.Entities.Identity;

/// <summary>
/// Access to one feature given straight to a user, on top of their roles,
/// stored inside the user document. Unlike a role, it carries its own
/// scopes — e.g. a store keeper can manage only their own warehouse while
/// their roles apply elsewhere.
/// </summary>
public class FeatureAccess
{
    /// <summary>Feature code from <c>Features</c>, e.g. <c>warehouses</c>.</summary>
    public string Feature { get; set; } = string.Empty;

    /// <summary>How much of the feature is allowed.</summary>
    public AccessLevel Level { get; set; }

    /// <summary>
    /// Where the access applies, for features limited by scope (see
    /// <c>FeatureInfo.ScopeType</c>). Empty for organisation-wide features.
    /// </summary>
    public List<ScopeGrant> Scopes { get; set; } = new();
}
