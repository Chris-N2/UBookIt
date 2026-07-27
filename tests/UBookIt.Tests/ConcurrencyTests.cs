using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

public class ConcurrencyTests
{
    [Fact]
    public async Task Racing_conflicting_placements_yield_exactly_one_success()
    {
        var room = TestData.Room();
        var (bookings, _, _) = TestData.Services(room);
        var start = TestData.Utc(TestData.BaseDate, "10:00");

        var attempts = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() =>
            bookings.PlaceAsync(new BookingRequest
            {
                ResourceId = room.Id,
                Start = start,
                Duration = TimeSpan.FromHours(1),
                Booker = TestData.Booker(),
            }))));

        var successes = attempts.Count(r => r.Succeeded);
        var conflicts = attempts.Where(r => !r.Succeeded).ToList();

        Assert.Equal(1, successes);
        Assert.Equal(19, conflicts.Count);
        Assert.All(conflicts, r => Assert.Equal(FailureCodes.Conflict, Assert.Single(r.Failures).Code));
    }
}
