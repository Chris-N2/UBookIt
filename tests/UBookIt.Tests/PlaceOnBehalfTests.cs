using System.Reflection;
using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Stores;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// An operator records a booking somebody made by telephone or at a desk (bookings spec,
/// "Placing a booking on a booker's behalf"; "Booking status machine").
/// <para>
/// <b>Every test here books through the production entry point</b>, never through the rule
/// check in isolation: the three things this change gets wrong if it gets anything wrong —
/// which terms reach the pipeline, which guard the path does not pass, and which status the
/// booking lands in — are all properties of the path rather than of the rules.
/// </para>
/// <para>
/// <b>The resources withhold direct booking wherever the waiver is the point.</b> The shared
/// <c>TestData.Room()</c> grants it, so a guard on the wrong overload would go unnoticed
/// against it — the same reasoning <c>DirectBookingTests</c> records.
/// </para>
/// </summary>
public class PlaceOnBehalfTests
{
    private static readonly DateOnly Date = TestData.BaseDate;

    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:x12}");

    /// <summary>
    /// A room open 08:00–18:00 on the fixture's day, needing a day's notice and bookable at
    /// most 90 days out, stating direct bookability explicitly — never defaulted, because
    /// which answer was given is what several of these tests are about.
    /// </summary>
    private static Resource Room(bool directlyBookable, int id = 1) => Resource.Create(
        ResourceTypes.Room,
        $"Room {id}",
        directlyBookable: directlyBookable,
        availability: TestData.Config(
            TestData.Weekly("08:00", "18:00", Date.DayOfWeek),
            constraints: BookingConstraints.Create(
                granularity: TimeSpan.FromMinutes(30),
                minDuration: TimeSpan.FromMinutes(60),
                maxDuration: TimeSpan.FromMinutes(240),
                leadTime: TimeSpan.FromHours(24),
                horizonDays: 90).Value),
        id: Id(id)).Value;

    private static (BookingService Bookings, InMemoryBookingStore Store) Wire(
        Resource resource, DateTimeOffset? nowUtc = null, bool autoConfirm = true)
    {
        var resources = new InMemoryResourceStore().Add(resource);
        var store = new InMemoryBookingStore();
        var time = new FixedTimeProvider(nowUtc ?? TestData.Now);
        var settings = new SiteBookingSettings
        {
            TimeZoneId = TestData.LondonZoneId,
            AutoConfirm = autoConfirm,
        };

        return (new BookingService(resources, store, time, settings), store);
    }

    private static BookingRequest Request(
        Resource resource, DateOnly? date = null, string start = "10:00", int minutes = 60)
        => new()
        {
            ResourceId = resource.Id,
            Start = TestData.Utc(date ?? Date, start),
            Duration = TimeSpan.FromMinutes(minutes),
            Booker = TestData.Booker(),
        };

    // ------------------------------------------------------------------
    // The terms themselves
    // ------------------------------------------------------------------

    [Fact]
    public void Visitor_terms_apply_approval_and_operator_terms_do_not()
    {
        var room = Room(directlyBookable: true);

        Assert.True(PlacementTerms.Visitor(room.Availability.Constraints).ApprovalApplies);
        Assert.False(PlacementTerms.Operator.ApprovalApplies);
    }

    [Fact]
    public void Spec_scenario_the_waiver_is_not_a_member_of_the_operators_terms()
    {
        // bookings spec, "Booking a single resource requires that resource to permit it":
        // direct bookability is decided by which entry point was called, never evaluated on
        // these terms. A member here would state the rule in a value and enforce it in an
        // overload — two places, free to disagree.
        //
        // Asserted over the whole public surface rather than by naming one forbidden member,
        // because the member somebody adds will be called `AllowsDirectBooking` or
        // `BypassesBookability` or `IsOperator`, not whatever a list here guessed.
        var names = typeof(PlacementTerms)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Select(p => p.Name)
            .ToList();

        Assert.Equal(
            new[] { "ApprovalApplies", "HorizonDays", "LeadTime", "Operator" },
            names.Order(StringComparer.Ordinal));
    }

    // ------------------------------------------------------------------
    // The waiver that is structural, not a flag
    // ------------------------------------------------------------------

    [Fact]
    public async Task Spec_scenario_an_operator_books_a_withholding_resource_directly()
    {
        var room = Room(directlyBookable: false);
        var (bookings, store) = Wire(room);

        var result = await bookings.PlaceOnBehalfAsync(Request(room));

        Assert.True(result.Succeeded);
        Assert.Equal(room.Id, Assert.Single(result.Value.Claims).ResourceId);
        Assert.Single(await store.GetClaimsAsync(
            [room.Id], TestData.Utc(Date, "00:00"), TestData.Utc(Date.AddDays(1), "00:00")));
    }

    [Fact]
    public async Task Spec_scenario_the_visitor_path_is_unchanged_by_the_operator_path_existing()
    {
        var room = Room(directlyBookable: false);
        var (bookings, store) = Wire(room);

        var visitor = await bookings.PlaceAsync(Request(room));

        Assert.False(visitor.Succeeded);
        Assert.Equal(
            FailureCodes.ResourceNotDirectlyBookable,
            Assert.Single(visitor.Failures).Code);
        Assert.Empty(await store.GetClaimsAsync(
            [room.Id], TestData.Utc(Date, "00:00"), TestData.Utc(Date.AddDays(1), "00:00")));
    }

    [Fact]
    public async Task A_resource_that_permits_direct_booking_is_placeable_both_ways()
    {
        // The waiver must not be the only thing the operator path can do: a granting resource
        // behaves identically down both, which is what makes the difference above specific to
        // the permission rather than to the path.
        var room = Room(directlyBookable: true);
        var (bookings, _) = Wire(room);

        var operatorResult = await bookings.PlaceOnBehalfAsync(Request(room, start: "10:00"));
        var visitorResult = await bookings.PlaceAsync(Request(room, start: "12:00"));

        Assert.True(operatorResult.Succeeded);
        Assert.True(visitorResult.Succeeded);
    }

    // ------------------------------------------------------------------
    // Which rules an operator is exempt from, and which bind
    // ------------------------------------------------------------------

    [Fact]
    public async Task Spec_scenario_placement_inside_the_resources_lead_time_succeeds()
    {
        var room = Room(directlyBookable: true);
        // "Now" is 09:00 on the booking date; the start is one hour later, against 24h notice.
        var (bookings, _) = Wire(room, nowUtc: TestData.Utc(Date, "09:00"));

        var result = await bookings.PlaceOnBehalfAsync(Request(room, start: "10:00"));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Spec_scenario_placement_in_the_past_is_refused()
    {
        var room = Room(directlyBookable: true);
        var (bookings, store) = Wire(room, nowUtc: TestData.Utc(Date, "12:00"));

        var result = await bookings.PlaceOnBehalfAsync(Request(room, start: "10:00"));

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.LeadTime, Assert.Single(result.Failures).Code);
        Assert.Empty(await store.GetClaimsAsync(
            [room.Id], TestData.Utc(Date, "00:00"), TestData.Utc(Date.AddDays(1), "00:00")));
    }

    [Fact]
    public async Task Spec_scenario_placement_beyond_the_horizon_succeeds()
    {
        var room = Room(directlyBookable: true);
        var (bookings, _) = Wire(room);
        // Same weekday, ~140 days out, past the resource's 90-day horizon.
        var far = Date.AddDays(7 * 20);

        var result = await bookings.PlaceOnBehalfAsync(Request(room, date: far));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Spec_scenario_open_hours_still_bind_an_operator()
    {
        var room = Room(directlyBookable: true);
        var (bookings, _) = Wire(room);

        var result = await bookings.PlaceOnBehalfAsync(Request(room, start: "07:00"));

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.OutsideOpenHours, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public async Task Spec_scenario_a_conflict_still_binds_an_operator()
    {
        var room = Room(directlyBookable: true);
        var (bookings, _) = Wire(room);

        var first = await bookings.PlaceOnBehalfAsync(Request(room, start: "10:00"));
        var second = await bookings.PlaceOnBehalfAsync(Request(room, start: "10:00"));

        Assert.True(first.Succeeded);
        Assert.False(second.Succeeded);
        Assert.Equal(FailureCodes.Conflict, Assert.Single(second.Failures).Code);
    }

    [Fact]
    public async Task Spec_scenario_duration_bounds_and_granularity_still_bind_an_operator()
    {
        var room = Room(directlyBookable: true);
        var (bookings, _) = Wire(room);

        // Each length isolates ONE rule: 30 is on the 30-minute grid and below the 60-minute
        // floor, 300 is on the grid and above the 240 ceiling. A length that broke two rules
        // at once would pass this test while the pipeline reported either of them.
        var tooShort = await bookings.PlaceOnBehalfAsync(Request(room, minutes: 30));
        var tooLong = await bookings.PlaceOnBehalfAsync(Request(room, minutes: 300));
        var offGrid = await bookings.PlaceOnBehalfAsync(Request(room, start: "10:07"));

        Assert.Equal(FailureCodes.DurationTooShort, Assert.Single(tooShort.Failures).Code);
        Assert.Equal(FailureCodes.DurationTooLong, Assert.Single(tooLong.Failures).Code);
        Assert.Equal(FailureCodes.Granularity, Assert.Single(offGrid.Failures).Code);
    }

    // ------------------------------------------------------------------
    // The status an operator's booking lands in
    // ------------------------------------------------------------------

    [Fact]
    public async Task Spec_scenario_an_operators_placement_under_approval_is_confirmed()
    {
        var room = Room(directlyBookable: true);
        var (bookings, _) = Wire(room, autoConfirm: false);

        var result = await bookings.PlaceOnBehalfAsync(Request(room));

        Assert.True(result.Succeeded);
        Assert.Equal(BookingStatus.Confirmed, result.Value.Status);
    }

    [Fact]
    public async Task Spec_scenario_an_operators_placement_under_auto_confirm_is_confirmed_too()
    {
        var room = Room(directlyBookable: true);
        var (bookings, _) = Wire(room, autoConfirm: true);

        var result = await bookings.PlaceOnBehalfAsync(Request(room));

        Assert.Equal(BookingStatus.Confirmed, result.Value.Status);
    }

    [Fact]
    public async Task Spec_scenario_a_visitors_booking_is_unaffected_by_that_decision()
    {
        // The mutant this catches: waiving approval unconditionally rather than on the terms.
        var room = Room(directlyBookable: true);
        var (offSite, _) = Wire(room, autoConfirm: false);
        var (onSite, _) = Wire(room, autoConfirm: true);

        var underApproval = await offSite.PlaceAsync(Request(room));
        var underAutoConfirm = await onSite.PlaceAsync(Request(room));

        Assert.Equal(BookingStatus.Requested, underApproval.Value.Status);
        Assert.Equal(BookingStatus.Confirmed, underAutoConfirm.Value.Status);
    }

    // ------------------------------------------------------------------
    // The booker
    // ------------------------------------------------------------------

    [Fact]
    public void Spec_scenario_a_booker_without_an_email_is_refused()
    {
        // Refused where a visitor's is refused — at the booker's own construction, which is the
        // only pathway either placement has to one. Placement cannot be reached with a booker
        // that does not exist, which is the guarantee: operator placement creates no booker
        // state a visitor's could not.
        Assert.False(Booker.Create(null, "Test Person", "not-an-address").Succeeded);
        Assert.False(Booker.Create(null, "Test Person", "").Succeeded);
        Assert.False(Booker.Create(null, "", "test@example.com").Succeeded);
    }

    [Fact]
    public async Task Spec_scenario_the_operators_booking_carries_no_member_key()
    {
        var room = Room(directlyBookable: true);
        var (bookings, _) = Wire(room);

        var result = await bookings.PlaceOnBehalfAsync(Request(room));

        Assert.Null(result.Value.Booker.MemberKey);
        Assert.NotNull(result.Value.Booker.Contact);
    }

    [Fact]
    public async Task Spec_scenario_an_operators_booking_behaves_as_any_other_afterwards()
    {
        var room = Room(directlyBookable: false);
        var (bookings, _) = Wire(room);

        var placed = await bookings.PlaceOnBehalfAsync(Request(room));
        var moved = await bookings.MoveAsync(
            placed.Value.Id, TestData.Utc(Date, "14:00"), TimeSpan.FromMinutes(60));
        var cancelled = await bookings.CancelAsync(placed.Value.Id);

        Assert.True(moved.Succeeded);
        Assert.True(cancelled.Succeeded);
        Assert.Equal(BookingStatus.Cancelled, cancelled.Value.Status);
    }

    [Fact]
    public async Task An_operators_booking_carries_a_reference_drawn_the_same_way()
    {
        var room = Room(directlyBookable: true);
        var (bookings, _) = Wire(room);

        var operatorBooking = await bookings.PlaceOnBehalfAsync(Request(room, start: "10:00"));
        var visitorBooking = await bookings.PlaceAsync(Request(room, start: "12:00"));

        Assert.NotEqual(default, operatorBooking.Value.Reference);
        Assert.NotEqual(operatorBooking.Value.Reference, visitorBooking.Value.Reference);
    }
}
