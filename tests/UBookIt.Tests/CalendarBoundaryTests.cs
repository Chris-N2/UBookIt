using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Inputs at the edges of the representable calendar must fail as structured
/// domain failures, never as exceptions (out-of-range-dates). The defect class
/// is an unhandled exception escaping an anonymous endpoint, so every test here
/// asserts a <see cref="DomainResult"/> — a thrown exception fails the test by
/// escaping it.
/// </summary>
public class CalendarBoundaryTests
{
    private static TimeSpan Mins(int minutes) => TimeSpan.FromMinutes(minutes);

    private static readonly DateOnly LastDay = DateOnly.MaxValue;
    private static readonly DateOnly FirstDay = DateOnly.MinValue;

    private static Resource Room(string type = ResourceTypes.Room)
        => Resource.Create(
            type,
            "Boundary Room",
            // Offered for direct booking. The domain default is the opposite;
            // these fixtures stand for ordinary bookable resources, and the
            // permission itself is exercised explicitly in DirectBookingTests.
            directlyBookable: true,
            availability: TestData.Config(
                WeeklyOpenHours.Create(
                    Enum.GetValues<DayOfWeek>().Select(d => (d, TestData.Win("09:00", "17:00")))).Value),
            id: Guid.NewGuid()).Value;

    private static string SingleCode(DomainResult result) => Assert.Single(result.Failures).Code;

    // ------------------------------------------------------------- availability

