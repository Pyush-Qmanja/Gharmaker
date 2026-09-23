using System.Globalization;

namespace Platform.Web.Extensions;

/// <summary>
/// Display helpers for timestamps. Everything is stored in UTC and shown in
/// IST; this is the only place that conversion happens in the UI.
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>Format used for every date-time shown to a user.</summary>
    public const string DisplayFormat = "dd MMM yyyy, hh:mm tt";

    /// <summary>Format used for dates in table columns.</summary>
    public const string DateFormat = "dd MMM yyyy";

    /// <summary>India Standard Time; the IANA id works on Windows and Linux.</summary>
    private static readonly TimeZoneInfo Ist = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    /// <summary>
    /// Converts a UTC instant to IST and formats it for display.
    /// </summary>
    /// <param name="utc">UTC instant.</param>
    /// <returns>The formatted IST string.</returns>
    public static string ToIstString(this DateTime utc) =>
        utc.ToIst().ToString(DisplayFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats a UTC instant as an IST date only, for table columns
    /// (the full time goes in the cell's tooltip via <see cref="ToIstString(DateTime)"/>).
    /// </summary>
    /// <param name="utc">UTC instant.</param>
    /// <returns>e.g. "24 Sep 2026".</returns>
    public static string ToIstDateString(this DateTime utc) =>
        utc.ToIst().ToString(DateFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// Converts an optional UTC instant to IST for display.
    /// </summary>
    /// <param name="utc">UTC instant, or null.</param>
    /// <returns>The formatted IST string, or an empty string.</returns>
    public static string ToIstString(this DateTime? utc) => utc?.ToIstString() ?? string.Empty;

    /// <summary>
    /// Converts a UTC instant to IST.
    /// </summary>
    /// <param name="utc">UTC instant.</param>
    /// <returns>The same instant in IST.</returns>
    public static DateTime ToIst(this DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Ist);

    /// <summary>
    /// Greeting for the time of day in IST: morning before noon, afternoon before 5 pm, evening after.
    /// </summary>
    /// <param name="utc">UTC instant, usually now.</param>
    /// <returns>"Good morning", "Good afternoon" or "Good evening".</returns>
    public static string Greeting(this DateTime utc) => utc.ToIst().Hour switch
    {
        < 12 => "Good morning",
        < 17 => "Good afternoon",
        _ => "Good evening",
    };
}
