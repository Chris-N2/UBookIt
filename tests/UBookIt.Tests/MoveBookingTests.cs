using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Core.Stores;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Moving a booking (bookings spec: "Moving a booking", "Atomic move contract", and the move
/// additions to "Placement and status changes are observable"), exercised through the real
/// service over the in-memory store.
/// <para>
/// <b>Every "between read and write" scenario is staged through a store wrapper</b>, because
/// the service reads the booking, decides, then calls the store's move — and the guarantees
/// under test are precisely about what happens when the world changes in between. The
/// wrapper does the concurrent thing at the one moment it matters, deterministically, rather
/// than hoping a race lands there.
/// </para>
/// </summary>
public class MoveBookingTests
{
    private const string Therapist = "therapist";

    private static readonly DateOnly Date = TestData.BaseDate;

    private static TimeSpan Mins(int minutes) => TimeSpan.FromMinutes(minutes);

    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:x12}");

    /// <summary>A resource open 08:00–18:00 on the fixture's weekday, half-hour grid.</summary>
    private static Resource Res(
        int id, string type = ResourceTypes.Room, TimeSpan? leadTime = null, int? horizonDays = null)
        => Resource.Create(
            type,
            $"Resource {id}",
            directlyBookable: true,
            availability: TestData.Config(
                TestData.Weekly("08:00", "18:00", Date.DayOfWeek),
                constraints: BookingConstraints.Create(
                    granularity: Mins(30),
                    minDuration: Mins(30),
                    maxDuration: Mins(480),
                    leadTime: leadTime,
                    horizonDays: horizonDays).Value),
            id: Id(id)).Value;

    private sealed class Harness
    {
        public required BookingService Bookings { get; init; }

        public required ServiceBookingService Services { get; init; }

        public required InMemoryBookingStore Store { get; init; }

        public required RecordingObserver Observer { get; init; }
    }

    /// <summary>Records every report, and — for moves — the previous interval and the store's state when told.</summary>
    private sealed class RecordingObserver(InMemoryBookingStore store) : IBookingObserver
    {
        public List<string> Told { get; } = [];

        public List<(Guid BookingId, BookingInterval Previous, BookingInterval StoredWhenTold, int MovesWhenTold)> Moves { get; } = [];

        public Task BookingPlacedAsync(Booking booking, CancellationToken cancellationToken = default)
            => Record("placed");

        public Task BookingConfirmedAsync(Booking booking, CancellationToken cancellationToken = default)
            => Record("confirmed");

        public Task BookingDeclinedAsync(Booking booking, CancellationToken cancellationToken = default)
            => Record("declined");

        public Task BookingCancelledAsync(Booking booking, CancellationToken cancellationToken = default)
            => Record("cancelled");

        public async Task BookingMovedAsync(
            Booking booking, BookingInterval previousInterval, CancellationToken cancellationToken = default)
        {
            var stored = await store.GetBookingAsync(booking.Id, cancellationToken);
            Moves.Add((booking.Id, previousInterval, stored!.Interval, store.MoveCount));
            await Record("moved");
        }

        private Task Record(string what)
        {
            Told.Add(what);
            return Task.CompletedTask;
        }
    }

    private static Harness Wire(
        DateTimeOffset? now = null,
        bool autoConfirm = true,
        Service? service = null,
        Func<InMemoryBookingStore, IBookingStore>? wrapStore = null,
        IBookingObserver? observer = null,
        params Resource[] resources)
    {
        var resourceStore = new InMemoryResourceStore();
        foreach (var resource in resources.Length == 0 ? [Res(1)] : resources)
        {
            resourceStore.Add(resource);
        }

        var serviceStore = new InMemoryServiceStore();
        if (service is not null)
        {
            serviceStore.Add(service);
        }

        var memory = new InMemoryBookingStore();
        IBookingStore store = wrapStore?.Invoke(memory) ?? memory;
        var time = new FixedTimeProvider(now ?? TestData.Now);
        var settings = TestData.Settings with { AutoConfirm = autoConfirm };
        var recorder = new RecordingObserver(memory);
        var bookings = new BookingService(resourceStore, store, time, settings, observer ?? recorder);
        var availability = new AvailabilityService(resourceStore, store, time, settings);

        return new Harness
        {
            Bookings = bookings,
            Services = new ServiceBookingService(serviceStore, resourceStore, store, availability, bookings, settings),
            Store = memory,
            Observer = recorder,
        };
    }

    private static async Task<Booking> Place(Harness h, string start = "10:00", int minutes = 60, Guid? resourceId = null)
    {
        var placed = await h.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = resourceId ?? Id(1),
            Start = TestData.Utc(Date, start),
            Duration = Mins(minutes),
            Booker = TestData.Booker(),
        });
        Assert.True(placed.Succeeded, string.Join(", ", placed.Failures.Select(f => f.Code)));
        return placed.Value;
    }

    private static Task<DomainResult<Booking>> Move(Harness h, Guid id, string start, int minutes = 60)
        => h.Bookings.MoveAsync(id, TestData.Utc(Date, start), Mins(minutes));

    private static void AssertSingleFailure(DomainResult<Booking> result, string code)
    {
        Assert.False(result.Succeeded);
        Assert.Equal(code, Assert.Single(result.Failures).Code);
    }

    // ---------------------------------------------------------------------------------------
    // 2.1 — the aggregate
    // ---------------------------------------------------------------------------------------

    private static Booking Rehydrated(BookingStatus status)
        => Booking.Rehydrate(
            Guid.NewGuid(),
            BookingReference.TryParse("BCDF2345", out var reference) ? reference : throw new InvalidOperationException(),
            BookingInterval.Create(TestData.Utc(Date, "10:00"), TestData.Utc(Date, "11:00"), TestData.LondonZoneId).Value,
            TestData.Booker(),
            [new ResourceClaim(Id(1)), new ResourceClaim(Id(3))],
            status,
            TestData.Now,
            new ServiceAttribution(Guid.NewGuid(), "Massage")).Value;

    [Theory]
    [InlineData(BookingStatus.Requested)]
    [InlineData(BookingStatus.Confirmed)]
    public void MoveTo_changes_the_interval_and_nothing_else(BookingStatus status)
    {
        var booking = Rehydrated(status);
        var before = (booking.Id, booking.Reference, booking.Booker, booking.Status, booking.CreatedUtc, booking.Service, Claims: booking.Claims.ToList());
        var target = BookingInterval.Create(TestData.Utc(Date, "14:00"), TestData.Utc(Date, "15:30"), TestData.LondonZoneId).Value;

        var result = booking.MoveTo(target);

        Assert.True(result.Succeeded);
        Assert.Equal(target, booking.Interval);
        Assert.Equal(before.Id, booking.Id);
        Assert.Equal(before.Reference, booking.Reference);
        Assert.Same(before.Booker, booking.Booker);
        Assert.Equal(before.Status, booking.Status);
        Assert.Equal(before.CreatedUtc, booking.CreatedUtc);
        Assert.Equal(before.Service, booking.Service);
        Assert.Equal(before.Claims, booking.Claims);
    }

    [Theory]
    [InlineData(BookingStatus.Declined)]
    [InlineData(BookingStatus.Cancelled)]
    public void MoveTo_is_refused_from_a_status_that_holds_no_time(BookingStatus status)
    {
        var booking = Rehydrated(status);
        var original = booking.Interval;
        var target = BookingInterval.Create(TestData.Utc(Date, "14:00"), TestData.Utc(Date, "15:00"), TestData.LondonZoneId).Value;

        var result = booking.MoveTo(target);

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.InvalidStatusTransition, Assert.Single(result.Failures).Code);
        Assert.Equal(original, booking.Interval);
        Assert.Equal(status, booking.Status);
    }

    [Theory]
    [InlineData(BookingStatus.Requested)]
    [InlineData(BookingStatus.Confirmed)]
    public void MoveTo_the_same_interval_is_refused(BookingStatus status)
    {
        var booking = Rehydrated(status);
        var same = BookingInterval.Create(booking.Interval.StartUtc, booking.Interval.EndUtc, booking.Interval.TimeZoneId).Value;

        var result = booking.MoveTo(same);

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.IntervalUnchanged, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void CanMoveTo_answers_without_changing_anything()
    {
        var booking = Rehydrated(BookingStatus.Confirmed);
        var original = booking.Interval;
        var target = BookingInterval.Create(TestData.Utc(Date, "14:00"), TestData.Utc(Date, "15:00"), TestData.LondonZoneId).Value;

        Assert.True(booking.CanMoveTo(target).Succeeded);
        Assert.Equal(original, booking.Interval);

        Assert.Equal(FailureCodes.IntervalUnchanged, Assert.Single(booking.CanMoveTo(original).Failures).Code);
        Assert.Equal(original, booking.Interval);
    }

    [Fact]
    public void MoveTo_a_cancelled_booking_reports_the_status_not_the_unchanged_interval()
    {
        // Both refusals apply; the status is the one that matters, because no interval would do.
        var booking = Rehydrated(BookingStatus.Cancelled);
        var same = BookingInterval.Create(booking.Interval.StartUtc, booking.Interval.EndUtc, booking.Interval.TimeZoneId).Value;

        var result = booking.MoveTo(same);

        Assert.Equal(FailureCodes.InvalidStatusTransition, Assert.Single(result.Failures).Code);
    }

    // ---------------------------------------------------------------------------------------
    // 2.5 — Moving a booking, through the service
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task A_confirmed_booking_is_moved_to_a_free_interval()
    {
        var h = Wire();
        var booking = await Place(h);
        var original = booking.Interval;

        var moved = await Move(h, booking.Id, "14:00", 90);

        Assert.True(moved.Succeeded);
        var stored = await h.Store.GetBookingAsync(booking.Id);
        Assert.NotNull(stored);
        Assert.Equal(TestData.Utc(Date, "14:00"), stored.Interval.StartUtc);
        Assert.Equal(TestData.Utc(Date, "15:30"), stored.Interval.EndUtc);
        Assert.Equal(booking.Reference, stored.Reference);
        Assert.Equal(BookingStatus.Confirmed, stored.Status);
        Assert.Equal(booking.Booker, stored.Booker);
        Assert.Equal(booking.Service, stored.Service);
        Assert.Equal(booking.Claims, stored.Claims);

        // The old interval is released: something else can take it.
        var claims = await h.Store.GetClaimsAsync(Id(1), original.StartUtc, original.EndUtc);
        Assert.Empty(claims);
    }

    [Fact]
    public async Task A_requested_booking_moves_without_changing_status()
    {
        var h = Wire(autoConfirm: false);
        var booking = await Place(h);
        Assert.Equal(BookingStatus.Requested, booking.Status);

        var moved = await Move(h, booking.Id, "14:00");

        Assert.True(moved.Succeeded);
        Assert.Equal(BookingStatus.Requested, (await h.Store.GetBookingAsync(booking.Id))!.Status);
    }

    [Fact]
    public async Task Moving_a_cancelled_booking_is_refused_and_touches_nothing()
    {
        var h = Wire();
        var booking = await Place(h);
        Assert.True((await h.Bookings.CancelAsync(booking.Id)).Succeeded);
        var movesBefore = h.Store.MoveCount;

        var moved = await Move(h, booking.Id, "14:00");

        AssertSingleFailure(moved, FailureCodes.InvalidStatusTransition);
        Assert.Equal(movesBefore, h.Store.MoveCount);
        Assert.Equal(TestData.Utc(Date, "10:00"), (await h.Store.GetBookingAsync(booking.Id))!.Interval.StartUtc);
        Assert.DoesNotContain("moved", h.Observer.Told);
    }

    [Fact]
    public async Task Moving_a_declined_booking_is_refused()
    {
        var h = Wire(autoConfirm: false);
        var booking = await Place(h);
        Assert.True((await h.Bookings.DeclineAsync(booking.Id)).Succeeded);

        AssertSingleFailure(await Move(h, booking.Id, "14:00"), FailureCodes.InvalidStatusTransition);
    }

    [Fact]
    public async Task A_move_inside_the_resource_lead_time_succeeds()
    {
        // Placed while "now" is a fortnight out, so the visitor's 24-hour lead is satisfied;
        // then "now" is 09:00 on the day and the booking moves to 10:00 — one hour's notice.
        var room = Res(1, leadTime: TimeSpan.FromHours(24));
        var placing = Wire(resources: room);
        var booking = await Place(placing, "14:00");

        var onTheDay = Wire(now: TestData.Utc(Date, "09:00"), resources: room);
        Assert.True((await onTheDay.Store.PlaceAsync(booking)).Succeeded);

        var moved = await Move(onTheDay, booking.Id, "10:00");

        Assert.True(moved.Succeeded, string.Join(", ", moved.Failures.Select(f => f.Code)));
    }

    [Fact]
    public async Task A_move_into_the_past_is_refused_with_the_lead_time_code()
    {
        var h = Wire(resources: Res(1));
        var booking = await Place(h, "14:00");

        var later = Wire(now: TestData.Utc(Date, "12:00"), resources: Res(1));
        Assert.True((await later.Store.PlaceAsync(booking)).Succeeded);

        var moved = await Move(later, booking.Id, "10:00");

        AssertSingleFailure(moved, FailureCodes.LeadTime);
        Assert.Equal(TestData.Utc(Date, "14:00"), (await later.Store.GetBookingAsync(booking.Id))!.Interval.StartUtc);
    }

    [Fact]
    public async Task A_move_beyond_the_horizon_succeeds()
    {
        var h = Wire(resources: Res(1, horizonDays: 90));
        var booking = await Place(h);
        var farDate = Date.AddDays(7 * 20); // same weekday, ~140 days out

        var moved = await h.Bookings.MoveAsync(booking.Id, TestData.Utc(farDate, "10:00"), Mins(60));

        Assert.True(moved.Succeeded, string.Join(", ", moved.Failures.Select(f => f.Code)));
    }

    [Fact]
    public async Task A_move_outside_open_hours_is_refused()
    {
        var h = Wire();
        var booking = await Place(h);

        AssertSingleFailure(await Move(h, booking.Id, "17:30"), FailureCodes.OutsideOpenHours);
    }

    [Fact]
    public async Task A_move_off_the_grid_or_too_long_is_refused_like_a_placement()
    {
        var h = Wire();
        var booking = await Place(h);

        AssertSingleFailure(await Move(h, booking.Id, "10:15"), FailureCodes.Granularity);
        AssertSingleFailure(await Move(h, booking.Id, "08:00", 600), FailureCodes.DurationTooLong);
    }

    [Fact]
    public async Task A_move_onto_another_booking_is_refused_and_the_original_interval_is_kept()
    {
        var h = Wire();
        var booking = await Place(h, "10:00");
        await Place(h, "14:00");

        var moved = await Move(h, booking.Id, "13:30");

        AssertSingleFailure(moved, FailureCodes.Conflict);
        Assert.Equal(TestData.Utc(Date, "10:00"), (await h.Store.GetBookingAsync(booking.Id))!.Interval.StartUtc);
    }

    [Fact]
    public async Task A_move_to_the_interval_already_held_is_refused_before_the_store()
    {
        var h = Wire();
        var booking = await Place(h, "10:00", 60);
        var movesBefore = h.Store.MoveCount;

        var moved = await Move(h, booking.Id, "10:00", 60);

        AssertSingleFailure(moved, FailureCodes.IntervalUnchanged);
        Assert.Equal(movesBefore, h.Store.MoveCount);
    }

    [Fact]
    public async Task Changing_only_the_length_is_a_move()
    {
        var h = Wire();
        var booking = await Place(h, "10:00", 60);

        var moved = await Move(h, booking.Id, "10:00", 90);

        Assert.True(moved.Succeeded);
        Assert.Equal(TestData.Utc(Date, "11:30"), (await h.Store.GetBookingAsync(booking.Id))!.Interval.EndUtc);
    }

    [Fact]
    public async Task A_small_shift_does_not_conflict_with_itself()
    {
        var h = Wire();
        var booking = await Place(h, "10:00", 60);

        var moved = await Move(h, booking.Id, "10:30", 60);

        Assert.True(moved.Succeeded, string.Join(", ", moved.Failures.Select(f => f.Code)));
    }

    private static Service TwoRoles()
        => Service.Create("Massage", null, [new ServiceRole(ResourceTypes.Room, 1), new ServiceRole(Therapist, 1)]).Value;

    [Fact]
    public async Task A_service_booking_moves_with_its_claims()
    {
        var service = TwoRoles();
        var h = Wire(service: service, resources: [Res(1), Res(2), Res(3, Therapist), Res(4, Therapist)]);
        var placed = await h.Services.PlaceAsync(new ServiceBookingRequest
        {
            ServiceId = service.Id,
            Start = TestData.Utc(Date, "10:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });
        Assert.True(placed.Succeeded);
        var claimsBefore = placed.Value.Claims.Select(c => c.ResourceId).Order().ToList();

        var moved = await Move(h, placed.Value.Id, "14:00");

        Assert.True(moved.Succeeded, string.Join(", ", moved.Failures.Select(f => f.Code)));
        var stored = await h.Store.GetBookingAsync(placed.Value.Id);
        Assert.Equal(claimsBefore, stored!.Claims.Select(c => c.ResourceId).Order());
        Assert.Equal(placed.Value.Service, stored.Service);
    }

    [Fact]
    public async Task A_service_booking_whose_resource_is_busy_does_not_swap_it()
    {
        var service = TwoRoles();
        var h = Wire(service: service, resources: [Res(1), Res(3, Therapist), Res(4, Therapist)]);
        var placed = await h.Services.PlaceAsync(new ServiceBookingRequest
        {
            ServiceId = service.Id,
            Start = TestData.Utc(Date, "10:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });
        Assert.True(placed.Succeeded);
        var therapist = placed.Value.Claims.Single(c => c.ResourceId != Id(1)).ResourceId;

        // The assigned therapist is booked directly at 14:00; the other therapist is free.
        await Place(h, "14:00", 60, therapist);

        var moved = await Move(h, placed.Value.Id, "14:00");

        AssertSingleFailure(moved, FailureCodes.Conflict);
        var stored = await h.Store.GetBookingAsync(placed.Value.Id);
        Assert.Contains(stored!.Claims, c => c.ResourceId == therapist);
        Assert.Equal(TestData.Utc(Date, "10:00"), stored.Interval.StartUtc);
    }

    // ---------------------------------------------------------------------------------------
    // service-booking — "Moving a booking placed for a service applies the service's length rules"
    // (QA round 1: a 45–120 minute service booking was moved to 30 minutes live)
    // ---------------------------------------------------------------------------------------

    private static Service BoundedService(int minMinutes, int maxMinutes)
        => Service.Create(
            "Room hire",
            ServiceDuration.Variable(Mins(minMinutes), Mins(maxMinutes)).Value,
            [new ServiceRole(ResourceTypes.Room, 1)]).Value;

    private static async Task<Booking> PlaceForService(Harness h, Service service, string start = "10:00", int minutes = 60)
    {
        var placed = await h.Services.PlaceAsync(new ServiceBookingRequest
        {
            ServiceId = service.Id,
            Start = TestData.Utc(Date, start),
            Duration = Mins(minutes),
            Booker = TestData.Booker(),
        });
        Assert.True(placed.Succeeded, string.Join(", ", placed.Failures.Select(f => f.Code)));
        return placed.Value;
    }

    [Fact]
    public async Task A_service_booking_cannot_be_moved_to_a_length_the_service_forbids()
    {
        var service = BoundedService(45, 120);
        var h = Wire(service: service, resources: Res(1));
        var booking = await PlaceForService(h, service);

        // THROUGH THE SERVICE BOOKING SERVICE — the operator's entry point. The booking service
        // alone knows resources and would accept 30 (the room allows 30–480).
        var moved = await h.Services.MoveAsync(booking.Id, TestData.Utc(Date, "14:00"), Mins(30));

        AssertSingleFailure(moved, FailureCodes.DurationTooShort);
        Assert.Equal(TestData.Utc(Date, "10:00"), (await h.Store.GetBookingAsync(booking.Id))!.Interval.StartUtc);
        Assert.DoesNotContain("moved", h.Observer.Told);
    }

    [Fact]
    public async Task A_service_booking_cannot_be_moved_past_a_claimed_resources_ceiling()
    {
        // The service permits up to 240; the room's own maximum is 90. The resource ceiling is
        // never widened by the service — the same rule as placement.
        var service = BoundedService(30, 240);
        var room = Resource.Create(
            ResourceTypes.Room, "Small room", directlyBookable: true,
            availability: TestData.Config(
                TestData.Weekly("08:00", "18:00", Date.DayOfWeek),
                constraints: BookingConstraints.Create(granularity: Mins(30), minDuration: Mins(30), maxDuration: Mins(90)).Value),
            id: Id(1)).Value;
        var h = Wire(service: service, resources: room);
        var booking = await PlaceForService(h, service);

        var moved = await h.Services.MoveAsync(booking.Id, TestData.Utc(Date, "14:00"), Mins(120));

        AssertSingleFailure(moved, FailureCodes.DurationTooLong);
    }

    [Fact]
    public async Task A_service_booking_moves_to_a_length_both_permit()
    {
        var service = BoundedService(45, 120);
        var h = Wire(service: service, resources: Res(1));
        var booking = await PlaceForService(h, service);

        var moved = await h.Services.MoveAsync(booking.Id, TestData.Utc(Date, "14:00"), Mins(90));

        Assert.True(moved.Succeeded, string.Join(", ", moved.Failures.Select(f => f.Code)));
        var stored = await h.Store.GetBookingAsync(booking.Id);
        Assert.Equal(TestData.Utc(Date, "15:30"), stored!.Interval.EndUtc);
        Assert.Equal(booking.Claims, stored.Claims);
        Assert.Contains("moved", h.Observer.Told);
    }

    [Fact]
    public async Task A_deleted_service_no_longer_binds_a_move()
    {
        // The attribution is a snapshot; a specification that has been deleted cannot bind.
        var service = BoundedService(45, 120);
        var h = Wire(service: service, resources: Res(1));
        var booking = await PlaceForService(h, service);

        var withoutService = Wire(resources: Res(1)); // same resource, no service in the store
        Assert.True((await withoutService.Store.PlaceAsync(booking)).Succeeded);

        var moved = await withoutService.Services.MoveAsync(booking.Id, TestData.Utc(Date, "14:00"), Mins(30));

        Assert.True(moved.Succeeded, string.Join(", ", moved.Failures.Select(f => f.Code)));
    }

    [Fact]
    public async Task A_direct_booking_is_delegated_untouched_by_the_service_booking_service()
    {
        var h = Wire();
        var booking = await Place(h, "10:00");
        await Place(h, "14:00");

        // The same refusal the booking service gives, for the same arguments.
        var viaService = await h.Services.MoveAsync(booking.Id, TestData.Utc(Date, "13:30"), Mins(60));
        var direct = await h.Bookings.MoveAsync(booking.Id, TestData.Utc(Date, "13:30"), Mins(60));

        AssertSingleFailure(viaService, FailureCodes.Conflict);
        AssertSingleFailure(direct, FailureCodes.Conflict);

        var moved = await h.Services.MoveAsync(booking.Id, TestData.Utc(Date, "15:00"), Mins(60));
        Assert.True(moved.Succeeded);
    }

    [Fact]
    public async Task An_unknown_booking_through_the_service_booking_service_is_not_found()
    {
        var h = Wire();

        AssertSingleFailure(await h.Services.MoveAsync(Guid.NewGuid(), TestData.Utc(Date, "14:00"), Mins(60)), FailureCodes.BookingNotFound);
    }

    [Fact]
    public async Task A_booking_with_an_erased_booker_can_move()
    {
        var h = Wire();
        var booking = await Place(h);
        Assert.True((await h.Bookings.EraseBookerAsync(booking.Id)).Succeeded);

        var moved = await Move(h, booking.Id, "14:00");

        Assert.True(moved.Succeeded);
        var stored = await h.Store.GetBookingAsync(booking.Id);
        Assert.True(stored!.Booker.IsErased);
        Assert.Equal(TestData.Utc(Date, "14:00"), stored.Interval.StartUtc);
    }

    [Fact]
    public async Task An_unknown_booking_is_reported_as_not_found()
    {
        var h = Wire();

        AssertSingleFailure(await Move(h, Guid.NewGuid(), "14:00"), FailureCodes.BookingNotFound);
        Assert.Empty(h.Observer.Told);
    }

    [Fact]
    public async Task The_status_is_checked_before_any_rule()
    {
        // A cancelled booking moved to an interval every rule would refuse hears about its
        // status, not its interval: no time would do, so the rules are never the reason.
        var h = Wire();
        var booking = await Place(h);
        Assert.True((await h.Bookings.CancelAsync(booking.Id)).Succeeded);

        AssertSingleFailure(await Move(h, booking.Id, "03:00", 5), FailureCodes.InvalidStatusTransition);
    }

    // ---------------------------------------------------------------------------------------
    // 2.6 — Observation
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task A_moved_booking_is_reported_once_after_the_store_with_where_it_came_from()
    {
        var h = Wire();
        var booking = await Place(h, "10:00");
        var previous = booking.Interval;

        var moved = await Move(h, booking.Id, "14:00");

        Assert.True(moved.Succeeded);
        Assert.Equal(["placed", "moved"], h.Observer.Told);
        var report = Assert.Single(h.Observer.Moves);
        Assert.Equal(booking.Id, report.BookingId);
        Assert.Equal(previous, report.Previous);
        // Told AFTER the write: the store already held the new interval, and had taken the move.
        Assert.Equal(TestData.Utc(Date, "14:00"), report.StoredWhenTold.StartUtc);
        Assert.Equal(1, report.MovesWhenTold);
    }

    [Fact]
    public async Task Every_refused_move_reports_nothing()
    {
        var h = Wire();
        var booking = await Place(h, "10:00");
        await Place(h, "14:00");
        h.Observer.Told.Clear();

        Assert.False((await Move(h, booking.Id, "13:30")).Succeeded);   // conflict
        Assert.False((await Move(h, booking.Id, "17:30")).Succeeded);   // outside open hours
        Assert.False((await Move(h, booking.Id, "10:00")).Succeeded);   // unchanged
        Assert.False((await Move(h, Guid.NewGuid(), "12:00")).Succeeded); // not found

        Assert.Empty(h.Observer.Told);
    }

    private sealed class ThrowingObserver : IBookingObserver
    {
        public Task BookingPlacedAsync(Booking booking, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task BookingConfirmedAsync(Booking booking, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task BookingDeclinedAsync(Booking booking, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task BookingCancelledAsync(Booking booking, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task BookingMovedAsync(Booking booking, BookingInterval previousInterval, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The site's handler is broken.");
    }

    [Fact]
    public async Task A_throwing_observer_does_not_break_a_move()
    {
        var h = Wire(observer: new ThrowingObserver());
        var booking = await Place(h);

        var moved = await Move(h, booking.Id, "14:00");

        Assert.True(moved.Succeeded);
        Assert.Equal(TestData.Utc(Date, "14:00"), (await h.Store.GetBookingAsync(booking.Id))!.Interval.StartUtc);
    }

    // ---------------------------------------------------------------------------------------
    // 2.7 — Atomic move contract, against the in-memory double
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Delegates everything to the real store, and runs <paramref name="beforeMove"/> at the
    /// one moment the contract is about: after the service has read and decided, before the
    /// store's move runs.
    /// </summary>
    private sealed class Interposing(InMemoryBookingStore inner, Func<InMemoryBookingStore, Task> beforeMove) : IBookingStore
    {
        public Task<IReadOnlyList<ClaimInfo>> GetClaimsAsync(Guid resourceId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
            => inner.GetClaimsAsync(resourceId, fromUtc, toUtc, cancellationToken);

        public Task<IReadOnlyList<ClaimInfo>> GetClaimsAsync(IReadOnlyCollection<Guid> resourceIds, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
            => inner.GetClaimsAsync(resourceIds, fromUtc, toUtc, cancellationToken);

        public Task<DomainResult<Booking>> PlaceAsync(Booking booking, CancellationToken cancellationToken = default)
            => inner.PlaceAsync(booking, cancellationToken);

        public Task<Booking?> GetBookingAsync(Guid bookingId, CancellationToken cancellationToken = default)
            => inner.GetBookingAsync(bookingId, cancellationToken);

        public Task UpdateAsync(Booking booking, CancellationToken cancellationToken = default)
            => inner.UpdateAsync(booking, cancellationToken);

        public async Task<DomainResult> MoveAsync(Guid bookingId, BookingInterval newInterval, IReadOnlyCollection<BookingStatus> permittedFrom, CancellationToken cancellationToken = default)
        {
            await beforeMove(inner);
            return await inner.MoveAsync(bookingId, newInterval, permittedFrom, cancellationToken);
        }

        public Task<bool> EraseBookerAsync(Guid bookingId, DateTimeOffset erasedUtc, CancellationToken cancellationToken = default)
            => inner.EraseBookerAsync(bookingId, erasedUtc, cancellationToken);

        public Task<IReadOnlyList<Guid>> GetBookingIdsDueForErasureAsync(DateTimeOffset cutoffUtc, int take, CancellationToken cancellationToken = default)
            => inner.GetBookingIdsDueForErasureAsync(cutoffUtc, take, cancellationToken);
    }

    private static Booking DirectBooking(Guid resourceId, string start, int minutes, BookingStatus status = BookingStatus.Confirmed)
        => Booking.Rehydrate(
            Guid.NewGuid(),
            new RandomBookingReferenceFactory().Next(),
            BookingInterval.Create(TestData.Utc(Date, start), TestData.Utc(Date, start).Add(Mins(minutes)), TestData.LondonZoneId).Value,
            TestData.Booker(),
            [new ResourceClaim(resourceId)],
            status,
            TestData.Now).Value;

    [Fact]
    public async Task A_move_and_a_placement_racing_for_the_new_interval_yield_exactly_one_success()
    {
        var h = Wire();
        var booking = await Place(h, "10:00");

        var outcomes = await Task.WhenAll(
            Task.Run(() => Move(h, booking.Id, "14:00")),
            Task.Run(() => h.Bookings.PlaceAsync(new BookingRequest
            {
                ResourceId = Id(1),
                Start = TestData.Utc(Date, "14:00"),
                Duration = Mins(60),
                Booker = TestData.Booker(),
            })));

        Assert.Equal(1, outcomes.Count(r => r.Succeeded));
        var loser = outcomes.Single(r => !r.Succeeded);
        Assert.Equal(FailureCodes.Conflict, Assert.Single(loser.Failures).Code);

        // Exactly one blocking booking holds 14:00 afterwards.
        var claims = await h.Store.GetClaimsAsync(Id(1), TestData.Utc(Date, "14:00"), TestData.Utc(Date, "15:00"));
        Assert.Single(claims, c => c.Status is BookingStatus.Requested or BookingStatus.Confirmed);
    }

    [Fact]
    public async Task A_placement_takes_the_old_interval_only_after_the_move_commits()
    {
        DomainResult<Booking>? beforeCommit = null;
        var h = Wire(wrapStore: memory => new Interposing(memory, async store =>
        {
            // The service has decided to move 10:00 → 14:00 but the store has not written it.
            // The booking still holds 10:00, so a placement there must lose.
            beforeCommit = await store.PlaceAsync(DirectBooking(Id(1), "10:00", 60));
        }));
        var booking = await Place(h, "10:00");

        var moved = await Move(h, booking.Id, "14:00");

        Assert.True(moved.Succeeded);
        Assert.NotNull(beforeCommit);
        Assert.Equal(FailureCodes.Conflict, Assert.Single(beforeCommit.Failures).Code);

        // After the commit, 10:00 is free.
        var afterCommit = await h.Store.PlaceAsync(DirectBooking(Id(1), "10:00", 60));
        Assert.True(afterCommit.Succeeded);
    }

    [Fact]
    public async Task A_cancel_landing_between_read_and_write_wins()
    {
        var h = Wire(wrapStore: memory => new Interposing(memory, async store =>
        {
            // Another operator cancels it after this move has read and decided.
            var stored = await store.GetBookingAsync(store.Ids().Single());
            Assert.True(stored!.Cancel().Succeeded);
            await store.UpdateAsync(stored);
        }));
        var booking = await Place(h, "10:00");

        var moved = await Move(h, booking.Id, "14:00");

        AssertSingleFailure(moved, FailureCodes.InvalidStatusTransition);
        var stored = await h.Store.GetBookingAsync(booking.Id);
        Assert.Equal(BookingStatus.Cancelled, stored!.Status);
        Assert.Equal(TestData.Utc(Date, "10:00"), stored.Interval.StartUtc);
        Assert.DoesNotContain("moved", h.Observer.Told);
    }

    [Fact]
    public async Task Two_moves_of_the_same_booking_both_complete_and_one_interval_remains()
    {
        var h = Wire();
        var booking = await Place(h, "10:00");

        var outcomes = await Task.WhenAll(
            Task.Run(() => Move(h, booking.Id, "13:00")),
            Task.Run(() => Move(h, booking.Id, "15:00")));

        Assert.All(outcomes, o => Assert.True(o.Succeeded));
        var stored = await h.Store.GetBookingAsync(booking.Id);
        Assert.Contains(stored!.Interval.StartUtc, new[] { TestData.Utc(Date, "13:00"), TestData.Utc(Date, "15:00") });

        // Only one claim on the calendar, wherever it landed.
        var claims = await h.Store.GetClaimsAsync(Id(1), TestData.Utc(Date, "08:00"), TestData.Utc(Date, "18:00"));
        Assert.Single(claims);
    }

    [Fact]
    public async Task An_erasure_between_read_and_write_survives_the_move()
    {
        var h = Wire(wrapStore: memory => new Interposing(memory, async store =>
        {
            await store.EraseBookerAsync(store.Ids().Single(), TestData.Now.AddDays(1));
        }));
        var booking = await Place(h, "10:00");

        var moved = await Move(h, booking.Id, "14:00");

        Assert.True(moved.Succeeded);
        var stored = await h.Store.GetBookingAsync(booking.Id);
        Assert.True(stored!.Booker.IsErased);
        Assert.Equal(TestData.Now.AddDays(1), stored.Booker.ErasedUtc);
        Assert.Equal(TestData.Utc(Date, "14:00"), stored.Interval.StartUtc);
    }

    [Fact]
    public async Task The_move_write_leaves_a_stale_aggregates_status_alone()
    {
        // Confirm it between read and write; the move must not write the stale Requested back.
        var h = Wire(autoConfirm: false, wrapStore: memory => new Interposing(memory, async store =>
        {
            var stored = await store.GetBookingAsync(store.Ids().Single());
            Assert.True(stored!.Confirm().Succeeded);
            await store.UpdateAsync(stored);
        }));
        var booking = await Place(h, "10:00");
        Assert.Equal(BookingStatus.Requested, booking.Status);

        var moved = await Move(h, booking.Id, "14:00");

        Assert.True(moved.Succeeded);
        var stored = await h.Store.GetBookingAsync(booking.Id);
        Assert.Equal(BookingStatus.Confirmed, stored!.Status);
        Assert.Equal(TestData.Utc(Date, "14:00"), stored.Interval.StartUtc);
    }
}
