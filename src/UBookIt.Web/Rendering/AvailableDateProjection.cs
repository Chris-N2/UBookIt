namespace UBookIt.Web.Rendering;

/// <summary>
/// Turns a window's worth of bookable starts into the dates the first step offers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written once and used by both flows.</b> The two builders already answer "which times" the
/// same way; answering "which dates" twice would be two implementations of one rule, and the way
/// they would eventually differ is that one lists a date the other's times say is empty.
/// </para>
/// <para>
/// <b>Grouped by LOCAL date, and this is the subtle one.</b> A start at 23:30 UTC is the next day
/// in <c>Europe/London</c> in summer. Grouping by the UTC date would put a date in the list whose
/// times, rendered in the site zone, belong to the day either side of it — and it would do so
/// only for sites not running in UTC, so a UTC-configured test suite would report it perfectly
/// correct.
/// </para>
/// </remarks>
public static class AvailableDateProjection
{
    /// <summary>
    /// The dates within the window that admit <paramref name="duration"/>, ascending.
    /// </summary>
    /// <param name="startsUtc">
    /// Every bookable start in the window, paired with whether it admits the chosen length. The
    /// caller supplies the admission test because the two flows carry different start types —
    /// what must not differ is the <i>predicate</i>, which each takes from the same place its own
    /// times do.
    /// </param>
    /// <param name="zone">The site zone, in which a "date" means anything at all.</param>
    /// <param name="selectedDate">The date the step is showing times for.</param>
    public static IReadOnlyList<AvailableDate> Dates(
        IEnumerable<(DateTimeOffset StartUtc, bool Admits)> startsUtc,
        TimeZoneInfo zone,
        DateOnly selectedDate)
    {
        ArgumentNullException.ThrowIfNull(startsUtc);
        ArgumentNullException.ThrowIfNull(zone);

        return startsUtc
            .Where(start => start.Admits)
            .Select(start => DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(start.StartUtc, zone).DateTime))
            .Distinct()
            .OrderBy(date => date)
            .Select(date => new AvailableDate(date, date == selectedDate))
            .ToList();
    }
}
