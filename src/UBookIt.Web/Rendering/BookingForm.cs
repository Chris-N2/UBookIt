namespace UBookIt.Web.Rendering;

/// <summary>
/// The parts of assembling a booking form that are about <em>a booking form</em>
/// rather than about a resource: the site zone, today in it, the date bounds, the
/// grid of permitted lengths, and the projection of instants into selectable
/// options.
/// <para>
/// Extracted so the service flow shares one implementation with the resource flow
/// rather than growing a second one beside it. Everything here is pure and
/// host-free — that property is why the original builder was testable without an
/// Umbraco host, and it is the property most easily lost in an extraction, so it
/// is stated rather than assumed: nothing in this file may take an
/// <c>HttpContext</c>, a <c>ViewComponent</c>, or a store.
/// </para>
/// </summary>
public static class BookingForm
{
    /// <summary>
    /// One selectable start: the exact UTC instant as its value (round-trip "O"
    /// format, so a POST re-selects the precise slot without re-parsing display
    /// text) and the site-zone wall clock as its label.
    /// </summary>
    public static BookingTimeOption ToOption(DateTimeOffset startUtc, TimeZoneInfo zone)
        => new(startUtc.ToString("O"), TimeZoneInfo.ConvertTime(startUtc, zone).ToString("HH:mm"));

    public static IReadOnlyList<BookingTimeOption> ToOptions(
        IEnumerable<DateTimeOffset> startsUtc, TimeZoneInfo zone)
        => startsUtc.Select(start => ToOption(start, zone)).ToList();

    /// <summary>Today in the site zone — the earliest selectable date and the render default.</summary>
    public static DateOnly TodayIn(DateTimeOffset nowUtc, TimeZoneInfo zone)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, zone).DateTime);

    /// <summary>Resolves the site zone; false (with UTC) for an unknown/invalid id.</summary>
    public static bool TryResolveZone(string timeZoneId, out TimeZoneInfo zone)
    {
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            zone = TimeZoneInfo.Utc;
            return false;
        }
    }

    /// <summary>
    /// A date that is never before today. A query parameter naming a past date
    /// should draw today's form rather than an error page.
    /// </summary>
    public static DateOnly NotBefore(DateOnly selected, DateOnly today)
        => selected < today ? today : selected;

    /// <summary>
    /// Every whole-minute length on a grid, from a minimum to a maximum
    /// inclusive, ascending.
    /// <para>
    /// Delegates to Core rather than computing it here. The rule moved when the
    /// structural-unfulfillability triad lifted (design D6): "does any length
    /// exist that every role can provide" is a domain question, and it had to
    /// become answerable without this project so the delivery API could publish
    /// the same answer instead of deriving a second one. This stays as the front
    /// end's spelling of it, so the resource flow's call sites are unchanged.
    /// </para>
    /// </summary>
    public static IReadOnlyList<int> LengthGrid(int minMinutes, int maxMinutes, int stepMinutes)
        => Core.Availability.LengthGrid.Minutes(minMinutes, maxMinutes, stepMinutes);
}
