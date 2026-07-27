namespace UBookIt.Core.Availability;

/// <summary>
/// Projects bookable start times for a duration: candidates advance in
/// granularity steps from each free-interval start and are offered iff the
/// whole [start, start + duration) fits inside that free interval and
/// satisfies lead time and horizon. Pure computation; nothing is persisted.
/// </summary>
internal static class SlotProjector
{
    internal static List<Slot> Project(
        IReadOnlyList<UtcInterval> freeIntervals,
        BookingConstraints constraints,
        TimeSpan duration,
        DateTimeOffset nowUtc,
        TimeZoneInfo zone)
    {
        var slots = new List<Slot>();
        var earliestStart = nowUtc + constraints.LeadTime;
        var lastLocalDate = WallClockMapper.ToLocalDate(nowUtc, zone).AddDays(constraints.HorizonDays);

        foreach (var interval in freeIntervals)
        {
            for (var start = interval.StartUtc; start + duration <= interval.EndUtc; start += constraints.Granularity)
            {
                if (start < earliestStart)
                {
                    continue;
                }

                if (WallClockMapper.ToLocalDate(start, zone) > lastLocalDate)
                {
                    break;
                }

                slots.Add(new Slot(start, duration));
            }
        }

        return slots;
    }
}
