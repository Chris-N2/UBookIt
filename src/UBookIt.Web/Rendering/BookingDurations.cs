using System.Globalization;

namespace UBookIt.Web.Rendering;

/// <summary>
/// Human-readable booking lengths for the default front-end. Kept out of Core:
/// the domain deals in <c>TimeSpan</c>, and phrasing is a presentation concern
/// (design D9). "90" reads as "1 hour 30 minutes" in the select and in the
/// message that explains an unavailable length.
/// </summary>
public static class BookingDurations
{
    public static string Label(int minutes)
    {
        var hours = minutes / 60;
        var remainder = minutes % 60;

        return (hours, remainder) switch
        {
            (0, _) => Minutes(remainder),
            (_, 0) => Hours(hours),
            _ => $"{Hours(hours)} {Minutes(remainder)}",
        };
    }

    private static string Hours(int hours)
        => hours == 1 ? "1 hour" : $"{hours.ToString(CultureInfo.InvariantCulture)} hours";

    private static string Minutes(int minutes)
        => minutes == 1 ? "1 minute" : $"{minutes.ToString(CultureInfo.InvariantCulture)} minutes";
}
