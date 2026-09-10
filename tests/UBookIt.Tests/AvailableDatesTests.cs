using UBookIt.Core.Availability;
using UBookIt.Tests.Support;
using UBookIt.Web.Rendering;

namespace UBookIt.Tests;

/// <summary>
/// Which dates the first step offers, how wide a span it reads to find them, and the one
/// property that makes widening the read safe.
/// </summary>
/// <remarks>
/// <b>The window tests use CONFIGURED bounds, not defaults.</b> Every bound is generous out of
/// the box — a 90-day horizon against a 31-day guardrail — so a window that ignored them looks
/// perfectly correct on a default configuration and fails only for sites that have tightened
/// something. The default arrangement is the one in which this cannot be tested.
/// </remarks>
public class AvailableDatesTests
{
    private static readonly DateOnly Today = new(2026, 9, 10);

    // ---------------------------------------------------------------------
    // The window
    // ---------------------------------------------------------------------

    [Fact]
    public void The_default_bounds_give_the_preferred_window()
    {
        var (from, to) = AvailableDateWindow.Compute(Today, horizonDays: 90, maxQueryRangeDays: 31);

        Assert.Equal(Today, from);
        Assert.Equal(Today.AddDays(AvailableDateWindow.PreferredDays - 1), to);
    }

    [Fact]
    public void A_tighter_query_guardrail_shortens_the_window_rather_than_failing_the_read()
    {
        // THE HIGHEST-SEVERITY FAILURE IN THIS CHANGE, and the least visible.
        //
        // MaxQueryRangeDays is not advice: the availability service REFUSES a span wider than
        // it, with `date-range-too-large`. A window that ignored the guardrail would therefore
        // not merely list too much — it would fail the read on every first-step render, and a
        // site that had tightened the setting would have no booking flow at all.
        var (from, to) = AvailableDateWindow.Compute(Today, horizonDays: 90, maxQueryRangeDays: 7);

        Assert.Equal(Today, from);
        Assert.Equal(Today.AddDays(6), to);

        // Asserted as the SERVICE counts it — inclusive — because that is the arithmetic the
        // refusal uses. Counting exclusively here would let a window one day too wide pass a
        // test written in the wrong units.
        Assert.Equal(7, to.DayNumber - from.DayNumber + 1);
    }

    [Fact]
    public void The_window_never_exceeds_the_guardrail_at_any_setting()
    {
        // The property rather than a sample, because the boundary is where an off-by-one lives
        // and a handful of cases is exactly how one survives.
        foreach (var guardrail in Enumerable.Range(1, 40))
        {
            var (from, to) = AvailableDateWindow.Compute(Today, horizonDays: 365, guardrail);

            Assert.True(
                to.DayNumber - from.DayNumber + 1 <= guardrail,
                $"A guardrail of {guardrail} produced a span of {to.DayNumber - from.DayNumber + 1} days, "
                + "which the availability read would refuse.");
        }
    }

    [Fact]
    public void A_short_horizon_shortens_the_window()
    {
        // A horizon of 3 means today plus three more days are bookable — four days inclusive.
        var (from, to) = AvailableDateWindow.Compute(Today, horizonDays: 3, maxQueryRangeDays: 31);

        Assert.Equal(Today, from);
        Assert.Equal(Today.AddDays(3), to);
    }

    [Fact]
    public void A_horizon_of_today_only_still_produces_a_usable_window()
    {
        // The degenerate case. A window of nothing would make the read span zero days and the
        // step render an empty list beside a date that is in fact bookable.
        var (from, to) = AvailableDateWindow.Compute(Today, horizonDays: 0, maxQueryRangeDays: 31);

        Assert.Equal(Today, from);
        Assert.Equal(Today, to);
    }

    [Fact]
    public void A_very_large_horizon_does_not_overflow_the_calendar()
    {
        // HorizonDays is validated as positive but not as small, and the date field's own upper
        // bound already saturates for the same reason.
        var (from, to) = AvailableDateWindow.Compute(
            DateOnly.MaxValue.AddDays(-2), horizonDays: int.MaxValue, maxQueryRangeDays: int.MaxValue);

        Assert.True(to >= from);
        Assert.True(to <= DateOnly.MaxValue);
    }

    // ---------------------------------------------------------------------
    // Which dates are listed
    // ---------------------------------------------------------------------

