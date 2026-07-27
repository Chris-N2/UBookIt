using UBookIt.Core.Stores;

namespace UBookIt.Core.Availability;

/// <summary>
/// Pure free-time computation: effective daily windows (weekly pattern with
/// exceptions applied) mapped to UTC, minus intervals claimed by blocking
/// bookings. Output is ordered and disjoint.
/// </summary>
internal static class FreeTimeCalculator
{
    /// <summary>
    /// The resource's open windows for the date range, as UTC intervals.
    /// Touching or overlapping windows coalesce: back-to-back windows form
    /// continuous bookable time, so an interval spanning their join is inside
    /// open hours.
    /// </summary>
    internal static List<UtcInterval> OpenIntervals(
        AvailabilityConfiguration config, TimeZoneInfo zone, DateOnly fromDate, DateOnly toDate)
    {
        var open = new List<UtcInterval>();

        for (var date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            foreach (var window in config.EffectiveWindows(date))
            {
                var start = WallClockMapper.ToUtc(date, window.Start, zone);
                var end = WallClockMapper.ToUtc(date, window.End, zone);

                if (start < end)
                {
                    open.Add(new UtcInterval(start, end));
                }
            }
        }

        open.Sort(static (a, b) => a.StartUtc.CompareTo(b.StartUtc));

        var merged = new List<UtcInterval>(open.Count);
        foreach (var interval in open)
        {
            if (merged.Count > 0 && interval.StartUtc <= merged[^1].EndUtc)
            {
                if (interval.EndUtc > merged[^1].EndUtc)
                {
                    merged[^1] = new UtcInterval(merged[^1].StartUtc, interval.EndUtc);
                }
            }
            else
            {
                merged.Add(interval);
            }
        }

        return merged;
    }

    /// <summary>Open intervals minus the given blocking claim intervals.</summary>
    internal static List<UtcInterval> Subtract(List<UtcInterval> open, IEnumerable<ClaimInfo> blockingClaims)
    {
        var blocks = blockingClaims
            .Select(c => new UtcInterval(c.Interval.StartUtc, c.Interval.EndUtc))
            .OrderBy(b => b.StartUtc)
            .ToList();

        var free = new List<UtcInterval>();

        foreach (var window in open)
        {
            var cursor = window.StartUtc;

            foreach (var block in blocks)
            {
                if (block.EndUtc <= cursor)
                {
                    continue;
                }

                if (block.StartUtc >= window.EndUtc)
                {
                    break;
                }

                if (block.StartUtc > cursor)
                {
                    free.Add(new UtcInterval(cursor, block.StartUtc));
                }

                cursor = block.EndUtc > cursor ? block.EndUtc : cursor;

                if (cursor >= window.EndUtc)
                {
                    break;
                }
            }

            if (cursor < window.EndUtc)
            {
                free.Add(new UtcInterval(cursor, window.EndUtc));
            }
        }

        return free;
    }
}
