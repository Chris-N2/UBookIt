using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

public class AvailabilityTests
{
    private static readonly DateOnly Date = TestData.BaseDate;

    [Fact]
    public async Task Open_day_without_bookings_is_fully_free()
    {
        var room = TestData.Room();
        var (_, availability, _) = TestData.Services(room);

        var result = await availability.GetFreeTimeAsync(room.Id, Date, Date);

        Assert.True(result.Succeeded);
        var window = Assert.Single(result.Value);
        Assert.Equal(TestData.Utc(Date, "08:00"), window.StartUtc);
        Assert.Equal(TestData.Utc(Date, "18:00"), window.EndUtc);
    }

    [Fact]
    public async Task Closed_day_has_no_free_time()
    {
        var room = TestData.Room();
        var (_, availability, _) = TestData.Services(room);
        var closedDay = Date.AddDays(1); // pattern only opens BaseDate's day of week

        var result = await availability.GetFreeTimeAsync(room.Id, closedDay, closedDay);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task Closure_exception_removes_availability()
    {
        var room = TestData.Room(TestData.Config(
            TestData.Weekly("08:00", "18:00", Date.DayOfWeek),
            exceptions: [DateException.Closure(Date)]));
        var (_, availability, _) = TestData.Services(room);

        var result = await availability.GetFreeTimeAsync(room.Id, Date, Date);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task Override_exception_replaces_the_days_windows()
    {
        var room = TestData.Room(TestData.Config(
            TestData.Weekly("08:00", "18:00", Date.DayOfWeek),
            exceptions: [DateException.Override(Date, [TestData.Win("08:00", "22:00")]).Value]));
        var (_, availability, _) = TestData.Services(room);

        var result = await availability.GetFreeTimeAsync(room.Id, Date, Date);

        Assert.True(result.Succeeded);
        var window = Assert.Single(result.Value);
        Assert.Equal(TestData.Utc(Date, "22:00"), window.EndUtc);
    }

    [Fact]
    public async Task Confirmed_booking_splits_the_free_window()
    {
        var room = TestData.Room();
        var (bookings, availability, _) = TestData.Services(room);

        var placed = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = room.Id,
            Start = TestData.Utc(Date, "10:00"),
            Duration = TimeSpan.FromHours(1),
            Booker = TestData.Booker(),
        });
        Assert.True(placed.Succeeded);

        var result = await availability.GetFreeTimeAsync(room.Id, Date, Date);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal((TestData.Utc(Date, "08:00"), TestData.Utc(Date, "10:00")),
            (result.Value[0].StartUtc, result.Value[0].EndUtc));
        Assert.Equal((TestData.Utc(Date, "11:00"), TestData.Utc(Date, "18:00")),
            (result.Value[1].StartUtc, result.Value[1].EndUtc));
    }

    [Fact]
    public async Task Cancelled_booking_does_not_reduce_free_time()
    {
        var room = TestData.Room();
        var (bookings, availability, _) = TestData.Services(room);

        var placed = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = room.Id,
            Start = TestData.Utc(Date, "10:00"),
            Duration = TimeSpan.FromHours(1),
            Booker = TestData.Booker(),
        });
        Assert.True(placed.Succeeded);
        Assert.True((await bookings.CancelAsync(placed.Value.Id)).Succeeded);

        var result = await availability.GetFreeTimeAsync(room.Id, Date, Date);

        Assert.True(result.Succeeded);
        var window = Assert.Single(result.Value);
        Assert.Equal(TestData.Utc(Date, "08:00"), window.StartUtc);
        Assert.Equal(TestData.Utc(Date, "18:00"), window.EndUtc);
    }

    [Fact]
    public async Task Unknown_resource_reports_resource_not_found()
    {
        var (_, availability, _) = TestData.Services(TestData.Room());

        var result = await availability.GetFreeTimeAsync(Guid.NewGuid(), Date, Date);

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.ResourceNotFound, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public async Task Inverted_date_range_is_rejected()
    {
        var room = TestData.Room();
        var (_, availability, _) = TestData.Services(room);

        var result = await availability.GetFreeTimeAsync(room.Id, Date, Date.AddDays(-1));

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.DateRangeInvalid, Assert.Single(result.Failures).Code);
    }
}