    private static (DateTimeOffset StartUtc, bool Admits) Start(string utc, bool admits = true)
        => (DateTimeOffset.Parse(utc, null, System.Globalization.DateTimeStyles.RoundtripKind), admits);

    [Fact]
    public void Only_dates_with_an_admitting_start_are_listed()
    {
        var dates = AvailableDateProjection.Dates(
            [
                Start("2026-09-10T09:00:00Z"),
                Start("2026-09-11T09:00:00Z", admits: false),
                Start("2026-09-12T09:00:00Z"),
            ],
            TimeZoneInfo.Utc,
            selectedDate: new DateOnly(2026, 9, 10));

        Assert.Equal(
            [new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 12)],
            dates.Select(date => date.Date));
    }

    [Fact]
    public void A_date_with_only_non_admitting_starts_is_absent_and_with_one_is_present()
    {
        // Both directions. A filter asserted in one direction only can be inverted and still
        // pass — the whole predicate could be negated and half the suite would agree.
        var day = new DateOnly(2026, 9, 11);

        var absent = AvailableDateProjection.Dates(
            [Start("2026-09-11T09:00:00Z", admits: false)], TimeZoneInfo.Utc, day);
        var present = AvailableDateProjection.Dates(
            [Start("2026-09-11T09:00:00Z", admits: true)], TimeZoneInfo.Utc, day);

        Assert.Empty(absent);
        Assert.Equal(day, Assert.Single(present).Date);
    }

    [Fact]
    public void A_date_is_listed_once_however_many_starts_it_has()
    {
        var dates = AvailableDateProjection.Dates(
            [
                Start("2026-09-10T09:00:00Z"),
                Start("2026-09-10T10:00:00Z"),
                Start("2026-09-10T11:00:00Z"),
            ],
            TimeZoneInfo.Utc,
            new DateOnly(2026, 9, 10));

        Assert.Single(dates);
    }

    [Fact]
    public void Dates_are_grouped_in_the_SITE_zone_and_not_in_UTC()
    {
        // THE ONE THAT ONLY FAILS FOR SITES THAT ARE NOT UTC.
        //
        // 23:30 UTC on 10 September is 00:30 on the ELEVENTH in Europe/London. A UTC-grouped
        // implementation lists the tenth — and puts that date in the list while the times
        // rendered beneath it, converted to the site zone, belong to the eleventh.
        //
        // A suite configured in UTC cannot see this at all, which is why the fixture names a
        // zone rather than relying on the default.
        var dates = AvailableDateProjection.Dates(
            [Start("2026-09-10T23:30:00Z")],
            TestData.London,
            selectedDate: new DateOnly(2026, 9, 11));

        Assert.Equal(new DateOnly(2026, 9, 11), Assert.Single(dates).Date);
    }

    [Fact]
    public void The_selected_date_is_marked_and_only_it()
    {
        var dates = AvailableDateProjection.Dates(
            [Start("2026-09-10T09:00:00Z"), Start("2026-09-11T09:00:00Z")],
            TimeZoneInfo.Utc,
            selectedDate: new DateOnly(2026, 9, 11));

        Assert.Equal(new DateOnly(2026, 9, 11), Assert.Single(dates, date => date.IsSelected).Date);
    }

    [Fact]
    public void Dates_are_ascending()
    {
        var dates = AvailableDateProjection.Dates(
            [Start("2026-09-14T09:00:00Z"), Start("2026-09-10T09:00:00Z"), Start("2026-09-12T09:00:00Z")],
            TimeZoneInfo.Utc,
            new DateOnly(2026, 9, 10));

        Assert.Equal(dates.Select(date => date.Date).Order(), dates.Select(date => date.Date));
    }

    // ---------------------------------------------------------------------
    // The differential property — the load-bearing claim of the change
    // ---------------------------------------------------------------------

    // The differential itself — window read versus single-day read, both genuinely issued —
    // lives in AvailableDatesFlowTests. It was here, comparing a hand-built list against the
    // same predicate re-typed over that same list, and calling nothing. A guard watching a
    // reconstruction of the thing it guards watches nothing; QA said so and was right.
    //
    // What remains below is about the FILTER's own semantics, which is a different and smaller
    // claim, and is honestly testable without a read.

    [Fact]
    public void The_day_filter_uses_the_site_zone()
    {
        // The same midnight-straddling case, on the filter rather than the grouping. If these
        // two disagreed about which day a start belongs to, the list and the times would
        // disagree — which is the one thing deriving both from one read is supposed to prevent.
        var start = new BookableStart(
            DateTimeOffset.Parse("2026-09-10T23:30:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromHours(1));

        Assert.Empty(BookingFormBuilder.OnDate([start], TestData.London, new DateOnly(2026, 9, 10)));
        Assert.Single(BookingFormBuilder.OnDate([start], TestData.London, new DateOnly(2026, 9, 11)));
    }

    [Fact]
    public void The_grouping_and_the_day_filter_agree_about_every_start()
    {
        // The two halves of "one reading cannot contradict itself", checked against each other
        // rather than each against a literal. For every start in a window, the date the list
        // groups it under is the date the filter would return it for.
        var starts = new List<BookableStart>
        {
            new(DateTimeOffset.Parse("2026-09-10T23:30:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind), TimeSpan.FromMinutes(30), TimeSpan.FromHours(1)),
            new(DateTimeOffset.Parse("2026-09-11T00:30:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind), TimeSpan.FromMinutes(30), TimeSpan.FromHours(1)),
            new(DateTimeOffset.Parse("2026-09-11T22:30:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind), TimeSpan.FromMinutes(30), TimeSpan.FromHours(1)),
        };

        var listed = AvailableDateProjection.Dates(
            starts.Select(start => (start.StartUtc, true)),
            TestData.London,
            selectedDate: default);

        foreach (var date in listed.Select(entry => entry.Date))
        {
            Assert.NotEmpty(BookingFormBuilder.OnDate(starts, TestData.London, date));
        }

        // And nothing is stranded: every start belongs to some listed date.
        Assert.Equal(
            starts.Count,
            listed.Sum(date => BookingFormBuilder.OnDate(starts, TestData.London, date.Date).Count));
    }

    // ---------------------------------------------------------------------
    // The two date controls
    // ---------------------------------------------------------------------

    private static DateOnly? Read(string? listed, string? typed)
    {
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        var query = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>();

        if (listed is not null)
        {
            query[BookingKeys.DateQuery] = listed;
        }

        if (typed is not null)
        {
            query[BookingKeys.OtherDateQuery] = typed;
        }

        context.Request.Query = new Microsoft.AspNetCore.Http.QueryCollection(query);

        return context.Request.ReadDateQuery();
    }

    [Fact]
    public void The_typed_date_wins_over_the_one_chosen_from_the_list()
    {
        // FOUND BY MUTATION, not written first: reversing the precedence left the whole suite
        // green. The rule is a decision — typing is the more deliberate act, and a visitor who
        // picks from the list and then types is correcting themselves — and a decision nothing
        // guards is a comment.
        Assert.Equal(new DateOnly(2026, 12, 1), Read(listed: "2026-09-11", typed: "2026-12-01"));
    }

    [Fact]
    public void The_list_is_used_when_nothing_was_typed()
    {
        Assert.Equal(new DateOnly(2026, 9, 11), Read(listed: "2026-09-11", typed: null));
    }

    [Fact]
    public void An_empty_typed_field_falls_through_to_the_list_rather_than_blanking_it()
    {
        // THE CASE THE WHOLE DESIGN RESTS ON, and it is easy to miss because it looks like
        // nothing. The field is deliberately never repopulated, so every submission after a
        // typed date carries an EMPTY value for it — and if that emptiness did not fall
        // through, the visitor could never use the list again.
        Assert.Equal(new DateOnly(2026, 9, 11), Read(listed: "2026-09-11", typed: string.Empty));
    }

    [Fact]
    public void An_unparseable_typed_date_does_not_discard_the_choice_already_made()
    {
        // A malformed date is not an instruction to forget the date already chosen.
        Assert.Equal(new DateOnly(2026, 9, 11), Read(listed: "2026-09-11", typed: "not-a-date"));
    }

    [Fact]
    public void Neither_control_supplying_a_date_is_no_date()
    {
        Assert.Null(Read(listed: null, typed: null));
    }

    [Fact]
    public void The_two_controls_do_not_share_a_query_parameter()
    {
        // The collision this separation exists to avoid: a form submitting one name from two
        // controls sends BOTH values, and which one binds is an accident of model binding.
        Assert.NotEqual(BookingKeys.DateQuery, BookingKeys.OtherDateQuery);
    }
}
