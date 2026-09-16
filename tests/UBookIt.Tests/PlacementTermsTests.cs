using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The terms the two policy rules of the placement pipeline are evaluated on (bookings spec,
/// "Moving a booking": lead time as zero, no horizon, for an operator; the resource's own for a
/// visitor).
/// <para>
/// <b>These tests reach the rules through the internal terms overload</b>, so they exercise
/// exactly the code both the visitor path and every operator operation run — not a copy of it.
/// A mutant that hard-coded operator terms inside the rule bodies fails the visitor tests; one
/// that hard-coded visitor terms fails the operator tests.
/// </para>
/// </summary>
public class PlacementTermsTests
{
    private static readonly DateOnly Date = TestData.BaseDate;

    /// <summary>A room needing a day's notice, bookable at most 90 days out.</summary>
    private static UBookIt.Core.Resources.Resource StrictRoom() => TestData.Room(TestData.Config(
        TestData.Weekly("08:00", "18:00", Date.DayOfWeek),
        constraints: BookingConstraints.Create(leadTime: TimeSpan.FromHours(24), horizonDays: 90).Value));

    [Fact]
    public void Visitor_terms_are_the_resource_constraints()
    {
        var room = StrictRoom();

        var terms = PlacementTerms.Visitor(room.Availability.Constraints);

        Assert.Equal(TimeSpan.FromHours(24), terms.LeadTime);
        Assert.Equal(90, terms.HorizonDays);
    }

    [Fact]
    public void Operator_terms_are_zero_lead_and_no_horizon()
    {
        var terms = PlacementTerms.Operator;

        Assert.Equal(TimeSpan.Zero, terms.LeadTime);
        Assert.Null(terms.HorizonDays);
    }

    [Fact]
    public void Visitor_inside_lead_time_is_refused_and_operator_is_not()
    {
        var room = StrictRoom();
        // "Now" is 09:00 on the booking date; the start is one hour later.
        var now = TestData.Utc(Date, "09:00");
        var (bookings, _, _) = TestData.Services(room, now);
        var start = TestData.Utc(Date, "10:00");

        var visitor = bookings.CheckPlacementRules(room, start, TimeSpan.FromHours(1));
        var op = bookings.CheckPlacementRules(room, start, TimeSpan.FromHours(1), PlacementTerms.Operator);

        Assert.False(visitor.Succeeded);
        Assert.Equal(FailureCodes.LeadTime, Assert.Single(visitor.Failures).Code);
        Assert.True(op.Succeeded);
    }

    [Fact]
    public void A_start_in_the_past_is_refused_under_operator_terms_with_the_lead_time_code()
    {
        var room = StrictRoom();
        var now = TestData.Utc(Date, "12:00");
        var (bookings, _, _) = TestData.Services(room, now);
        var start = TestData.Utc(Date, "10:00");

        var op = bookings.CheckPlacementRules(room, start, TimeSpan.FromHours(1), PlacementTerms.Operator);

        Assert.False(op.Succeeded);
        var failure = Assert.Single(op.Failures);
        Assert.Equal(FailureCodes.LeadTime, failure.Code);
        Assert.Contains("already passed", failure.Message);
    }

    [Fact]
    public void A_start_exactly_now_is_not_in_the_past()
    {
        var room = StrictRoom();
        var now = TestData.Utc(Date, "10:00");
        var (bookings, _, _) = TestData.Services(room, now);

        var op = bookings.CheckPlacementRules(room, now, TimeSpan.FromHours(1), PlacementTerms.Operator);

        Assert.True(op.Succeeded);
    }

    [Fact]
    public void Visitor_beyond_horizon_is_refused_and_operator_is_not()
    {
        var room = StrictRoom();
        var (bookings, _, _) = TestData.Services(room);
        var farDate = Date.AddDays(7 * 20); // same weekday, ~140 days out, past the 90-day horizon
        var start = TestData.Utc(farDate, "10:00");

        var visitor = bookings.CheckPlacementRules(room, start, TimeSpan.FromHours(1));
        var op = bookings.CheckPlacementRules(room, start, TimeSpan.FromHours(1), PlacementTerms.Operator);

        Assert.False(visitor.Succeeded);
        Assert.Equal(FailureCodes.Horizon, Assert.Single(visitor.Failures).Code);
        Assert.True(op.Succeeded);
    }

    [Fact]
    public void Operator_terms_do_not_relax_open_hours_or_granularity()
    {
        var room = StrictRoom();
        var (bookings, _, _) = TestData.Services(room);

        var outside = bookings.CheckPlacementRules(
            room, TestData.Utc(Date, "07:00"), TimeSpan.FromHours(1), PlacementTerms.Operator);
        var offGrid = bookings.CheckPlacementRules(
            room, TestData.Utc(Date, "10:07"), TimeSpan.FromHours(1), PlacementTerms.Operator);

        Assert.Equal(FailureCodes.OutsideOpenHours, Assert.Single(outside.Failures).Code);
        Assert.Equal(FailureCodes.Granularity, Assert.Single(offGrid.Failures).Code);
    }

    [Fact]
    public void The_public_check_is_the_visitor_check()
    {
        var room = StrictRoom();
        var now = TestData.Utc(Date, "09:00");
        var (bookings, _, _) = TestData.Services(room, now);
        var start = TestData.Utc(Date, "10:00");

        var viaPublic = bookings.CheckPlacementRules(room, start, TimeSpan.FromHours(1));
        var viaTerms = bookings.CheckPlacementRules(
            room, start, TimeSpan.FromHours(1), PlacementTerms.Visitor(room.Availability.Constraints));

        Assert.Equal(viaTerms.Succeeded, viaPublic.Succeeded);
        Assert.Equal(
            viaTerms.Failures.Select(f => f.Code),
            viaPublic.Failures.Select(f => f.Code));
    }
}
