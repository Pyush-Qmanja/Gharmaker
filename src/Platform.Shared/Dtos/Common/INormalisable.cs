namespace Platform.Shared.Dtos.Common;

/// <summary>
/// A request that can tidy itself before validation — for example dropping
/// the "no access" rows an access grid posts, or adding the view capability a
/// manage capability implies. The UI calls it on every posted form and the
/// API mappers apply the same rules, so both sides agree on the result.
/// </summary>
public interface INormalisable
{
    /// <summary>
    /// Brings the request into its canonical form. Must be safe to call more than once.
    /// </summary>
    void Normalise();
}
