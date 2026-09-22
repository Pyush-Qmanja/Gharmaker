namespace Platform.Web.Extensions;

/// <summary>
/// Display helpers for timestamps. Everything is stored in UTC and shown in
/// IST; this is the only place that conversion happens in the UI.
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>Format used for every date-time shown to a user.</summary>
    public const string DisplayFormat = "dd MMM yyyy, hh:mm tt";

    /// <summary>India Standard Time; the IANA id works on Windows and Linux.</summary>
    private static readonly TimeZoneInfo Ist = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    /// <summary>
    /// Converts a UTC instant to IST and formats it for display.
    /// </summary>
    /// <param name="utc">UTC instant.</param>
    /// <returns>The formatted IST string.</returns>
    public static string ToIstString(this DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Ist).ToString(DisplayFormat);

    /// <summary>
    /// Converts an optional UTC instant to IST for display.
    /// </summary>
    /// <param name="utc">UTC instant, or null.</param>
    /// <returns>The formatted IST string, or an empty string.</returns>
    public static string ToIstString(this DateTime? utc) => utc?.ToIstString() ?? string.Empty;
}
