namespace Platform.Web.Common;

/// <summary>
/// Country calling codes offered next to phone numbers. India first (the
/// default); then the countries customers and site staff most often call from.
/// </summary>
public static class DialCodes
{
    /// <summary>Code selected when a number has none yet.</summary>
    public const string Default = "+91";

    /// <summary>Suffix of the form field that carries a phone number's calling code.</summary>
    public const string FieldSuffix = "__dial";

    /// <summary>Codes in the order shown.</summary>
    public static readonly IReadOnlyList<DialCode> All = new DialCode[]
    {
        new("+91", "India"),
        new("+971", "UAE"),
        new("+966", "Saudi Arabia"),
        new("+974", "Qatar"),
        new("+968", "Oman"),
        new("+965", "Kuwait"),
        new("+973", "Bahrain"),
        new("+977", "Nepal"),
        new("+880", "Bangladesh"),
        new("+94", "Sri Lanka"),
        new("+65", "Singapore"),
        new("+60", "Malaysia"),
        new("+44", "United Kingdom"),
        new("+1", "USA / Canada"),
        new("+61", "Australia"),
    };

    /// <summary>
    /// Splits a stored number (+919876543210) into its calling code and the rest.
    /// </summary>
    /// <param name="phone">Stored number, or null.</param>
    /// <returns>The code (longest known match, else India) and the local number.</returns>
    public static (string Code, string Local) Split(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return (Default, string.Empty);
        }

        string value = phone.Trim();
        DialCode? match = All.Where(d => value.StartsWith(d.Code, StringComparison.Ordinal)).MaxBy(d => d.Code.Length);
        return match is null ? (Default, value) : (match.Code, value[match.Code.Length..]);
    }

    /// <summary>
    /// Joins a calling code and what was typed into one international number.
    /// Spaces, dashes, brackets and leading zeros are dropped; a number typed
    /// with its own "+" is kept as typed.
    /// </summary>
    /// <param name="code">Calling code chosen, e.g. +91.</param>
    /// <param name="typed">Number typed.</param>
    /// <returns>The number, e.g. +919876543210, or empty when nothing was typed.</returns>
    public static string Join(string? code, string? typed)
    {
        string digits = new((typed ?? string.Empty).Where(c => char.IsAsciiDigit(c) || c == '+').ToArray());
        if (digits.Length == 0)
        {
            return string.Empty;
        }

        if (digits.StartsWith('+'))
        {
            return digits;
        }

        string chosen = All.Any(d => d.Code == code) ? code! : Default;
        return chosen + digits.TrimStart('0');
    }
}

/// <summary>
/// One country calling code.
/// </summary>
/// <param name="Code">Code with its plus, e.g. +91.</param>
/// <param name="Country">Country name.</param>
public sealed record DialCode(string Code, string Country);
