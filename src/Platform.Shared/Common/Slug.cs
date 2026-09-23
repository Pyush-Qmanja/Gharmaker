using System.Text;

namespace Platform.Shared.Common;

/// <summary>
/// Builds URL-safe slugs (<c>tata-tiscon-550sd</c>) from names. The one place
/// slugs are made, so imports, forms and the storefront always agree.
/// </summary>
public static class Slug
{
    /// <summary>
    /// Lower-cases, keeps letters and digits, and turns every other run of
    /// characters into a single hyphen.
    /// </summary>
    /// <param name="text">Any text, e.g. "Tata Tiscon 550SD".</param>
    /// <returns>The slug, e.g. <c>tata-tiscon-550sd</c>; empty when nothing usable remains.</returns>
    public static string From(string text)
    {
        var builder = new StringBuilder(text.Length);
        bool pendingHyphen = false;
        foreach (char c in text.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                if (pendingHyphen && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(c);
                pendingHyphen = false;
            }
            else
            {
                pendingHyphen = true;
            }
        }

        return builder.ToString();
    }
}