    [Fact]
    public async Task Free_time_at_the_last_representable_date_is_rejected()
    {
        var room = Room();
        var (_, availability, _) = TestData.Services(room);

        var result = await availability.GetFreeTimeAsync(room.Id, LastDay, LastDay);

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.DateRangeInvalid, SingleCode(result));
    }

    [Fact]
    public async Task Slots_at_the_last_representable_date_are_rejected()
    {
        var room = Room();
        var (_, availability, _) = TestData.Services(room);

        var result = await availability.GetSlotsAsync(room.Id, LastDay, LastDay, Mins(60));

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.DateRangeInvalid, SingleCode(result));
    }

    [Fact]
    public async Task Bookable_starts_at_the_last_representable_date_are_rejected()
    {
        var room = Room();
        var (_, availability, _) = TestData.Services(room);

        var result = await availability.GetBookableStartsAsync(room.Id, LastDay, LastDay);

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.DateRangeInvalid, SingleCode(result));
    }

    [Fact]
    public async Task Service_bookable_starts_at_the_last_representable_date_are_rejected()
    {
        var room = Room();
        var service = Service.Create("Boundary", null, [new ServiceRole(ResourceTypes.Room, 1)]).Value;

        var resources = new InMemoryResourceStore().Add(room);
        var bookings = new InMemoryBookingStore();
        var time = new FixedTimeProvider(TestData.Now);
        var settings = TestData.Settings;

        var serviceBooking = new ServiceBookingService(
            new InMemoryServiceStore().Add(service),
            resources,
            bookings,
            new AvailabilityService(resources, bookings, time, settings),
            new BookingService(resources, bookings, time, settings),
            settings);

        var result = await serviceBooking.GetBookableStartsAsync(service.Id, LastDay, LastDay);

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.DateRangeInvalid, SingleCode(result));
    }

    [Fact]
    public async Task Free_time_at_the_first_representable_date_is_rejected()
    {
        var room = Room();
        var (_, availability, _) = TestData.Services(room);

        var result = await availability.GetFreeTimeAsync(room.Id, FirstDay, FirstDay);

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.DateRangeInvalid, SingleCode(result));
    }

    [Fact]
    public void The_first_representable_date_would_throw_in_a_zone_east_of_utc()
    {
        // Why the lower bound is rejected at all rather than left to work: with a
        // western zone the mapping happens to succeed, so a London-only test
        // would have called this safe. Mapping 09:00 on year one into UTC from an
        // eastern zone lands before the first representable instant.
        var auckland = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DateTimeOffset(
                FirstDay.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Unspecified),
                auckland.GetUtcOffset(FirstDay.ToDateTime(new TimeOnly(9, 0)))));
    }

    [Fact]
    public async Task Queries_just_inside_the_edges_still_work()
    {
        var room = Room();
        var (_, availability, _) = TestData.Services(room);

        var nearEnd = LastDay.AddDays(-1);
        var nearStart = FirstDay.AddDays(1);

        var end = await availability.GetFreeTimeAsync(room.Id, nearEnd, nearEnd);
        var start = await availability.GetFreeTimeAsync(room.Id, nearStart, nearStart);

        Assert.True(end.Succeeded, "the day before the last representable date must remain queryable");
        Assert.True(start.Succeeded, "the day after the first representable date must remain queryable");
    }

    [Fact]
    public async Task An_over_wide_range_still_reports_as_over_wide_even_at_the_edge()
    {
        // Ordering matters: the span check runs first, so a range that is both
        // too wide and edge-touching reports the more useful of the two.
        var room = Room();
        var (_, availability, _) = TestData.Services(room);

        var result = await availability.GetFreeTimeAsync(room.Id, LastDay.AddDays(-400), LastDay);

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.DateRangeTooLarge, SingleCode(result));
    }

    // ---------------------------------------------------------------- placement

    [Fact]
    public async Task A_placement_whose_interval_would_overflow_is_rejected()
    {
        var room = Room();
        var (bookings, _, store) = TestData.Services(room);

        var result = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = room.Id,
            Start = DateTimeOffset.MaxValue - TimeSpan.FromMinutes(30),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.IntervalInvalid, SingleCode(result));

        // Nothing persisted: no claim exists for this resource anywhere in time.
        Assert.Empty(await store.GetClaimsAsync(
            room.Id, DateTimeOffset.MinValue, DateTimeOffset.MaxValue));
    }

    [Fact]
    public async Task A_placement_whose_interval_would_underflow_is_rejected()
    {
        var room = Room();
        var (bookings, _, _) = TestData.Services(room);

        var result = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = room.Id,
            Start = TestData.Utc(TestData.BaseDate, "09:00"),
            Duration = TimeSpan.FromMinutes(int.MinValue),
            Booker = TestData.Booker(),
        });

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.IntervalInvalid, SingleCode(result));
    }

    [Fact]
    public async Task A_placement_starting_on_the_first_representable_date_is_rejected()
    {
        var room = Room();
        var (bookings, _, _) = TestData.Services(room);

        var result = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = room.Id,
            Start = new DateTimeOffset(FirstDay.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.IntervalInvalid, SingleCode(result));
    }

    [Fact]
    public async Task A_placement_on_the_day_before_the_last_representable_date_is_rejected()
    {
        // The tenth case, which probing the endpoints did not surface: the
        // interval is representable, but the open-hours window needs the day
        // after, and the walk then steps one past it.
        var room = Room();
        var (bookings, _, _) = TestData.Services(room);

        var result = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = room.Id,
            Start = new DateTimeOffset(LastDay.AddDays(-1).ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.IntervalInvalid, SingleCode(result));
    }

    [Fact]
    public async Task A_service_placement_at_a_calendar_edge_is_rejected()
    {
        var room = Room();
        var service = Service.Create("Boundary", null, [new ServiceRole(ResourceTypes.Room, 1)]).Value;

        var resources = new InMemoryResourceStore().Add(room);
        var store = new InMemoryBookingStore();
        var time = new FixedTimeProvider(TestData.Now);
        var settings = TestData.Settings;

        var serviceBooking = new ServiceBookingService(
            new InMemoryServiceStore().Add(service),
            resources,
            store,
            new AvailabilityService(resources, store, time, settings),
            new BookingService(resources, store, time, settings),
            settings);

        var result = await serviceBooking.PlaceAsync(new ServiceBookingRequest
        {
            ServiceId = service.Id,
            Start = new DateTimeOffset(FirstDay.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        Assert.False(result.Succeeded);

        // Echoed, not translated into service-unavailable: an unrepresentable
        // interval is a property of the request, identical for every candidate,
        // so blaming the pool would send the caller looking for another slot.
        Assert.Equal(FailureCodes.IntervalInvalid, SingleCode(result));
    }

    [Fact]
    public async Task Ordinary_far_future_placement_still_fails_on_horizon()
    {
        // The guards must not have swallowed a real rule: a date far ahead but
        // nowhere near the calendar's limit is a horizon problem, as before.
        var room = Room();
        var (bookings, _, _) = TestData.Services(room);

        var result = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = room.Id,
            Start = TestData.Utc(TestData.BaseDate.AddYears(5), "09:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Code == FailureCodes.Horizon);
        Assert.DoesNotContain(result.Failures, f => f.Code == FailureCodes.IntervalInvalid);
    }

    // ------------------------------------------------------------- offset dimension

    // A start carries its own UTC offset, and the addition that builds the
    // interval moves the *clock* component, not the UTC instant. Headroom
    // measured against DateTimeOffset.MaxValue/MinValue is therefore the wrong
    // headroom for an offset-carrying start — the clock can overflow while UTC
    // still has room, and vice versa at the bottom.

    // Only offsets that keep the *start itself* representable are listed: a
    // western offset at the ceiling (or an eastern one at the floor) cannot be
    // constructed at all, so it never reaches the domain.
    [Theory]
    [InlineData(14)]
    [InlineData(1)]
    public async Task A_placement_near_the_ceiling_is_rejected_whatever_its_offset(int offsetHours)
    {
        var room = Room();
        var (bookings, _, _) = TestData.Services(room);

        var start = new DateTimeOffset(
            LastDay.ToDateTime(new TimeOnly(23, 0), DateTimeKind.Unspecified), TimeSpan.FromHours(offsetHours));

        var result = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = room.Id,
            Start = start,
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.IntervalInvalid, SingleCode(result));
    }

    [Theory]
    [InlineData(-14)]
    [InlineData(-1)]
    public async Task A_placement_near_the_floor_is_rejected_whatever_its_offset(int offsetHours)
    {
        var room = Room();
        var (bookings, _, _) = TestData.Services(room);

        var start = new DateTimeOffset(
            FirstDay.ToDateTime(new TimeOnly(0, 0), DateTimeKind.Unspecified), TimeSpan.FromHours(offsetHours));

        var result = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = room.Id,
            Start = start,
            Duration = Mins(-60),
            Booker = TestData.Booker(),
        });

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.IntervalInvalid, SingleCode(result));
    }

    [Fact]
    public async Task A_service_placement_near_the_ceiling_is_rejected_whatever_its_offset()
    {
        var room = Room();
        var service = Service.Create("Boundary", null, [new ServiceRole(ResourceTypes.Room, 1)]).Value;

        var resources = new InMemoryResourceStore().Add(room);
        var store = new InMemoryBookingStore();
        var time = new FixedTimeProvider(TestData.Now);
        var settings = TestData.Settings;

        var serviceBooking = new ServiceBookingService(
            new InMemoryServiceStore().Add(service),
            resources,
            store,
            new AvailabilityService(resources, store, time, settings),
            new BookingService(resources, store, time, settings),
            settings);

        var result = await serviceBooking.PlaceAsync(new ServiceBookingRequest
        {
            ServiceId = service.Id,
            Start = new DateTimeOffset(
                LastDay.ToDateTime(new TimeOnly(23, 0), DateTimeKind.Unspecified), TimeSpan.FromHours(14)),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.IntervalInvalid, SingleCode(result));
    }

    [Fact]
    public async Task A_placement_just_inside_the_floor_is_evaluated_in_an_eastern_site_zone()
    {
        // The open-hours window reaches one day either side of the start, and
        // mapping year one into UTC from an eastern zone falls off the calendar.
        // The lower window margin must therefore match the upper one.
        var auckland = new SiteBookingSettings { TimeZoneId = "Pacific/Auckland" };
        var room = Room();
        var resources = new InMemoryResourceStore().Add(room);
        var store = new InMemoryBookingStore();
        var bookings = new BookingService(
            resources, store, new FixedTimeProvider(TestData.Now), auckland);

        var result = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = room.Id,
            Start = new DateTimeOffset(
                FirstDay.AddDays(1).ToDateTime(new TimeOnly(9, 0), DateTimeKind.Unspecified), TimeSpan.FromHours(13)),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.IntervalInvalid, SingleCode(result));
    }

    // ------------------------------------------------------------------ horizon

    [Fact]
    public async Task An_enormous_horizon_saturates_rather_than_throwing()
    {
        // HorizonDays is only validated as positive, so an administrator can
        // configure one that overflows the calendar. That must not break every
        // request against the resource.
        var room = Resource.Create(
            ResourceTypes.Room,
            "Long Horizon",
            directlyBookable: true,
            availability: TestData.Config(
                TestData.Weekly("09:00", "17:00", TestData.BaseDate.DayOfWeek),
                constraints: BookingConstraints.Create(horizonDays: int.MaxValue).Value),
            id: Guid.NewGuid()).Value;

        var (bookings, availability, _) = TestData.Services(room);

        var starts = await availability.GetBookableStartsAsync(room.Id, TestData.BaseDate, TestData.BaseDate);
        Assert.True(starts.Succeeded);
        Assert.NotEmpty(starts.Value);

        var placed = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = room.Id,
            Start = TestData.Utc(TestData.BaseDate, "09:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        Assert.True(placed.Succeeded);
    }

    [Fact]
    public void Saturating_addition_clamps_at_both_ends()
    {
        Assert.Equal(DateOnly.MaxValue, CalendarBounds.AddDaysSaturating(DateOnly.MaxValue, 1));
        Assert.Equal(DateOnly.MaxValue, CalendarBounds.AddDaysSaturating(TestData.BaseDate, int.MaxValue));
        Assert.Equal(DateOnly.MinValue, CalendarBounds.AddDaysSaturating(TestData.BaseDate, int.MinValue));
        Assert.Equal(TestData.BaseDate.AddDays(7), CalendarBounds.AddDaysSaturating(TestData.BaseDate, 7));
    }
}
