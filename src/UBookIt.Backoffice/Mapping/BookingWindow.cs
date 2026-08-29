using UBookIt.Core;
using UBookIt.Core.Common;

namespace UBookIt.Backoffice.Mapping;

/// <summary>
/// Turns the pair of dates an operator asks for into the half-open UTC instants the
/// booking read port takes.
/// </summary>
/// <remarks>
/// <para>
/// <b>The conversion happens once, server-side, because the site's time zone is a server
/// setting.</b> Pushed to callers it would be reimplemented per client — and the package
/// already serves a headless delivery API, so "the client" is not one client. Two
/// implementations of a daylight-saving rule is one more than the number that can be right.
/// </para>
/// <para>
/// <b>Both named dates are included.</b> Naming a Monday and a Sunday means all of Sunday,
/// which is what anyone means, so the exclusive end is the start of the day after
/// <c>toDate</c>.
/// </para>
/// </remarks>
internal static class BookingWindow
{
    /// <summary>
    /// Resolves a site-local date pair to a half-open UTC window, or explains why it
    /// cannot — always in terms of the dates the caller sent.
    /// </summary>
    public static DomainResult<(DateTimeOffset FromUtc, DateTimeOffset ToUtc)> Resolve(
        DateOnly fromDate, DateOnly toDate, SiteBookingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (toDate < fromDate)
        {
            return Failure(
                FailureCodes.DateRangeInvalid,
                $"The window must end on or after it starts; {fromDate:O} to {toDate:O} runs backwards.");
        }

        // Inclusive, matching how the availability guard counts the same setting: naming
        // one date is a window of one day, not of zero.
        var dateCount = toDate.DayNumber - fromDate.DayNumber + 1;

        if (dateCount > settings.MaxQueryRangeDays)
        {
            return Failure(
                FailureCodes.DateRangeTooLarge,
                $"The window {fromDate:O} to {toDate:O} covers {dateCount} days, which exceeds "
                + $"the maximum of {settings.MaxQueryRangeDays}.");
        }

        if (!TryFindZone(settings.TimeZoneId, out var zone))
        {
            // No field: the fault is the site's TimeZoneId setting, not anything the
            // caller sent. Attributing it to `to` tells them to change a parameter that
            // is fine, and sends them looking in the one place the answer is not.
            return Failure(
                FailureCodes.TimeZoneInvalid,
                $"The site's configured time zone '{settings.TimeZoneId}' could not be resolved.",
                field: null);
        }

        // Dates are the only bound applied here, deliberately.
        //
        // An earlier version checked the converted span too, and that was wrong: a window
        // of exactly the maximum number of DATES resolves to slightly more than that many
        // days of elapsed time whenever it contains a fall-back transition. With the
        // default guardrail of 31, that refused "show me October" on every European site,
        // annually. The port counts whole days for the same reason, so a window this
        // method accepts is one the port accepts.
        return DomainResult<(DateTimeOffset, DateTimeOffset)>.Success(
            (StartOfDayUtc(fromDate, zone), StartOfDayUtc(toDate.AddDays(1), zone)));
    }

    /// <summary>
    /// The instant a date <b>begins</b> in the site's zone.
    /// <para>
    /// Midnight is neither guaranteed to exist nor guaranteed to be unique, and a
    /// daylight-saving transition at local midnight produces one of each. Both are
    /// handled, because handling one and not the other is how this method was wrong.
    /// </para>
    /// </summary>
    private static DateTimeOffset StartOfDayUtc(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(local))
        {
            // A spring-forward at midnight SKIPS the local time entirely, and
            // ConvertTimeToUtc throws rather than choosing. Walk to the first minute that
            // does exist — a gap is at most a couple of hours, so this terminates quickly.
            do
            {
                local = local.AddMinutes(1);
            }
            while (zone.IsInvalidTime(local));
        }
        else if (zone.IsAmbiguousTime(local))
        {
            // A fall-back at midnight makes the local time happen TWICE, and
            // ConvertTimeToUtc resolves an ambiguous time to standard time — the SECOND
            // occurrence. The day begins the first time it begins, so take the earliest
            // instant, which is the largest UTC offset.
            //
            // Getting this wrong moved the whole day boundary by the DST delta: bookings
            // in the first hour of the date landed on the previous day, the 25-hour day
            // was attributed to the wrong date, and both ends of every window shifted.
            // Real today in America/Havana, and historically in Brazil.
            return new DateTimeOffset(
                local - zone.GetAmbiguousTimeOffsets(local).Max(), TimeSpan.Zero);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }

    private static bool TryFindZone(string timeZoneId, out TimeZoneInfo zone)
    {
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            zone = TimeZoneInfo.Utc;
            return false;
        }
    }

    private static DomainResult<(DateTimeOffset, DateTimeOffset)> Failure(
        string code, string message, string? field = "to")
        => DomainResult<(DateTimeOffset, DateTimeOffset)>.Failure(code, message, field);
}
