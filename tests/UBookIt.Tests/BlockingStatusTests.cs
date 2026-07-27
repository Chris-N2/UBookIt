using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Covers the full blocking rule: claims block iff their booking is Requested
/// or Confirmed; Cancelled and Declined never block (bookings spec, "Conflict
/// semantics"; availability spec, "Free-time computation").
/// </summary>
public class BlockingStatusTests
{
    private static readonly DateOnly Date = TestData.BaseDate;

    private static Booking BookingIn(BookingStatus status, Guid resourceId, string start, string end)
        => Booking.Create(
            Guid.NewGuid(),
            BookingInterval.Create(TestData.Utc(Date, start), TestData.Utc(Date, end), TestData.LondonZoneId).Value,
            TestData.Booker(),
            [new ResourceClaim(resourceId)],
            status,
            TestData.Now);

    [Fact]
    public async Task Requested_booking_reduces_free_time()
    {
        var room = TestData.Room();
        var (_, availability, store) = TestData.Services(room);
        var seeded = await store.PlaceAsync(BookingIn(BookingStatus.Requested, room.Id, "10:00", "11:00"));
        Assert.True(seeded.Succeeded);

        var result = await availability.GetFreeTimeAsync(room.Id, Date, Date);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal(TestData.Utc(Date, "10:00"), result.Value[0].EndUtc);
        Assert.Equal(TestData.Utc(Date, "11:00"), result.Value[1].StartUtc);
    }

    [Fact]
    public async Task Requested_booking_causes_conflict_on_placement()
    {
        var room = TestData.Room();
        var (bookings, _, store) = TestData.Services(room);
        var seeded = await store.PlaceAsync(BookingIn(BookingStatus.Requested, room.Id, "10:00", "11:00"));
        Assert.True(seeded.Succeeded);

        var result = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = room.Id,
            Start = TestData.Utc(Date, "10:30"),
            Duration = TimeSpan.FromHours(1),
            Booker = TestData.Booker(),
        });

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.Conflict, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public async Task Declined_booking_does_not_reduce_free_time()
    {
        var room = TestData.Room();
        var (_, availability, store) = TestData.Services(room);
        var seeded = await store.PlaceAsync(BookingIn(BookingStatus.Declined, room.Id, "10:00", "11:00"));
        Assert.True(seeded.Succeeded);

        var result = await availability.GetFreeTimeAsync(room.Id, Date, Date);

        Assert.True(result.Succeeded);
        var window = Assert.Single(result.Value);
        Assert.Equal(TestData.Utc(Date, "08:00"), window.StartUtc);
        Assert.Equal(TestData.Utc(Date, "18:00"), window.EndUtc);
    }

    [Fact]
    public async Task Declined_booking_does_not_block_placement()
    {
        var room = TestData.Room();
        var (bookings, _, store) = TestData.Services(room);
        var seeded = await store.PlaceAsync(BookingIn(BookingStatus.Declined, room.Id, "10:00", "11:00"));
        Assert.True(seeded.Succeeded);

        var result = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = room.Id,
            Start = TestData.Utc(Date, "10:00"),
            Duration = TimeSpan.FromHours(1),
            Booker = TestData.Booker(),
        });

        Assert.True(result.Succeeded);
    }
}
