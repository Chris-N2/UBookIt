namespace UBookIt.Core.Availability;

/// <summary>
/// Facts about runs of closed dates — site closures and closed periods — that need no
/// availability computation.
/// </summary>
public static class ClosedRanges
{
    /// <summary>
    /// The first date from <paramref name="from"/> onwards that none of
    /// <paramref name="ranges"/> covers, following ranges that adjoin or overlap one another.
    /// Returns <paramref name="from"/> itself when nothing covers it.
    /// </summary>
    /// <remarks>
    /// <b>Only ranges, never hours or bookings.</b> A date that is merely fully booked, or
    /// outside a weekly pattern, is not "closed" here: the answer is a cheap fact about
    /// ranges, not a search for availability, so callers can use it to place a window without
    /// paying for one. The cost is one sort of the ranges, whatever their length.
    /// </remarks>
    public static DateOnly FirstUncovered(DateOnly from, IEnumerable<(DateOnly Start, DateOnly End)> ranges)
    {
        var candidate = from;

        foreach (var (start, end) in ranges.OrderBy(r => r.Start))
        {
            if (start > candidate)
            {
                // Sorted by start: every later range starts later still, so nothing
                // reaches back to cover the candidate.
                break;
            }

            if (end >= candidate)
            {
                // Saturating: a range ending on the last representable date leaves no
                // uncovered date after it, and the candidate stays at that end.
                if (end == DateOnly.MaxValue)
                {
                    return end;
                }

                candidate = end.AddDays(1);
            }
        }

        return candidate;
    }

    /// <summary>The ranges an availability configuration closes by its own and inherited rules.</summary>
    public static IEnumerable<(DateOnly Start, DateOnly End)> Of(AvailabilityConfiguration availability)
        => availability.Closures.Select(c => (c.Date, c.EndDate))
            .Concat(availability.ClosedPeriods.Select(p => (p.StartDate, p.EndDate)));
}
