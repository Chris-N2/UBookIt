using UBookIt.Core.Availability;
using UBookIt.Core.Common;
using UBookIt.Tests.Support;
using static UBookIt.Tests.Support.TestData;

namespace UBookIt.Tests;

/// <summary>
/// Closures as ranges, resources' closed periods, and the precedence ladder that now holds
/// both — all domain rules, so no storage is involved (closure-date-ranges: the
/// <c>site-closures</c> and <c>availability</c> deltas).
/// </summary>
public class ClosureRangeTests
{
    private static readonly DateOnly Monday = new(2027, 3, 1);

    private static readonly DayOfWeek[] Weekdays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    private static SiteClosure Range(DateOnly from, DateOnly to, string label = "Refurbishment", Guid? id = null)
        => SiteClosure.Create(from, label, id, to).Value;

    private static ClosedPeriod Period(DateOnly from, DateOnly to, string? note = null)
        => ClosedPeriod.Create(from, to, note).Value;

    private static AvailabilityConfiguration WeekdaysOpen(
        IEnumerable<DateException>? exceptions = null,
        IEnumerable<SiteClosure>? closures = null,
        IEnumerable<ClosedPeriod>? periods = null)
        => AvailabilityConfiguration.Create(
            Weekly("09:00", "17:00", Weekdays), exceptions, null, closures, periods).Value;

    // ── SiteClosure as a range ────────────────────────────────────────────────────────────

    [Fact]
    public void A_closure_without_an_end_is_the_one_day()
    {
        var closure = SiteClosure.Create(Monday, "Christmas Day").Value;

        Assert.Equal(Monday, closure.Date);
        Assert.Equal(Monday, closure.EndDate);
        Assert.True(closure.Covers(Monday));
        Assert.False(closure.Covers(Monday.AddDays(1)));
    }

    [Fact]
    public void A_range_covers_both_ends_and_everything_between()
    {
        var closure = Range(Monday, Monday.AddDays(11));

        Assert.True(closure.Covers(Monday));
        Assert.True(closure.Covers(Monday.AddDays(5)));
        Assert.True(closure.Covers(Monday.AddDays(11)));
        Assert.False(closure.Covers(Monday.AddDays(-1)));
        Assert.False(closure.Covers(Monday.AddDays(12)));
    }

