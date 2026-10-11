using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Placement through the booking service — the production entry point — on dates a closure RANGE
/// or a resource's closed period covers (closure-date-ranges: <c>availability</c>'s
/// <i>Resource closed periods</i> and <c>site-closures</c>' <i>A range costs only the dates a
/// question asks about</i>).
/// </summary>
/// <remarks>
/// <b>Through <see cref="BookingService.PlaceAsync"/>, not through <c>EffectiveWindows</c>.</b> The
/// project's rule for availability is that each route is verified at its own entry point: a test
/// of the configuration and a test of placement can both pass while the two are never joined.
/// </remarks>
public class ClosedPeriodPlacementTests
{
    private static readonly DateOnly Date = TestData.BaseDate;

    private static Resource Room(IEnumerable<SiteClosure>? closures = null, IEnumerable<ClosedPeriod>? periods = null)
        => Resource.Create(
            ResourceTypes.Room,
            "Painted Room",
            directlyBookable: true,
            availability: AvailabilityConfiguration.Create(
                TestData.Weekly("08:00", "18:00", Enum.GetValues<DayOfWeek>()),
                periods ?? [],
                closures: closures).Value,
            id: new Guid("00000000-0000-0000-0000-00000000c10d")).Value;

    private static BookingRequest At(Guid resourceId, DateOnly date) => new()
    {
        ResourceId = resourceId,
        Start = TestData.Utc(date, "10:00"),
        Duration = TimeSpan.FromHours(1),
        Booker = TestData.Booker(),
    };

    [Fact]
    public async Task Placement_on_the_middle_date_of_a_closure_range_is_refused()
    {
        var room = Room(closures: [SiteClosure.CreateRange(Date.AddDays(-3), Date.AddDays(3), "Refurbishment").Value]);
        var (bookings, _, _) = TestData.Services(room);

        var result = await bookings.PlaceAsync(At(room.Id, Date));

        Assert.Equal(FailureCodes.OutsideOpenHours, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public async Task Placement_inside_a_closed_period_is_refused_and_the_day_after_is_not()
    {
        var room = Room(periods: [ClosedPeriod.Create(Date, Date.AddDays(4), "Being painted").Value]);
        var (bookings, _, _) = TestData.Services(room);

        var inside = await bookings.PlaceAsync(At(room.Id, Date.AddDays(2)));
        var after = await bookings.PlaceAsync(At(room.Id, Date.AddDays(5)));

        Assert.Equal(FailureCodes.OutsideOpenHours, Assert.Single(inside.Failures).Code);
        Assert.True(after.Succeeded);
    }

    [Fact]
    public async Task The_refusal_inside_a_closed_period_discloses_nothing_about_it()
    {
        var room = Room(periods: [ClosedPeriod.Create(Date, Date, "Broken leg").Value]);
        var (bookings, _, _) = TestData.Services(room);

        var failure = Assert.Single((await bookings.PlaceAsync(At(room.Id, Date))).Failures);

        Assert.DoesNotContain("leg", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("period", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("period", failure.Field ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_booking_placed_before_a_closed_period_survives_it_and_still_blocks_its_time()
    {
        var open = Room();
        var resources = new InMemoryResourceStore().Add(open);
        var store = new InMemoryBookingStore();
        var time = new FixedTimeProvider(TestData.Now);
        var bookings = new BookingService(resources, store, time, TestData.Settings);
        var availability = new AvailabilityService(resources, store, time, TestData.Settings);

        var placed = await bookings.PlaceAsync(At(open.Id, Date));
        Assert.True(placed.Succeeded);

        // The operator then closes the room for the week — the booking already inside it.
        resources.Add(Room(periods: [ClosedPeriod.Create(Date.AddDays(-1), Date.AddDays(5)).Value]));

        var kept = await store.GetBookingAsync(placed.Value.Id);
        Assert.NotNull(kept);
        Assert.Equal(placed.Value.Status, kept!.Status);
        Assert.Equal(placed.Value.Reference, kept.Reference);

        // Nothing new can be placed that day, the booking's own hour included.
        var again = await bookings.PlaceAsync(At(open.Id, Date));
        Assert.False(again.Succeeded);

        // And the day offers no free time at all.
        var free = await availability.GetFreeTimeAsync(open.Id, Date, Date);
        Assert.True(free.Succeeded);
        Assert.Empty(free.Value);
    }
}
