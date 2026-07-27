using UBookIt.Core.Bookings;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// The full Core-services-over-SQL-stores flow: place → free time reflects
/// the booking → cancel → free time restored.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class EndToEndTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Place_then_cancel_reflects_in_availability()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture);
        var date = new DateOnly(2026, 9, 17);
        var (bookings, availability) = fixture.CreateServices(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));

        DateTimeOffset At(int hour) => new(2026, 9, 17, hour, 0, 0, TimeSpan.Zero);

        var placed = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = resourceId,
            Start = At(10),
            Duration = TimeSpan.FromHours(2),
            Booker = Booker.Create(null, "End ToEnd", "e2e@example.com").Value,
        });
        Assert.True(placed.Succeeded);

        var during = await availability.GetFreeTimeAsync(resourceId, date, date);
        Assert.True(during.Succeeded);
        Assert.Equal(2, during.Value.Count);
        Assert.Equal((At(8), At(10)), (during.Value[0].StartUtc, during.Value[0].EndUtc));
        Assert.Equal((At(12), At(18)), (during.Value[1].StartUtc, during.Value[1].EndUtc));

        Assert.True((await bookings.CancelAsync(placed.Value.Id)).Succeeded);

        var after = await availability.GetFreeTimeAsync(resourceId, date, date);
        Assert.True(after.Succeeded);
        var window = Assert.Single(after.Value);
        Assert.Equal((At(8), At(18)), (window.StartUtc, window.EndUtc));
    }
}
