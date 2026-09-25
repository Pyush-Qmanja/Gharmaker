namespace Platform.Shared.Constants;

/// <summary>
/// States and union territories of India with their GST state codes. Addresses
/// store the state by name; GST compares the seller's state with the place of
/// supply through <see cref="AreSame"/>, so spelling and case never decide the tax.
/// </summary>
public static class IndianStates
{
    /// <summary>Every state and union territory, in alphabetical order.</summary>
    public static readonly IReadOnlyList<IndianState> All = new IndianState[]
    {
        new("35", "Andaman and Nicobar Islands", IsUnionTerritory: true),
        new("37", "Andhra Pradesh", IsUnionTerritory: false),
        new("12", "Arunachal Pradesh", IsUnionTerritory: false),
        new("18", "Assam", IsUnionTerritory: false),
        new("10", "Bihar", IsUnionTerritory: false),
        new("04", "Chandigarh", IsUnionTerritory: true),
        new("22", "Chhattisgarh", IsUnionTerritory: false),
        new("26", "Dadra and Nagar Haveli and Daman and Diu", IsUnionTerritory: true),
        new("07", "Delhi", IsUnionTerritory: false),
        new("30", "Goa", IsUnionTerritory: false),
        new("24", "Gujarat", IsUnionTerritory: false),
        new("06", "Haryana", IsUnionTerritory: false),
        new("02", "Himachal Pradesh", IsUnionTerritory: false),
        new("01", "Jammu and Kashmir", IsUnionTerritory: false),
        new("20", "Jharkhand", IsUnionTerritory: false),
        new("29", "Karnataka", IsUnionTerritory: false),
        new("32", "Kerala", IsUnionTerritory: false),
        new("38", "Ladakh", IsUnionTerritory: true),
        new("31", "Lakshadweep", IsUnionTerritory: true),
        new("23", "Madhya Pradesh", IsUnionTerritory: false),
        new("27", "Maharashtra", IsUnionTerritory: false),
        new("14", "Manipur", IsUnionTerritory: false),
        new("17", "Meghalaya", IsUnionTerritory: false),
        new("15", "Mizoram", IsUnionTerritory: false),
        new("13", "Nagaland", IsUnionTerritory: false),
        new("21", "Odisha", IsUnionTerritory: false),
        new("34", "Puducherry", IsUnionTerritory: false),
        new("03", "Punjab", IsUnionTerritory: false),
        new("08", "Rajasthan", IsUnionTerritory: false),
        new("11", "Sikkim", IsUnionTerritory: false),
        new("33", "Tamil Nadu", IsUnionTerritory: false),
        new("36", "Telangana", IsUnionTerritory: false),
        new("16", "Tripura", IsUnionTerritory: false),
        new("09", "Uttar Pradesh", IsUnionTerritory: false),
        new("05", "Uttarakhand", IsUnionTerritory: false),
        new("19", "West Bengal", IsUnionTerritory: false),
    };

    private static readonly Dictionary<string, IndianState> ByName =
        All.ToDictionary(s => Key(s.Name), StringComparer.Ordinal);

    /// <summary>
    /// Finds a state by name, ignoring case, extra spaces and "&amp;" for "and".
    /// </summary>
    /// <param name="name">State name as typed on an address.</param>
    /// <returns>The state, or null when the name is not recognised.</returns>
    public static IndianState? Find(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : ByName.GetValueOrDefault(Key(name));

    /// <summary>
    /// Checks whether a name is a recognised state or union territory.
    /// </summary>
    /// <param name="name">State name.</param>
    /// <returns>True when <see cref="Find"/> recognises it.</returns>
    public static bool IsKnown(string? name) => Find(name) is not null;

    /// <summary>
    /// Decides whether two state names mean the same state (same GST code).
    /// </summary>
    /// <param name="first">One state name.</param>
    /// <param name="second">The other state name.</param>
    /// <returns>True when both are recognised and share a GST state code.</returns>
    public static bool AreSame(string? first, string? second) =>
        Find(first) is { } a && Find(second) is { } b && a.GstCode == b.GstCode;

    /// <summary>
    /// Builds the comparison key for a state name.
    /// </summary>
    /// <param name="name">State name.</param>
    /// <returns>Lower-case name with single spaces and "and" for "&amp;".</returns>
    private static string Key(string name) =>
        string.Join(' ', name.Replace("&", " and ", StringComparison.Ordinal)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
}

/// <summary>
/// One state or union territory.
/// </summary>
/// <param name="GstCode">Two-digit GST state code (the first two digits of a GSTIN).</param>
/// <param name="Name">Official name.</param>
/// <param name="IsUnionTerritory">True for a union territory without a legislature (UTGST instead of SGST).</param>
public sealed record IndianState(string GstCode, string Name, bool IsUnionTerritory);
