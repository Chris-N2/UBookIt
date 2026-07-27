using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Touching open-hours windows coalesce into continuous bookable time
/// (availability spec, "Free-time computation").
/// </summary>
public class TouchingWindowTests
{
    private static readonly DateOnly Date = TestData.BaseDate;

    private static UBookIt.Core.Resources.Resource SplitDayRoom()
        => TestData.Room(TestData.Config(WeeklyOpenHours.Create(
        [
            (Date.DayOfWeek, TestData.Win("08:00", "12:00")),
            (Date.DayOfWeek, TestData.Win("12:00", "14:00")),
        ]).Value));

    [Fact]
    public async Task Touching_windows_report_a_single_free_interval()
    {
        var room = SplitDayRoom();
        var (_, availability, _) = TestData.Services(room);

        var result = await availability.GetFreeTimeAsync(room.Id, Date, Date);

        Assert.True(result.Succeeded);
        var window = Assert.Single(result.Value);
        Assert.Equal(TestData.Utc(Date, "08:00"), window.StartUtc);
        Assert.Equal(TestData.Utc(Date, "14:00"), window.EndUtc);
    }

    [Fact]
    public async Task Booking_spanning_the_window_join_succeeds()
    {
        var room = SplitDayRoom();
        var (bookings, _, _) = TestData.Services(room);

        var result = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = room.Id,
            Start = TestData.Utc(Date, "11:00"),
            Duration = TimeSpan.FromHours(2),
            Booker = TestData.Booker(),
        });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Slots_are_offered_across_the_window_join()
    {
        var room = SplitDayRoom();
        var (_, availability, _) = TestData.Services(room);

        var result = await availability.GetSlotsAsync(room.Id, Date, Date, TimeSpan.FromHours(2));

        Assert.True(result.Succeeded);
        Assert.Contains(result.Value, s => s.StartUtc == TestData.Utc(Date, "11:00"));
    }

    [Fact]
    public async Task Gapped_windows_still_reject_a_spanning_booking()
    {
        var room = TestData.Room(TestData.Config(WeeklyOpenHours.Create(
        [
            (Date.DayOfWeek, TestData.Win("08:00", "12:00")),
            (Date.DayOfWeek, TestData.Win("13:00", "14:00")),
        ]).Value));
        var (bookings, _, _) = TestData.Services(room);

        var result = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = room.Id,
            Start = TestData.Utc(Date, "11:00"),
            Duration = TimeSpan.FromHours(2),
            Booker = TestData.Booker(),
        });

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.OutsideOpenHours, Assert.Single(result.Failures).Code);
    }
}

/// <summary>
/// One code per failed rule: an unaligned out-of-bounds duration reports both
/// codes (bookings spec, "Placement validation pipeline").
/// </summary>
public class DurationFailureAccumulationTests
{
    private static readonly DateOnly Date = TestData.BaseDate;

    [Fact]
    public async Task Unaligned_and_too_short_duration_reports_both_codes()
    {
        var room = TestData.Room();
        var (bookings, _, _) = TestData.Services(room);

        var result = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = room.Id,
            Start = TestData.Utc(Date, "10:00"),
            Duration = TimeSpan.FromMinutes(20),
            Booker = TestData.Booker(),
        });

        Assert.False(result.Succeeded);
        Assert.Equal(
            new[] { FailureCodes.Granularity, FailureCodes.DurationTooShort },
            result.Failures.Select(f => f.Code).ToArray());
    }

    [Fact]
    public async Task Unaligned_and_too_long_duration_reports_both_codes()
    {
        var room = TestData.Room();
        var (bookings, _, _) = TestData.Services(room);

        var result = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = room.Id,
            Start = TestData.Utc(Date, "08:00"),
            Duration = TimeSpan.FromMinutes(490),
            Booker = TestData.Booker(),
        });

        Assert.False(result.Succeeded);
        Assert.Equal(
            new[] { FailureCodes.Granularity, FailureCodes.DurationTooLong },
            result.Failures.Select(f => f.Code).ToArray());
    }
}

/// <summary>
/// Lead-time and horizon boundaries are inclusive: exactly-at-boundary
/// placements are allowed.
/// </summary>
public class BoundaryTests
{
    private static UBookIt.Core.Resources.Resource EveryDayRoom(BookingConstraints? constraints = null)
        => TestData.Room(TestData.Config(
            TestData.Weekly("08:00", "18:00", Enum.GetValues<DayOfWeek>()),
            constraints: constraints));

    [Fact]
    public async Task Start_exactly_at_lead_time_boundary_is_allowed()
    {
        var room = EveryDayRoom(BookingConstraints.Create(leadTime: TimeSpan.FromHours(2)).Value);
        var now = TestData.Utc(TestData.BaseDate, "08:00");
        var (bookings, _, _) = TestData.Services(room, now);

        var result = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = room.Id,
            Start = TestData.Utc(TestData.BaseDate, "10:00"),
            Duration = TimeSpan.FromHours(1),
            Booker = TestData.Booker(),
        });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Start_on_the_last_horizon_day_is_allowed_and_the_day_after_is_not()
    {
        var room = EveryDayRoom();
        var (bookings, _, _) = TestData.Services(room);
        var lastDay = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(TestData.Now, TestData.London).DateTime).AddDays(90);

        var onBoundary = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = room.Id,
            Start = TestData.Utc(lastDay, "10:00"),
            Duration = TimeSpan.FromHours(1),
            Booker = TestData.Booker(),
        });
        var pastBoundary = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = room.Id,
            Start = TestData.Utc(lastDay.AddDays(1), "10:00"),
            Duration = TimeSpan.FromHours(1),
            Booker = TestData.Booker(),
        });

        Assert.True(onBoundary.Succeeded);
        Assert.False(pastBoundary.Succeeded);
        Assert.Equal(FailureCodes.Horizon, Assert.Single(pastBoundary.Failures).Code);
    }
}
