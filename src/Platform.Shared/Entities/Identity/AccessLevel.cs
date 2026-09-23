namespace Platform.Shared.Entities.Identity;

/// <summary>
/// How much of one feature a grant allows. Each level includes the ones
/// below it: <see cref="Manage"/> also allows <see cref="View"/>.
/// </summary>
public enum AccessLevel
{
    /// <summary>No access through this grant.</summary>
    None = 0,

    /// <summary>See the feature's records (its <c>view</c> capability).</summary>
    View = 1,

    /// <summary>See and change the feature's records (its <c>view</c> and <c>manage</c> capabilities).</summary>
    Manage = 2,
}
