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
