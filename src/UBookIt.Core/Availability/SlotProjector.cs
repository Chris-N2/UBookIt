namespace UBookIt.Core.Availability;

/// <summary>
/// Projects bookable start times from free time. Candidates advance in
/// granularity steps from each free-interval start and must satisfy lead time
/// and horizon.
/// <para>
/// Both projections — fixed-duration slots and bookable starts — derive from
/// the single traversal in <see cref="Walk"/> rather than two independent
/// walks, so they cannot disagree about which starts are offered (availability
/// spec, "Slot projection"). Pure computation; nothing is persisted.
/// </para>
/// </summary>
internal static class SlotProjector
{
    /// <summary>
    /// Starts from which the whole <paramref name="duration"/> fits. Callers
    /// validate the duration against the resource's constraints first.
    /// </summary>
    internal static List<Slot> Project(
        IReadOnlyList<UtcInterval> freeIntervals,
        BookingConstraints constraints,
        TimeSpan duration,
        DateTimeOffset nowUtc,
        TimeZoneInfo zone)
    {
        var slots = new List<Slot>();

        // The `duration <= maxRun` test below is equivalent to the older
        // "does [start, start+duration) fit in the interval" test only for a
        // duration that is a positive granularity multiple. Callers validate
        // first, but enforcing it here keeps the equivalence a property of the
        // projection rather than of every present and future caller: an
        // unbookable length yields nothing instead of a subtly different set.
        if (duration <= TimeSpan.Zero || duration.Ticks % constraints.Granularity.Ticks != 0)
        {
            return slots;
        }

        foreach (var (start, maxRun) in Walk(freeIntervals, constraints, nowUtc, zone))
        {
            // Equivalent to the older "start + duration <= interval end" test:
            // a validated duration is a granularity multiple no greater than
            // MaxDuration, so it fits iff it is within the floored run.
            if (duration <= maxRun)
            {
                slots.Add(new Slot(start, duration));
            }
        }

        return slots;
    }

    /// <summary>
    /// Every start with the range of lengths bookable from it. A start offering
    /// less than the resource's minimum duration is omitted.
    /// </summary>
    internal static List<BookableStart> ProjectBookableStarts(
        IReadOnlyList<UtcInterval> freeIntervals,
        BookingConstraints constraints,
        DateTimeOffset nowUtc,
        TimeZoneInfo zone)
    {
        var starts = new List<BookableStart>();

        foreach (var (start, maxRun) in Walk(freeIntervals, constraints, nowUtc, zone))
        {
            if (maxRun >= constraints.MinDuration)
            {
                starts.Add(new BookableStart(start, constraints.MinDuration, maxRun));
            }
        }

        return starts;
    }

    /// <summary>
    /// Each candidate start paired with the longest bookable run from it: the
    /// remainder of its containing free interval, capped at the resource's
    /// maximum duration and floored to a granularity multiple.
    /// </summary>
    private static IEnumerable<(DateTimeOffset StartUtc, TimeSpan MaxRun)> Walk(
        IReadOnlyList<UtcInterval> freeIntervals,
        BookingConstraints constraints,
        DateTimeOffset nowUtc,
        TimeZoneInfo zone)
    {
        var earliestStart = nowUtc + constraints.LeadTime;
        var lastLocalDate = WallClockMapper.ToLocalDate(nowUtc, zone).AddDays(constraints.HorizonDays);

        foreach (var interval in freeIntervals)
        {
            for (var start = interval.StartUtc; start < interval.EndUtc; start += constraints.Granularity)
            {
                if (start < earliestStart)
                {
                    continue;
                }

                if (WallClockMapper.ToLocalDate(start, zone) > lastLocalDate)
                {
                    break;
                }

                var maxRun = DurationMath.FloorTo(
                    DurationMath.MinOf(interval.EndUtc - start, constraints.MaxDuration),
                    constraints.Granularity);

                if (maxRun > TimeSpan.Zero)
                {
                    yield return (start, maxRun);
                }
            }
        }
    }
}
