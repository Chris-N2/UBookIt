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
/// Public rather than internal because the default front-end needs the same
/// saturating horizon arithmetic when it renders a date picker's upper bound,
/// and it lives in another assembly. Copying three lines across that boundary
/// would recreate the duplication this type exists to remove.
/// </para>
/// </summary>
public static class CalendarBounds
{
    /// <summary>
    /// Whether a day-by-day walk ending at <paramref name="toDate"/> can run to
    /// completion. Such a walk increments once more after processing its final
    /// day, so the last representable date cannot be its end.
    /// </summary>
    public static bool IsWalkableTo(DateOnly toDate) => toDate < DateOnly.MaxValue;

    /// <summary>
    /// The inclusive day range spanning one day either side of
    /// <paramref name="date"/>, as the open-hours rule requires. Returns false
    /// when either neighbour lies outside the calendar, or when the resulting
    /// range could not then be walked.
    /// </summary>
    public static bool TryWindowAround(DateOnly date, out DateOnly from, out DateOnly to)
    {
        from = default;
        to = default;

        // Needs a day before, a day after, and that day after must itself be
        // walkable — the walk steps once past it.
        if (date.DayNumber <= DateOnly.MinValue.DayNumber
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
    /// </summary>
    public static bool TryAdd(DateTimeOffset start, TimeSpan duration, out DateTimeOffset end)
    {
        end = default;

        if (duration > DateTimeOffset.MaxValue - start || duration < DateTimeOffset.MinValue - start)
        {
            return false;
        }

        end = start + duration;
        return true;
    }
}
