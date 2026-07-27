namespace UBookIt.Core.Availability;

/// <summary>
/// Maps wall-clock times in the site zone to UTC instants with the DST rules
/// fixed by the availability spec: times inside a spring-forward gap collapse
/// to the transition instant (the gap has zero UTC measure, so gapped
/// wall-clock time is never bookable); ambiguous fall-back times resolve to
/// their first occurrence (the earlier UTC offset).
/// </summary>
internal static class WallClockMapper
{
    internal static DateTimeOffset ToUtc(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(local))
        {
            // Walk back to the last valid minute before the gap; the gap start
            // paired with the pre-transition offset is the transition instant.
            // (DST transitions land on minute boundaries.)
            var probe = local;
            do
            {
                probe = probe.AddMinutes(-1);
            }
            while (zone.IsInvalidTime(probe));

            var offsetBefore = zone.GetUtcOffset(probe);
            return new DateTimeOffset(probe.AddMinutes(1), offsetBefore).ToUniversalTime();
        }

        if (zone.IsAmbiguousTime(local))
        {
            // First occurrence = earlier instant = larger UTC offset.
            var offset = zone.GetAmbiguousTimeOffsets(local).Max();
            return new DateTimeOffset(local, offset).ToUniversalTime();
        }

        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }

    internal static DateOnly ToLocalDate(DateTimeOffset utc, TimeZoneInfo zone)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utc, zone).DateTime);
}
