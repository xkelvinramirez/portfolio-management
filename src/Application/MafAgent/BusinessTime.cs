using System.Globalization;

namespace Application.MafAgent;

/// <summary>
/// The business time zone, Europe/Madrid (plan §0), the same one RefreshPortfolioPricesCommandHandler and
/// PortfolioStatusReportBackgroundService use. Dates are stored as UTC and shown to the user in local time.
/// </summary>
public static class BusinessTime
{
    public const string ZoneId = "Europe/Madrid";

    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(ZoneId);

    /// <summary>UTC as shown in values, e.g. 2026-11-10T18:30:00Z.</summary>
    public static string FormatUtc(DateTime utc) =>
        utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>Local Madrid time with its offset, e.g. 2026-11-10T19:30:00+01:00 (Europe/Madrid).</summary>
    public static string FormatLocal(DateTime utc)
    {
        var local = new DateTimeOffset(TimeZoneInfo.ConvertTimeFromUtc(utc, Zone), Zone.GetUtcOffset(utc));
        return $"{local.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture)} ({ZoneId})";
    }
}