    [Fact]
    public void An_inverted_range_is_refused_with_its_own_code()
    {
        var result = SiteClosure.Create(Monday, "Backwards", endDate: Monday.AddDays(-1));

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.ClosureRangeInvalid, Assert.Single(result.Failures).Code);
        Assert.Equal("closure-range-invalid", FailureCodes.ClosureRangeInvalid);
    }

    [Fact]
    public void Overlapping_closures_are_refused_with_the_existing_duplicate_code()
    {
        var result = AvailabilityConfiguration.Create(
            WeeklyOpenHours.Empty,
            closures: [Range(Monday, Monday.AddDays(30)), Range(Monday.AddDays(25), Monday.AddDays(40))]);

        Assert.Equal(FailureCodes.DuplicateClosureDate, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void A_closure_swallowed_by_a_longer_earlier_one_is_an_overlap()
    {
        // The second starts after the first's start AND ends before its end: an overlap a
        // neighbour-only comparison of ends would miss if a third sat between them.
        var result = AvailabilityConfiguration.Create(
            WeeklyOpenHours.Empty,
            closures:
            [
                Range(Monday, Monday.AddDays(60)),
                Range(Monday.AddDays(5), Monday.AddDays(6)),
                Range(Monday.AddDays(10), Monday.AddDays(12)),
            ]);

        Assert.Equal(FailureCodes.DuplicateClosureDate, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void Adjacent_closures_are_not_an_overlap()
    {
        var result = AvailabilityConfiguration.Create(
            WeeklyOpenHours.Empty,
            closures: [Range(Monday, Monday.AddDays(4)), Range(Monday.AddDays(5), Monday.AddDays(9))]);

        Assert.True(result.Succeeded);
    }

    // ── Every date in a range is closed ──────────────────────────────────────────────────

    [Fact]
    public void Every_date_in_a_closure_range_is_closed_and_the_days_either_side_are_not()
    {
        var config = WeekdaysOpen(closures: [Range(Monday, Monday.AddDays(11))]);

        for (var date = Monday; date <= Monday.AddDays(11); date = date.AddDays(1))
        {
            Assert.Empty(config.EffectiveWindows(date));
        }

        Assert.NotEmpty(config.EffectiveWindows(Monday.AddDays(-3))); // the Friday before
        Assert.NotEmpty(config.EffectiveWindows(Monday.AddDays(14))); // the Monday after
    }

    [Fact]
    public void A_closure_range_supersedes_an_override_exception_inside_it()
    {
        var inside = Monday.AddDays(2);
        var config = WeekdaysOpen(
            exceptions: [DateException.Override(inside, [Win("10:00", "14:00")]).Value],
            closures: [Range(Monday, Monday.AddDays(4))]);

        Assert.Empty(config.EffectiveWindows(inside));
        Assert.True(config.IsExceptionSuperseded(inside));
    }

    [Fact]
    public void Shortening_a_closure_releases_the_exception_it_no_longer_covers()
    {
        var inside = Monday.AddDays(3);
        var exception = DateException.Override(inside, [Win("10:00", "14:00")]).Value;

        var shortened = WeekdaysOpen(exceptions: [exception], closures: [Range(Monday, Monday.AddDays(1))]);

        Assert.Equal([Win("10:00", "14:00")], shortened.EffectiveWindows(inside));
        Assert.Empty(shortened.EffectiveWindows(Monday));
        Assert.False(shortened.IsExceptionSuperseded(inside));
    }

    // ── Closed periods ───────────────────────────────────────────────────────────────────

    [Fact]
    public void A_closed_period_closes_every_date_it_covers()
    {
        var config = WeekdaysOpen(periods: [Period(Monday, Monday.AddDays(11), "Being painted")]);

        Assert.Empty(config.EffectiveWindows(Monday));
        Assert.Empty(config.EffectiveWindows(Monday.AddDays(11)));
        Assert.NotEmpty(config.EffectiveWindows(Monday.AddDays(14)));
    }

    [Fact]
    public void A_one_day_closed_period_closes_that_day()
    {
        var config = WeekdaysOpen(periods: [Period(Monday, Monday)]);

        Assert.Empty(config.EffectiveWindows(Monday));
        Assert.NotEmpty(config.EffectiveWindows(Monday.AddDays(1)));
    }

    [Fact]
    public void An_inverted_period_is_refused()
    {
        var result = ClosedPeriod.Create(Monday, Monday.AddDays(-1));

        Assert.Equal(FailureCodes.ClosedPeriodInvalid, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void An_over_long_note_is_refused_as_validation()
    {
        var result = ClosedPeriod.Create(Monday, Monday, new string('x', ClosedPeriod.MaxNoteLength + 1));

        Assert.Equal(FailureCodes.ClosedPeriodInvalid, Assert.Single(result.Failures).Code);
        Assert.True(ClosedPeriod.Create(Monday, Monday, new string('x', ClosedPeriod.MaxNoteLength)).Succeeded);
    }

    [Fact]
    public void A_blank_note_is_no_note()
    {
        Assert.Null(Period(Monday, Monday, "   ").Note);
        Assert.Equal("Broken leg", Period(Monday, Monday, "  Broken leg  ").Note);
    }

    [Fact]
    public void Overlapping_periods_are_refused()
    {
        var result = AvailabilityConfiguration.Create(
            WeeklyOpenHours.Empty,
            closedPeriods: [Period(Monday, Monday.AddDays(13)), Period(Monday.AddDays(9), Monday.AddDays(19))]);

        Assert.Equal(FailureCodes.ClosedPeriodOverlap, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void Adjacent_periods_are_not_an_overlap()
    {
        var result = AvailabilityConfiguration.Create(
            WeeklyOpenHours.Empty,
            closedPeriods: [Period(Monday, Monday.AddDays(13)), Period(Monday.AddDays(14), Monday.AddDays(19))]);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void A_period_covering_an_exception_is_refused()
    {
        var result = AvailabilityConfiguration.Create(
            WeeklyOpenHours.Empty,
            exceptions: [DateException.Override(Monday.AddDays(4), [Win("10:00", "14:00")]).Value],
            closedPeriods: [Period(Monday, Monday.AddDays(13))]);

        Assert.Equal(FailureCodes.ClosedPeriodConflictsWithException, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void A_period_covering_a_closure_exception_is_refused_too()
    {
        // Closed either way, but still two rules for one date: the operator decides which stays.
        var result = AvailabilityConfiguration.Create(
            WeeklyOpenHours.Empty,
            exceptions: [DateException.Closure(Monday.AddDays(4))],
            closedPeriods: [Period(Monday, Monday.AddDays(13))]);

        Assert.Equal(FailureCodes.ClosedPeriodConflictsWithException, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void A_site_closure_over_the_same_dates_is_not_a_conflict()
    {
        var config = WeekdaysOpen(
            closures: [Range(Monday.AddDays(9), Monday.AddDays(19))],
            periods: [Period(Monday, Monday.AddDays(13))]);

        for (var date = Monday; date <= Monday.AddDays(19); date = date.AddDays(1))
        {
            Assert.Empty(config.EffectiveWindows(date));
        }
    }

    [Fact]
    public void Opting_out_of_a_closure_reveals_the_resources_own_period()
    {
        // The closure is absent from the configuration, which is what an opt-out means.
        var config = WeekdaysOpen(periods: [Period(Monday.AddDays(2), Monday.AddDays(3))]);

        Assert.NotEmpty(config.EffectiveWindows(Monday));
        Assert.Empty(config.EffectiveWindows(Monday.AddDays(2)));
        Assert.Empty(config.EffectiveWindows(Monday.AddDays(3)));
        Assert.NotEmpty(config.EffectiveWindows(Monday.AddDays(4)));
    }

    [Fact]
    public void The_new_codes_have_their_published_spelling()
    {
        Assert.Equal("closed-period-invalid", FailureCodes.ClosedPeriodInvalid);
        Assert.Equal("closed-period-overlap", FailureCodes.ClosedPeriodOverlap);
        Assert.Equal("closed-period-conflicts-with-exception", FailureCodes.ClosedPeriodConflictsWithException);
    }

    // ── Cost does not grow with a range's length ─────────────────────────────────────────

    [Fact]
    public void A_ten_year_closure_costs_no_more_to_build_and_read_than_a_one_day_one()
    {
        // Measured as bytes allocated on this thread: expanding a ten-year range per date would
        // allocate thousands of entries, and a scan allocates nothing per date.
        static long Allocated(SiteClosure closure)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var config = AvailabilityConfiguration.Create(
                Weekly("09:00", "17:00", Weekdays), closures: [closure]).Value;
            for (var date = Monday; date < Monday.AddDays(7); date = date.AddDays(1))
            {
                _ = config.EffectiveWindows(date);
            }

            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        _ = Allocated(Range(Monday, Monday)); // warm up
        var oneDay = Allocated(Range(Monday, Monday));
        var tenYears = Allocated(Range(Monday, Monday.AddYears(10)));

        // A per-date expansion of ten years would be tens of kilobytes; allow small noise only.
        Assert.True(tenYears - oneDay < 1024, $"one day: {oneDay} bytes; ten years: {tenYears} bytes");
    }

    // ── ClosedRanges.FirstUncovered ──────────────────────────────────────────────────────

    [Fact]
    public void An_uncovered_day_is_its_own_first_uncovered_date()
        => Assert.Equal(Monday, ClosedRanges.FirstUncovered(Monday, [(Monday.AddDays(1), Monday.AddDays(3))]));

    [Fact]
    public void A_range_covering_the_day_moves_to_the_day_after_it_ends()
        => Assert.Equal(
            Monday.AddDays(10),
            ClosedRanges.FirstUncovered(Monday, [(Monday.AddDays(-5), Monday.AddDays(9))]));

    [Fact]
    public void Adjoining_ranges_are_followed()
        => Assert.Equal(
            Monday.AddDays(10),
            ClosedRanges.FirstUncovered(Monday, [(Monday.AddDays(5), Monday.AddDays(9)), (Monday, Monday.AddDays(4))]));

    [Fact]
    public void Overlapping_ranges_are_followed()
        => Assert.Equal(
            Monday.AddDays(13),
            ClosedRanges.FirstUncovered(Monday, [(Monday, Monday.AddDays(7)), (Monday.AddDays(3), Monday.AddDays(12))]));

    [Fact]
    public void A_one_day_gap_stops_the_walk()
        => Assert.Equal(
            Monday.AddDays(5),
            ClosedRanges.FirstUncovered(Monday, [(Monday, Monday.AddDays(4)), (Monday.AddDays(6), Monday.AddDays(9))]));

    [Fact]
    public void A_range_ending_on_the_last_date_does_not_overflow()
        => Assert.Equal(
            DateOnly.MaxValue,
            ClosedRanges.FirstUncovered(Monday, [(Monday, DateOnly.MaxValue)]));
}
