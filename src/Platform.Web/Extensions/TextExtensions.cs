using System.Text.RegularExpressions;

namespace Platform.Web.Extensions;

/// <summary>
/// Display helpers for code names. The one place PascalCase is turned into
/// words, used by labels, drop-downs and tables alike.
/// </summary>
public static partial class TextExtensions
{
    /// <summary>
    /// Splits a PascalCase name into words: <c>ThirdParty</c> becomes "Third Party",
    /// <c>LogoUrl</c> becomes "Logo Url".
    /// </summary>
    /// <param name="name">PascalCase name.</param>
    /// <returns>The words, separated by spaces.</returns>
    public static string ToWords(this string name) => WordBoundary().Replace(name, " $1");

    /// <summary>
    /// Display text for an enum value, e.g. <c>WarehouseType.ThirdParty</c> → "Third Party".
    /// </summary>
    /// <param name="value">Enum value.</param>
    /// <returns>The words.</returns>
    public static string ToWords(this Enum value) => value.ToString().ToWords();

    /// <summary>
    /// Counts something in words with the right plural: 1 SKU, 2 SKUs, 1 category, 3 categories.
    /// </summary>
    /// <param name="count">How many.</param>
    /// <param name="singular">Word for one, e.g. "SKU".</param>
    /// <param name="plural">Word for several; defaults to <paramref name="singular"/> + "s".</param>
    /// <returns>"count word".</returns>
    public static string Counted(this int count, string singular, string? plural = null) =>
        $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";

    /// <summary>Matches an upper-case letter that starts a new word.</summary>
    /// <returns>The compiled regex.</returns>
    [GeneratedRegex("(?<=[a-z0-9])([A-Z])")]
    private static partial Regex WordBoundary();
}
