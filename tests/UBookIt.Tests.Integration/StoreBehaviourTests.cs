using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

[Collection(SqlServerCollection.Name)]
public class StoreBehaviourTests(SqlServerFixture fixture)
{
    private static readonly DateTimeOffset Nine = new(2026, 9, 15, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Claims_query_uses_half_open_overlap()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        await using var context = fixture.CreateContext();
        var store = new SqlBookingStore(context);
        var placed = await store.PlaceAsync(Seed.ConfirmedBooking(resourceId, Nine, TimeSpan.FromHours(1)), Ct);
        Assert.True(placed.Succeeded);

        // Booking is 09:00–10:00. Query starting exactly at its end: excluded.
        var fromEnd = await store.GetClaimsAsync(resourceId, Nine.AddHours(1), Nine.AddHours(2), Ct);
        Assert.Empty(fromEnd);

        // Query overlapping the last minute: included.
        var overlapping = await store.GetClaimsAsync(resourceId, Nine.AddMinutes(59), Nine.AddHours(2), Ct);
        Assert.Single(overlapping);
    }

    [Fact]
    public async Task Status_change_persists_and_stops_blocking()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var (bookings, _) = fixture.CreateServices(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));

        var placed = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = resourceId,
            Start = Nine,
            Duration = TimeSpan.FromHours(1),
            Booker = Booker.Create(null, "Cancelling Carla", "carla@example.com").Value,
        }, Ct);
        Assert.True(placed.Succeeded);

        var cancelled = await bookings.CancelAsync(placed.Value.Id, Ct);
        Assert.True(cancelled.Succeeded);

        await using var context = fixture.CreateContext();
        var reloaded = await new SqlBookingStore(context).GetBookingAsync(placed.Value.Id, Ct);
        Assert.Equal(BookingStatus.Cancelled, reloaded!.Status);

        // The slot is free again: a replacement placement succeeds.
        var replacement = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = resourceId,
            Start = Nine,
            Duration = TimeSpan.FromHours(1),
            Booker = Booker.Create(null, "Replacing Rob", "rob@example.com").Value,
        }, Ct);
        Assert.True(replacement.Succeeded);
    }

    [Fact]
    public async Task Failed_placement_leaves_no_rows()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        await using (var firstContext = fixture.CreateContext())
        {
            var placed = await new SqlBookingStore(firstContext)
                .PlaceAsync(Seed.ConfirmedBooking(resourceId, Nine, TimeSpan.FromHours(1)), Ct);
            Assert.True(placed.Succeeded);
        }

        var conflicting = Seed.ConfirmedBooking(resourceId, Nine.AddMinutes(30), TimeSpan.FromHours(1));
        await using var secondContext = fixture.CreateContext();
        var result = await new SqlBookingStore(secondContext).PlaceAsync(conflicting, Ct);

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.Conflict, Assert.Single(result.Failures).Code);

        await using var verifyContext = fixture.CreateContext();
        Assert.Equal(1, verifyContext.Claims.Count(c => c.ResourceId == resourceId));
        Assert.Null(await new SqlBookingStore(verifyContext).GetBookingAsync(conflicting.Id, Ct));
    }
}
