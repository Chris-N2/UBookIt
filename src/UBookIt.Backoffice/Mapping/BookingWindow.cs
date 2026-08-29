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
            return Failure(
                FailureCodes.TimeZoneInvalid,
                $"The site's configured time zone '{settings.TimeZoneId}' could not be resolved.");
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
    /// Midnight on a date in the site's zone, as an instant.
    /// <para>
    /// Midnight does not exist on every date in every zone — some zones spring forward at
    /// midnight, so the local time is skipped. <c>ConvertTimeToUtc</c> throws on those
    /// rather than choosing, so the first instant that does exist on that date is used.
    /// </para>
    /// </summary>
    private static DateTimeOffset StartOfDayUtc(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(local))
        {
            // Walk forward to the first valid minute. A DST gap is at most a couple of
            // hours, so this terminates quickly and lands on the instant the day begins.
            do
            {
                local = local.AddMinutes(1);
            }
            while (zone.IsInvalidTime(local));
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

    private static DomainResult<(DateTimeOffset, DateTimeOffset)> Failure(string code, string message)
        => DomainResult<(DateTimeOffset, DateTimeOffset)>.Failure(code, message, "to");
}
