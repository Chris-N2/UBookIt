using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

public class ConflictTests
{
    private static readonly DateOnly Date = TestData.BaseDate;

    private static BookingRequest Request(Guid resourceId, string start, TimeSpan duration) => new()
    {
        ResourceId = resourceId,
        Start = TestData.Utc(Date, start),
        Duration = duration,
        Booker = TestData.Booker(),
    };

    [Fact]
    public async Task Back_to_back_bookings_do_not_conflict()
    {
        var room = TestData.Room();
        var (bookings, _, _) = TestData.Services(room);

        var first = await bookings.PlaceAsync(Request(room.Id, "09:00", TimeSpan.FromHours(1)));
        var second = await bookings.PlaceAsync(Request(room.Id, "10:00", TimeSpan.FromHours(1)));

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
    }

    [Fact]
    public async Task One_minute_overlap_conflicts()
    {
        // Spec scenario: existing 09:00–10:00, request 09:59–11:00 → conflict.
        // 09:59 is not granularity-aligned, so to isolate the conflict rule we
        // use a 1-minute granularity resource.
        var room = TestData.Room(TestData.Config(
            TestData.Weekly("08:00", "18:00", Date.DayOfWeek),
            constraints: UBookIt.Core.Availability.BookingConstraints.Create(
                granularity: TimeSpan.FromMinutes(1), minDuration: TimeSpan.FromMinutes(30)).Value));
        var (bookings, _, _) = TestData.Services(room);

        var first = await bookings.PlaceAsync(Request(room.Id, "09:00", TimeSpan.FromHours(1)));
        Assert.True(first.Succeeded);

        var overlapping = await bookings.PlaceAsync(Request(room.Id, "09:59", TimeSpan.FromMinutes(61)));

        Assert.False(overlapping.Succeeded);
        Assert.Equal(FailureCodes.Conflict, Assert.Single(overlapping.Failures).Code);
    }

    [Fact]
    public async Task Cancelled_booking_does_not_block_replacement()
    {
        var room = TestData.Room();
        var (bookings, _, _) = TestData.Services(room);

        var first = await bookings.PlaceAsync(Request(room.Id, "09:00", TimeSpan.FromHours(1)));
        Assert.True(first.Succeeded);
        Assert.True((await bookings.CancelAsync(first.Value.Id)).Succeeded);

        var replacement = await bookings.PlaceAsync(Request(room.Id, "09:00", TimeSpan.FromHours(1)));

        Assert.True(replacement.Succeeded);
    }

    [Fact]
    public async Task Same_interval_on_a_different_resource_does_not_conflict()
    {
        var roomA = TestData.Room();
        var roomB = TestData.Room();
        var resources = new InMemoryResourceStore().Add(roomA).Add(roomB);
        var store = new InMemoryBookingStore();
        var time = new FixedTimeProvider(TestData.Now);
        var bookings = new BookingService(resources, store, time, TestData.Settings);

        var first = await bookings.PlaceAsync(Request(roomA.Id, "09:00", TimeSpan.FromHours(1)));
        var second = await bookings.PlaceAsync(Request(roomB.Id, "09:00", TimeSpan.FromHours(1)));

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
    }
}
