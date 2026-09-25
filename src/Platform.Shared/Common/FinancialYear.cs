using System.Globalization;

namespace Platform.Shared.Common;

/// <summary>
/// Indian financial year (1 April to 31 March, in IST), used in human
/// references such as <c>ORD-2627-004512</c> for FY 2026-27.
/// </summary>
public static class FinancialYear
{
    /// <summary>India Standard Time: UTC+05:30, no daylight saving.</summary>
    public static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);

    /// <summary>
    /// Returns the four-digit code of the financial year containing an instant.
    /// </summary>
    /// <param name="utc">UTC instant.</param>
    /// <returns>For example <c>2627</c> for any day from 1 Apr 2026 to 31 Mar 2027 (IST).</returns>
    public static string Code(DateTime utc)
    {
        DateTime ist = ToIst(utc);
        int startYear = ist.Month >= 4 ? ist.Year : ist.Year - 1;
        return (startYear % 100).ToString("D2", CultureInfo.InvariantCulture)
            + ((startYear + 1) % 100).ToString("D2", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Returns the IST calendar date of an instant.
    /// </summary>
    /// <param name="utc">UTC instant.</param>
    /// <returns>The date in India.</returns>
    public static DateOnly IstDate(DateTime utc) => DateOnly.FromDateTime(ToIst(utc));

    /// <summary>
    /// Shifts a UTC instant to IST wall-clock time.
    /// </summary>
    /// <param name="utc">UTC instant.</param>
    /// <returns>The same instant as IST local time.</returns>
    private static DateTime ToIst(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).Add(IstOffset);
}
