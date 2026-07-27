using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

[Collection(SqlServerCollection.Name)]
public class StoreBehaviourTests(SqlServerFixture fixture)
{
    private static readonly DateTimeOffset Nine = new(2026, 9, 15, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Claims_query_uses_half_open_overlap()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture);
        var store = new SqlBookingStore(fixture.CreateContext());
        Assert.True((await store.PlaceAsync(Seed.ConfirmedBooking(resourceId, Nine, TimeSpan.FromHours(1)))).Succeeded);

        // Booking is 09:00–10:00. Query starting exactly at its end: excluded.
        var fromEnd = await store.GetClaimsAsync(resourceId, Nine.AddHours(1), Nine.AddHours(2));
        Assert.Empty(fromEnd);

        // Query overlapping the last minute: included.
        var overlapping = await store.GetClaimsAsync(resourceId, Nine.AddMinutes(59), Nine.AddHours(2));
        Assert.Single(overlapping);
    }

    [Fact]
    public async Task Status_change_persists_and_stops_blocking()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture);
        var (bookings, _) = fixture.CreateServices(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));

        var placed = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = resourceId,
            Start = Nine,
            Duration = TimeSpan.FromHours(1),
            Booker = Booker.Create(null, "Cancelling Carla", "carla@example.com").Value,
        });
        Assert.True(placed.Succeeded);

        var cancelled = await bookings.CancelAsync(placed.Value.Id);
        Assert.True(cancelled.Succeeded);

        var reloaded = await new SqlBookingStore(fixture.CreateContext()).GetBookingAsync(placed.Value.Id);
        Assert.Equal(BookingStatus.Cancelled, reloaded!.Status);

        // The slot is free again: a replacement placement succeeds.
        var replacement = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = resourceId,
            Start = Nine,
            Duration = TimeSpan.FromHours(1),
            Booker = Booker.Create(null, "Replacing Rob", "rob@example.com").Value,
        });
        Assert.True(replacement.Succeeded);
    }

    [Fact]
    public async Task Failed_placement_leaves_no_rows()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture);
        var store = new SqlBookingStore(fixture.CreateContext());
        Assert.True((await store.PlaceAsync(Seed.ConfirmedBooking(resourceId, Nine, TimeSpan.FromHours(1)))).Succeeded);

        var conflicting = Seed.ConfirmedBooking(resourceId, Nine.AddMinutes(30), TimeSpan.FromHours(1));
        var result = await new SqlBookingStore(fixture.CreateContext()).PlaceAsync(conflicting);

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.Conflict, Assert.Single(result.Failures).Code);

        await using var context = fixture.CreateContext();
        Assert.Equal(1, context.Claims.Count(c => c.ResourceId == resourceId));
        Assert.Null(await new SqlBookingStore(fixture.CreateContext()).GetBookingAsync(conflicting.Id));
    }
}
