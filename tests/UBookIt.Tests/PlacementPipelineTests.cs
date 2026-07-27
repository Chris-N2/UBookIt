using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

public class PlacementPipelineTests
{
    private static readonly DateOnly Date = TestData.BaseDate;

    private static BookingRequest Request(
        Guid resourceId, DateTimeOffset start, TimeSpan? duration = null) => new()
    {
        ResourceId = resourceId,
        Start = start,
        Duration = duration ?? TimeSpan.FromHours(1),
        Booker = TestData.Booker(),
    };

    private static async Task AssertSingleFailure(
        Task<UBookIt.Core.Common.DomainResult<Booking>> placement, string expectedCode)
    {
        var result = await placement;
        Assert.False(result.Succeeded);
        Assert.Equal(expectedCode, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public async Task Unknown_resource_fails()
    {
        var (bookings, _, _) = TestData.Services(TestData.Room());

        await AssertSingleFailure(
            bookings.PlaceAsync(Request(Guid.NewGuid(), TestData.Utc(Date, "10:00"))),
            FailureCodes.ResourceNotFound);
    }

    [Fact]
    public async Task Zero_duration_is_interval_invalid()
    {
        var room = TestData.Room();
        var (bookings, _, _) = TestData.Services(room);

        await AssertSingleFailure(
            bookings.PlaceAsync(Request(room.Id, TestData.Utc(Date, "10:00"), TimeSpan.Zero)),
            FailureCodes.IntervalInvalid);
    }

    [Fact]
    public async Task Unaligned_duration_is_granularity_failure()
    {
        var room = TestData.Room();
        var (bookings, _, _) = TestData.Services(room);

        await AssertSingleFailure(
            bookings.PlaceAsync(Request(room.Id, TestData.Utc(Date, "10:00"), TimeSpan.FromMinutes(40))),
            FailureCodes.Granularity);
    }

    [Fact]
    public async Task Unaligned_start_within_a_window_is_granularity_failure()
    {
        var room = TestData.Room();
        var (bookings, _, _) = TestData.Services(room);

        await AssertSingleFailure(
            bookings.PlaceAsync(Request(room.Id, TestData.Utc(Date, "10:10"))),
            FailureCodes.Granularity);
    }

    [Fact]
    public async Task Too_short_duration_fails()
    {
        var room = TestData.Room();
        var (bookings, _, _) = TestData.Services(room);

        await AssertSingleFailure(
            bookings.PlaceAsync(Request(room.Id, TestData.Utc(Date, "10:00"), TimeSpan.FromMinutes(15))),
            FailureCodes.DurationTooShort);
    }

    [Fact]
    public async Task Too_long_duration_fails()
    {
        var room = TestData.Room();
        var (bookings, _, _) = TestData.Services(room);

        await AssertSingleFailure(
            bookings.PlaceAsync(Request(room.Id, TestData.Utc(Date, "08:00"), TimeSpan.FromHours(9))),
            FailureCodes.DurationTooLong);
    }

    [Fact]
    public async Task Booking_sooner_than_lead_time_fails()
    {
        var room = TestData.Room(TestData.Config(
            TestData.Weekly("00:00", "23:45", Date.DayOfWeek),
            constraints: BookingConstraints.Create(leadTime: TimeSpan.FromHours(2)).Value));

        // "Now" one hour before the requested start on the booking date itself.
        var now = TestData.Utc(Date, "09:00");
        var (bookings, _, _) = TestData.Services(room, now);

        await AssertSingleFailure(
            bookings.PlaceAsync(Request(room.Id, TestData.Utc(Date, "10:00"))),
            FailureCodes.LeadTime);
    }

    [Fact]
    public async Task Booking_beyond_horizon_fails()
    {
        var farDate = Date.AddDays(7 * 20); // same weekday, ~140 days out (> 90-day horizon)
        var room = TestData.Room();
        var (bookings, _, _) = TestData.Services(room);

        await AssertSingleFailure(
            bookings.PlaceAsync(Request(room.Id, TestData.Utc(farDate, "10:00"))),
            FailureCodes.Horizon);
    }

    [Fact]
    public async Task Interval_outside_open_hours_fails()
    {
        var room = TestData.Room();
        var (bookings, _, _) = TestData.Services(room);

        await AssertSingleFailure(
            bookings.PlaceAsync(Request(room.Id, TestData.Utc(Date, "07:00"))),
            FailureCodes.OutsideOpenHours);
    }

    [Fact]
    public async Task Interval_straddling_the_window_edge_fails()
    {
        var room = TestData.Room();
        var (bookings, _, _) = TestData.Services(room);

        await AssertSingleFailure(
            bookings.PlaceAsync(Request(room.Id, TestData.Utc(Date, "17:30"))),
            FailureCodes.OutsideOpenHours);
    }

    [Fact]
    public async Task Valid_request_succeeds_with_confirmed_single_claim_booking()
    {
        var room = TestData.Room();
        var (bookings, _, _) = TestData.Services(room);

        var result = await bookings.PlaceAsync(Request(room.Id, TestData.Utc(Date, "10:00")));

        Assert.True(result.Succeeded);
        var booking = result.Value;
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        var claim = Assert.Single(booking.Claims);
        Assert.Equal(room.Id, claim.ResourceId);
        Assert.Equal(TestData.Utc(Date, "10:00"), booking.Interval.StartUtc);
        Assert.Equal(TestData.Utc(Date, "11:00"), booking.Interval.EndUtc);
        Assert.Equal(TestData.LondonZoneId, booking.Interval.TimeZoneId);
    }

    [Fact]
    public async Task Multiple_failures_are_reported_in_pipeline_order()
    {
        var room = TestData.Room(TestData.Config(
            TestData.Weekly("08:00", "18:00", Date.DayOfWeek),
            constraints: BookingConstraints.Create(leadTime: TimeSpan.FromHours(2)).Value));
        var now = TestData.Utc(Date, "06:30");
        var (bookings, _, _) = TestData.Services(room, now);

        // 07:00 start: violates lead time AND is outside open hours.
        var result = await bookings.PlaceAsync(Request(room.Id, TestData.Utc(Date, "07:00")));

        Assert.False(result.Succeeded);
        Assert.Equal(
            new[] { FailureCodes.LeadTime, FailureCodes.OutsideOpenHours },
            result.Failures.Select(f => f.Code).ToArray());
    }

    [Fact]
    public async Task Cancelling_a_confirmed_booking_succeeds_then_repeat_cancel_fails()
    {
        var room = TestData.Room();
        var (bookings, _, _) = TestData.Services(room);
        var placed = await bookings.PlaceAsync(Request(room.Id, TestData.Utc(Date, "10:00")));

        var cancelled = await bookings.CancelAsync(placed.Value.Id);
        Assert.True(cancelled.Succeeded);
        Assert.Equal(BookingStatus.Cancelled, cancelled.Value.Status);

        var again = await bookings.CancelAsync(placed.Value.Id);
        Assert.False(again.Succeeded);
        Assert.Equal(FailureCodes.InvalidStatusTransition, Assert.Single(again.Failures).Code);
    }

    [Fact]
    public async Task Cancelling_an_unknown_booking_fails()
    {
        var (bookings, _, _) = TestData.Services(TestData.Room());

        var result = await bookings.CancelAsync(Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.BookingNotFound, Assert.Single(result.Failures).Code);
    }
}
