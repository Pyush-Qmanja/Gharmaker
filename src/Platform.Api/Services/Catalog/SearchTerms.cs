namespace Platform.Api.Services.Catalog;

/// <summary>
/// Builds the search words stored on a product. Firestore cannot search inside
/// text, so each product keeps every word of its name, brand and SKU codes plus
/// every prefix of those words; typing "tisc" then matches "Tata Tiscon" with
/// one indexed <c>array-contains</c> query.
/// </summary>
public static class SearchTerms
{
    /// <summary>Shortest prefix stored; one letter would match almost everything.</summary>
    public const int MinLength = 2;

    /// <summary>Longest prefix stored; longer words still match on their first 15 letters.</summary>
    public const int MaxLength = 15;

    /// <summary>
    /// Builds the stored terms from any number of texts.
    /// </summary>
    /// <param name="texts">Product name, brand name, SKU codes, variants...</param>
    /// <returns>Distinct lower-case words and prefixes.</returns>
    public static List<string> Build(IEnumerable<string?> texts)
    {
        var terms = new HashSet<string>(StringComparer.Ordinal);
        foreach (string word in texts.SelectMany(Words))
        {
            for (int length = MinLength; length <= Math.Min(word.Length, MaxLength); length++)
            {
                terms.Add(word[..length]);
            }
        }

        return terms.Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Splits search input into the words to look for, normalised like stored terms.
    /// </summary>
    /// <param name="search">What the user typed.</param>
    /// <returns>Words of at least <see cref="MinLength"/> characters, longest first, truncated to <see cref="MaxLength"/>.</returns>
    public static List<string> Query(string search) =>
        Words(search)
            .Where(w => w.Length >= MinLength)
            .Select(w => w.Length > MaxLength ? w[..MaxLength] : w)
            .Distinct()
            .OrderByDescending(w => w.Length)
            .ToList();

    /// <summary>
    /// Lower-cases text and splits it into runs of letters and digits.
    /// </summary>
    /// <param name="text">Any text.</param>
    /// <returns>The words.</returns>
    private static IEnumerable<string> Words(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var current = new System.Text.StringBuilder();
        foreach (char c in text.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                current.Append(c);
            }
            else if (current.Length > 0)
            {
                yield return current.ToString();
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }
}
