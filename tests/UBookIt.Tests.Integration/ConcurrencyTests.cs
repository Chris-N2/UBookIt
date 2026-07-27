using UBookIt.Core.Common;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

[Collection(SqlServerCollection.Name)]
public class ConcurrencyTests(SqlServerFixture fixture)
{
    /// <summary>
    /// The concurrency proof from the persistence spec: racing conflicting
    /// placements against real SQL Server — exactly one wins.
    /// </summary>
    [Fact]
    public async Task Racing_conflicting_placements_yield_exactly_one_success_and_one_row()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture);
        var start = new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

        var attempts = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(async () =>
        {
            // Each racer gets its own DbContext (its own connection + transaction).
            var store = new SqlBookingStore(fixture.CreateContext());
            return await store.PlaceAsync(Seed.ConfirmedBooking(resourceId, start, TimeSpan.FromHours(1)));
        })));

        Assert.Equal(1, attempts.Count(r => r.Succeeded));
        Assert.All(
            attempts.Where(r => !r.Succeeded),
            r => Assert.Equal(FailureCodes.Conflict, Assert.Single(r.Failures).Code));

        await using var context = fixture.CreateContext();
        Assert.Equal(1, context.Claims.Count(c => c.ResourceId == resourceId));
        Assert.Equal(1, context.Bookings.Count(b => b.Claims.Any(c => c.ResourceId == resourceId)));
    }
}
