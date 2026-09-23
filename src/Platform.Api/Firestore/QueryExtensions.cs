using Google.Cloud.Firestore;

namespace Platform.Api.Firestore;

/// <summary>
/// Firestore query helpers shared by every service.
/// </summary>
public static class QueryExtensions
{
    /// <summary>
    /// Highest code point in the Basic Multilingual Plane's private-use area.
    /// Appended to a prefix it forms the upper bound of a "starts with" range.
    /// Written as a number so it is visible in source.
    /// </summary>
    private const char RangeEnd = (char)0xF8FF;

    /// <summary>
    /// Filters to documents whose field starts with <paramref name="prefix"/>
    /// and orders by that field — the only text search Firestore supports.
    /// Case-sensitive, so store the field normalised (lower- or upper-case)
    /// and normalise the prefix the same way.
    /// </summary>
    /// <param name="query">Query to filter.</param>
    /// <param name="field">Stored field name, from <c>FirestoreNaming.Field</c>.</param>
    /// <param name="prefix">Normalised prefix.</param>
    /// <returns>The filtered, ordered query.</returns>
    public static Query WhereStartsWith(this Query query, string field, string prefix) =>
        query
            .WhereGreaterThanOrEqualTo(field, prefix)
            .WhereLessThan(field, prefix + RangeEnd)
            .OrderBy(field);
}
