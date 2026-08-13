namespace UBookIt.Core.Availability;

/// <summary>
/// Calendar-edge arithmetic. Every operation that could step outside the
/// representable range of <see cref="DateOnly"/> or <see cref="DateTimeOffset"/>
/// lives here, so the reasoning about those limits exists in one place rather
/// than at each call site.
/// <para>
/// The motivating defect (out-of-range-dates): three independent pieces of code
/// added or subtracted days without considering the boundary, and each threw an
/// unhandled exception out of an anonymous endpoint. Guarding at the call sites
/// individually would have left the next one to rediscover it.
/// </para>
/// <para>
/// The type is public but only <see cref="AddDaysSaturating"/> is: the default
/// front-end needs that one when it renders a date picker's upper bound and
/// lives in another assembly, and copying it across that boundary would
/// recreate the duplication this type exists to remove. The rest is internal —
/// public API is a compatibility promise, and nothing outside Core needs it.
/// </para>
/// </summary>
public static class CalendarBounds
{
    /// <summary>
    /// Whether a day-by-day walk ending at <paramref name="toDate"/> can run to
    /// completion. Such a walk increments once more after processing its final
    /// day, so the last representable date cannot be its end.
    /// </summary>
    internal static bool IsWalkableTo(DateOnly toDate) => toDate < DateOnly.MaxValue;

    /// <summary>
    /// The inclusive day range spanning one day either side of
    /// <paramref name="date"/>, as the open-hours rule requires. Returns false
    /// when either neighbour lies outside the calendar, or when the resulting
    /// range could not then be walked.
    /// </summary>
    internal static bool TryWindowAround(DateOnly date, out DateOnly from, out DateOnly to)
    {
        from = default;
        to = default;

        // A margin of two days is needed at each end, for different reasons.
        //
        // Above: the day after must exist, and the walk steps once past it.
        //
        // Below: the day before must exist, and must itself be mappable to UTC.
        // Mapping a wall-clock time on the first representable date from a site
        // zone east of UTC lands before the calendar starts, so a window whose
        // earlier day is that date throws for such a site — the same reasoning
        // that rejects `from == MinValue` on the query path.
        if (date.DayNumber <= DateOnly.MinValue.DayNumber + 1
            || date.DayNumber >= DateOnly.MaxValue.DayNumber - 1)
        {
            return false;
        }

        from = date.AddDays(-1);
        to = date.AddDays(1);
        return true;
    }

    /// <summary>
    /// Adds whole days, saturating at the ends of the calendar instead of
    /// throwing. Used for the booking horizon, where "further ahead than the
    /// calendar reaches" simply means "no effective limit" — a saturating
    /// answer is correct, so there is nothing to report to the caller.
    /// </summary>
    public static DateOnly AddDaysSaturating(DateOnly date, int days)
    {
        var target = (long)date.DayNumber + days;

        if (target >= DateOnly.MaxValue.DayNumber)
        {
            return DateOnly.MaxValue;
        }

        return target <= DateOnly.MinValue.DayNumber ? DateOnly.MinValue : DateOnly.FromDayNumber((int)target);
    }

    /// <summary>
    /// Adds a duration to an instant, reporting failure instead of throwing when
    /// the result would fall outside the representable range. Both directions
    /// matter: a far-future start overflows, a large negative duration underflows.
    /// <para>
    /// There are two independent limits, and comparing against
    /// <see cref="DateTimeOffset.MaxValue"/>/<see cref="DateTimeOffset.MinValue"/>
    /// tests neither of them correctly. Those subtractions measure headroom in
    /// <em>UTC</em>, whereas the addition moves the <em>clock</em> component and
    /// keeps the offset; the result must then also convert back to a
    /// representable UTC instant. For a start carrying an eastern offset the
    /// clock can overflow while UTC still has room — which is exactly how the
    /// first version of this guard let a 500 through, so both limits are checked
    /// explicitly here rather than inferred from one comparison.
    /// </para>
    /// </summary>
    internal static bool TryAdd(DateTimeOffset start, TimeSpan duration, out DateTimeOffset end)
    {
        end = default;

        var clock = start.DateTime;

        if (duration > DateTime.MaxValue - clock || duration < DateTime.MinValue - clock)
        {
            return false;
        }

        var shifted = clock + duration;

        // The DateTimeOffset constructor additionally requires the UTC
        // equivalent — clock minus offset — to be representable.
        var utcTicks = shifted.Ticks - start.Offset.Ticks;
        if (utcTicks < DateTime.MinValue.Ticks || utcTicks > DateTime.MaxValue.Ticks)
        {
            return false;
        }

        end = new DateTimeOffset(shifted, start.Offset);
        return true;
    }
}
