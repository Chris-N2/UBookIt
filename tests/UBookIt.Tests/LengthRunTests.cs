using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The anchoring invariant: a <see cref="LengthRun"/> denotes exactly the
/// multiples of its step inside its range, so its minimum is always a multiple
/// of its step (service-booking spec, "Every bookable length run is anchored at
/// its step").
/// <para>
/// Two algorithms depend on it and neither would report its violation: subset
/// elimination compares two equal-step runs assuming they are in phase, and
/// intersection across roles takes the least common multiple of two steps on
/// the same assumption. Both were relying on the producers happening to floor
/// their bounds, which is why the invariant is now enforced where runs are
/// built rather than assumed where they are read.
/// </para>
/// </summary>
public class LengthRunTests
{
    private static readonly DateOnly Date = TestData.BaseDate;

    private static TimeSpan Mins(int minutes) => TimeSpan.FromMinutes(minutes);

    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:x12}");

    private static Resource Room(
        int id, int granularity, int min, int max, string open = "09:00", string close = "17:00")
        => Resource.Create(
            ResourceTypes.Room,
            $"Resource {id}",
            // Offered for direct booking. The domain default is the opposite;
            // these fixtures stand for ordinary bookable resources, and the
            // permission itself is exercised explicitly in DirectBookingTests.
            directlyBookable: true,
            availability: TestData.Config(
                TestData.Weekly(open, close, Date.DayOfWeek),
                constraints: BookingConstraints.Create(
                    granularity: Mins(granularity),
                    minDuration: Mins(min),
                    maxDuration: Mins(max)).Value),
            id: Id(id)).Value;

    [Fact]
    public void An_out_of_phase_run_cannot_be_constructed()
    {
        // 5, 15, 10 was constructible before this invariant was enforced, and
        // `Admits` even honoured its phase — which is precisely how elimination
        // and intersection could disagree with it.
        var thrown = Assert.Throws<ArgumentException>(() => new LengthRun(Mins(5), Mins(15), Mins(10)));

        Assert.Equal("min", thrown.ParamName);
    }

    [Theory]
    [InlineData(30, 120, 30)]
    [InlineData(60, 60, 20)]
    [InlineData(0, 90, 45)]
    public void An_anchored_run_is_accepted(int min, int max, int step)
    {
        var run = new LengthRun(Mins(min), Mins(max), Mins(step));

        Assert.Equal(Mins(min), run.Min);
        Assert.Equal(0, run.Min.Ticks % run.Step.Ticks);
    }

    [Fact]
    public void An_unreachable_maximum_is_rejected()
    {
        // The other half of the same claim: the union-availability requirement
        // says Max is a multiple of Step "so Max is always reachable". A run
        // ending at 100 on a 30-minute grid tops out at 90 and would report a
        // maximum nobody can book.
        var thrown = Assert.Throws<ArgumentException>(() => new LengthRun(Mins(30), Mins(100), Mins(30)));

        Assert.Equal("max", thrown.ParamName);
    }

    [Fact]
    public void A_non_positive_step_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LengthRun(Mins(30), Mins(60), TimeSpan.Zero));
    }

    [Fact]
    public void An_inverted_range_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LengthRun(Mins(60), Mins(30), Mins(30)));
    }

    // ------------------------------------------------------------- intersection

    [Fact]
    public async Task Every_run_produced_by_availability_projection_is_anchored()
    {
        // Bounds deliberately off every grid in the pool (25 and 155 are
        // multiples of none of 20, 30, 45), grids chosen pairwise coprime-ish,
        // and one candidate booked so free time truncates a run rather than the
        // configuration alone deciding it. Any of those is a way for a producer
        // to emit a minimum that is not a multiple of its step.
        var service = Service.Create(
            "Consultation",
            ServiceDuration.Variable(Mins(25), Mins(155)).Value,
            [new ServiceRole(ResourceTypes.Room, 1)]).Value;

        var resources = new InMemoryResourceStore()
            .Add(Room(1, granularity: 20, min: 20, max: 480))
            .Add(Room(2, granularity: 30, min: 30, max: 480))
            .Add(Room(3, granularity: 45, min: 45, max: 450));

        var services = new InMemoryServiceStore().Add(service);
        var booking = TestData.ServiceBooking(services, resources);

        var starts = await booking.GetBookableStartsAsync(service.Id, Date, Date);

        Assert.True(starts.Succeeded);
        Assert.NotEmpty(starts.Value);

        foreach (var start in starts.Value)
        {
            Assert.NotEmpty(start.Runs);
            Assert.All(start.Runs, run => Assert.Equal(0, run.Min.Ticks % run.Step.Ticks));
        }
    }

    [Fact]
    public async Task Every_range_produced_by_duration_resolution_is_anchored()
    {
        // The other producer: a service's bounds narrowed by a resource's own
        // range. Both ends must land on the resource's grid, or the run built
        // from them at a start would be out of phase.
        var service = Service.Create(
            "Consultation",
            ServiceDuration.Variable(Mins(25), Mins(155)).Value,
            [new ServiceRole(ResourceTypes.Room, 1)]).Value;

        var resources = new InMemoryResourceStore()
            .Add(Room(1, granularity: 20, min: 20, max: 480))
            .Add(Room(2, granularity: 30, min: 30, max: 480))
            .Add(Room(3, granularity: 45, min: 45, max: 450));

        var services = new InMemoryServiceStore().Add(service);
        var booking = TestData.ServiceBooking(services, resources);

        var candidates = (await booking.ResolveCandidatesAsync(service.Id)).SingleRolePool();

        Assert.Equal(3, candidates.Count);
        Assert.All(candidates, candidate =>
        {
            Assert.Equal(0, candidate.Range.Min.Ticks % candidate.Granularity.Ticks);
            Assert.Equal(0, candidate.Range.Max.Ticks % candidate.Granularity.Ticks);
        });
    }

    [Fact]
    public async Task A_truncated_run_is_still_anchored()
    {
        // Free time cut by an existing booking is the case where a producer
        // computes a maximum from something other than the configuration.
        var service = Service.Create(
            "Consultation",
            ServiceDuration.Variable(Mins(25), null).Value,
            [new ServiceRole(ResourceTypes.Room, 1)]).Value;

        var room = Room(1, granularity: 45, min: 45, max: 450);
        var resources = new InMemoryResourceStore().Add(room);
        var services = new InMemoryServiceStore().Add(service);

        var bookingStore = new InMemoryBookingStore();
        var time = new FixedTimeProvider(TestData.Now);
        var availability = new AvailabilityService(resources, bookingStore, time, TestData.Settings);
        var bookings = new BookingService(resources, bookingStore, time, TestData.Settings);
        var serviceBooking = new ServiceBookingService(
            services, resources, bookingStore, availability, bookings, TestData.Settings);

        var placed = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = room.Id,
            Start = TestData.Utc(Date, "11:15"),
            Duration = Mins(45),
            Booker = TestData.Booker(),
        });

        Assert.True(placed.Succeeded);

        var starts = await serviceBooking.GetBookableStartsAsync(service.Id, Date, Date);

        Assert.NotEmpty(starts.Value);
        Assert.All(
            starts.Value.SelectMany(s => s.Runs),
            run => Assert.Equal(0, run.Min.Ticks % run.Step.Ticks));
    }
}
