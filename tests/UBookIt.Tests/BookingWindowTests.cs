using UBookIt.Backoffice.Mapping;
using UBookIt.Core;
using UBookIt.Core.Common;

namespace UBookIt.Tests;

/// <summary>
/// The site-local date window, and its conversion to the instants the read port takes.
/// <para>
/// This is where the timezone rule lives, once, because the site's zone is a server
/// setting and a rule reimplemented per client differs per client at a daylight-saving
/// boundary.
/// </para>
/// </summary>
public class BookingWindowTests
{
    private static SiteBookingSettings Settings(string zone = "UTC", int maxDays = 31)
        => new() { TimeZoneId = zone, MaxQueryRangeDays = maxDays };

    /// <summary>
    /// London: clocks go forward 2026-03-29 01:00 and back 2026-10-25 02:00. A real zone
    /// with both transitions, rather than a fixed offset that would let a broken
    /// conversion pass.
    /// </summary>
    private const string London = "Europe/London";

    [Fact]
    public void The_last_named_date_is_included_in_full()
    {
        // The off-by-one here is invisible until an operator notices a booking missing
        // from the last day of the range they asked for.
        var window = BookingWindow.Resolve(
            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 7), Settings());

        Assert.True(window.Succeeded);
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), window.Value.FromUtc);

        // Exclusive end is the START of the day after — so all of the 7th is inside.
        Assert.Equal(new DateTimeOffset(2026, 6, 8, 0, 0, 0, TimeSpan.Zero), window.Value.ToUtc);
    }

    [Fact]
    public void A_single_date_is_a_window_of_one_day()
    {
        var window = BookingWindow.Resolve(
            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 1), Settings());

        Assert.True(window.Succeeded);
        Assert.Equal(TimeSpan.FromDays(1), window.Value.ToUtc - window.Value.FromUtc);
    }

    [Fact]
    public void The_window_is_the_sites_local_day_not_a_utc_one()
    {
        // London in summer is UTC+1, so a local day starts at 23:00 the previous UTC day.
        // A conversion that ignored the zone would return midnight UTC and quietly shift
        // every window by an hour — showing bookings from the wrong day at both ends.
        var window = BookingWindow.Resolve(
            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 1), Settings(London));

        Assert.True(window.Succeeded);
        Assert.Equal(new DateTimeOffset(2026, 5, 31, 23, 0, 0, TimeSpan.Zero), window.Value.FromUtc);
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero), window.Value.ToUtc);
    }

    [Fact]
    public void A_spring_forward_day_is_twenty_three_hours()
    {
        // 2026-03-29: London loses an hour at 01:00. The site-local day is what defines
        // the window, so it is 23 hours — not 24, and not "24 minus a fudge".
        var window = BookingWindow.Resolve(
            new DateOnly(2026, 3, 29), new DateOnly(2026, 3, 29), Settings(London));

        Assert.True(window.Succeeded);
        Assert.Equal(TimeSpan.FromHours(23), window.Value.ToUtc - window.Value.FromUtc);
    }

    [Fact]
    public void A_fall_back_day_is_twenty_five_hours()
    {
        // 2026-10-25: London gains an hour at 02:00.
        var window = BookingWindow.Resolve(
            new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 25), Settings(London));

        Assert.True(window.Succeeded);
        Assert.Equal(TimeSpan.FromHours(25), window.Value.ToUtc - window.Value.FromUtc);
    }

    [Fact]
    public void A_window_of_the_maximum_number_of_dates_is_allowed_even_across_a_fall_back()
    {
        // The case the design predicted would bite: 31 dates spanning the fall-back is 31
        // days and one hour of elapsed time, which BookingQuery.Create measures and would
        // refuse. The endpoint counts DATES, so the window a caller would consider legal
        // is legal — and this asserts the elapsed span really does exceed 31 days, so the
        // test cannot pass by the situation never arising.
        var from = new DateOnly(2026, 10, 10);
        var to = from.AddDays(30);

        var window = BookingWindow.Resolve(from, to, Settings(London, maxDays: 31));

        Assert.True(
            window.Succeeded,
            "A window of exactly the maximum number of dates must be accepted.");
        Assert.True(
            (window.Value.ToUtc - window.Value.FromUtc).TotalDays > 31,
            "This fixture no longer spans the fall-back boundary, so it is not testing the case.");
    }

    /// <summary>
    /// Havana falls back <b>at local midnight</b> on 2025-11-02, so 00:00 happens twice.
    /// </summary>
    private const string Havana = "America/Havana";

    /// <summary>
    /// Santiago springs forward <b>at local midnight</b> on 2026-09-06, so 00:00 does not
    /// exist at all.
    /// </summary>
    private const string Santiago = "America/Santiago";

    [Fact]
    public void A_day_that_begins_twice_begins_at_the_first_one()
    {
        // The defect this test exists for, and the mirror of the gap case below.
        //
        // ConvertTimeToUtc resolves an ambiguous local time to STANDARD time — the second
        // occurrence — so the day was starting an hour late. Bookings in the first hour of
        // 2 November were missing from 2 November and appeared on 1 November instead, and
        // the 25-hour day was attributed to the wrong date. Both ends of every window
        // containing such a date were wrong.
        var window = BookingWindow.Resolve(
            new DateOnly(2025, 11, 2), new DateOnly(2025, 11, 2), Settings(Havana));

        Assert.True(window.Succeeded);
        Assert.Equal(new DateTimeOffset(2025, 11, 2, 4, 0, 0, TimeSpan.Zero), window.Value.FromUtc);

        // And the 25 hours belong to 2 November, not to 1 November.
        Assert.Equal(TimeSpan.FromHours(25), window.Value.ToUtc - window.Value.FromUtc);

        var previous = BookingWindow.Resolve(
            new DateOnly(2025, 11, 1), new DateOnly(2025, 11, 1), Settings(Havana));

        Assert.Equal(TimeSpan.FromHours(24), previous.Value.ToUtc - previous.Value.FromUtc);
        Assert.Equal(window.Value.FromUtc, previous.Value.ToUtc);
    }

    [Fact]
    public void A_day_whose_midnight_does_not_exist_begins_at_its_first_real_instant()
    {
        // The gap branch had no test at all — deleting it would have failed nothing,
        // because every fixture used UTC or Europe/London, neither of which skips
        // midnight. Santiago does.
        var window = BookingWindow.Resolve(
            new DateOnly(2026, 9, 6), new DateOnly(2026, 9, 6), Settings(Santiago));

        Assert.True(window.Succeeded);

        // The day is short, and it begins the moment it begins rather than at a local
        // midnight that never occurred.
        Assert.Equal(TimeSpan.FromHours(23), window.Value.ToUtc - window.Value.FromUtc);

        // Contiguous with the day before: no gap, no overlap, so no booking can fall
        // between two adjacent windows or appear in both.
        var previous = BookingWindow.Resolve(
            new DateOnly(2026, 9, 5), new DateOnly(2026, 9, 5), Settings(Santiago));

        Assert.Equal(window.Value.FromUtc, previous.Value.ToUtc);
    }

    [Theory]
    [InlineData(Havana)]
    [InlineData(Santiago)]
    [InlineData(London)]
    public void Consecutive_days_never_overlap_or_leave_a_gap(string zone)
    {
        // The property both transitions threaten, asserted over a whole year rather than
        // at the two dates someone remembered. A booking must belong to exactly one day.
        var settings = Settings(zone);
        var date = new DateOnly(2025, 6, 1);

        for (var day = 0; day < 400; day++)
        {
            var current = BookingWindow.Resolve(date, date, settings);
            var next = BookingWindow.Resolve(date.AddDays(1), date.AddDays(1), settings);

            Assert.True(current.Succeeded && next.Succeeded);
            Assert.Equal(current.Value.ToUtc, next.Value.FromUtc);
            Assert.True(
                current.Value.ToUtc > current.Value.FromUtc,
                $"{zone} {date:O} did not move forwards.");

            date = date.AddDays(1);
        }
    }

    [Fact]
    public void One_date_past_the_maximum_is_refused_in_terms_of_the_dates_sent()
    {
        var from = new DateOnly(2026, 6, 1);
        var result = BookingWindow.Resolve(from, from.AddDays(31), Settings(maxDays: 31));

        Assert.False(result.Succeeded);

        var failure = Assert.Single(result.Failures);
        Assert.Equal(FailureCodes.DateRangeTooLarge, failure.Code);

        // Named in dates, not in converted instants the caller never supplied.
        Assert.Contains("2026-06-01", failure.Message, StringComparison.Ordinal);
        Assert.Contains("32 days", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_backwards_window_is_refused_with_its_own_code()
    {
        var result = BookingWindow.Resolve(
            new DateOnly(2026, 6, 7), new DateOnly(2026, 6, 1), Settings());

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.DateRangeInvalid, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void An_unresolvable_site_zone_is_reported_as_such()
    {
        // A site misconfiguration, and the message says so — otherwise an operator hunts
        // their own query for a fault that is in settings.
        var result = BookingWindow.Resolve(
            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 2), Settings("Not/AZone"));

        Assert.False(result.Succeeded);

        var failure = Assert.Single(result.Failures);
        Assert.Equal(FailureCodes.TimeZoneInvalid, failure.Code);
        Assert.Contains("Not/AZone", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_maximum_is_the_site_setting_rather_than_a_constant()
    {
        var from = new DateOnly(2026, 6, 1);

        Assert.True(BookingWindow.Resolve(from, from.AddDays(59), Settings(maxDays: 90)).Succeeded);
        Assert.False(BookingWindow.Resolve(from, from.AddDays(59), Settings(maxDays: 7)).Succeeded);
    }
}
