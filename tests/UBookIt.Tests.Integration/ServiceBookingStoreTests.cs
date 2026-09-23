using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Services;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// The two port methods booking-via-service added, against real SQL Server: the
/// batched claims read and the type-filtered resource listing. Both are asserted
/// against their single-resource counterparts, which is the contract — a
/// divergence here is invisible to Core tests using in-memory doubles.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class ServiceBookingStoreTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Day = new(2026, 9, 16, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Batched_claims_match_the_single_resource_reads()
    {
        fixture.EnsureAvailable();

        var ids = new[]
        {
            await Seed.EveryDayRoomAsync(fixture, Ct),
            await Seed.EveryDayRoomAsync(fixture, Ct),
            await Seed.EveryDayRoomAsync(fixture, Ct),
        };

        await using var context = fixture.CreateContext();
        var store = new SqlBookingStore(context);

        // One booking on each of the first two; the third stays empty so the
        // batch must not invent claims for it.
        foreach (var id in ids.Take(2))
        {
            var placed = await store.PlaceAsync(
                Seed.ConfirmedBooking(id, Day.AddHours(10), TimeSpan.FromHours(1)), Ct);
            Assert.True(placed.Succeeded);
        }

        var from = Day;
        var to = Day.AddDays(1);

        var batched = await store.GetClaimsAsync(ids, from, to, Ct);

        var perResource = new List<ClaimInfo>();
        foreach (var id in ids)
        {
            perResource.AddRange(await store.GetClaimsAsync(id, from, to, Ct));
        }

        Assert.Equal(
            perResource.Select(c => (c.ResourceId, c.BookingId)).OrderBy(x => x).ToArray(),
            batched.Select(c => (c.ResourceId, c.BookingId)).OrderBy(x => x).ToArray());

        Assert.Equal(2, batched.Count);
        Assert.DoesNotContain(batched, c => c.ResourceId == ids[2]);
    }

    [Fact]
    public async Task Batched_claims_carry_each_claims_own_resource_id()
    {
        fixture.EnsureAvailable();

        var ids = new[]
        {
            await Seed.EveryDayRoomAsync(fixture, Ct),
            await Seed.EveryDayRoomAsync(fixture, Ct),
        };

        await using var context = fixture.CreateContext();
        var store = new SqlBookingStore(context);

        await store.PlaceAsync(Seed.ConfirmedBooking(ids[0], Day.AddHours(9), TimeSpan.FromHours(1)), Ct);
        await store.PlaceAsync(Seed.ConfirmedBooking(ids[1], Day.AddHours(11), TimeSpan.FromHours(1)), Ct);

        var batched = await store.GetClaimsAsync(ids, Day, Day.AddDays(1), Ct);

        // A single projection over several resources must not stamp every row
        // with the same id — the per-resource read cannot catch that.
        Assert.Equal(2, batched.Select(c => c.ResourceId).Distinct().Count());
    }

    [Fact]
    public async Task Batched_claims_of_an_empty_id_set_read_nothing()
    {
        fixture.EnsureAvailable();

        await using var context = fixture.CreateContext();
        var store = new SqlBookingStore(context);

        Assert.Empty(await store.GetClaimsAsync([], Day, Day.AddDays(1), Ct));
    }

    [Fact]
    public async Task Type_listing_returns_complete_aggregates_of_that_type_only()
    {
        fixture.EnsureAvailable();

        var type = $"kind-{Guid.NewGuid():N}"[..12];
        var wanted = new[]
        {
            await Seed.EveryDayRoomAsync(fixture, Ct, type: type),
            await Seed.EveryDayRoomAsync(fixture, Ct, type: type),
        };
        var other = await Seed.EveryDayRoomAsync(fixture, Ct);

        await using var context = fixture.CreateContext();
        var store = new SqlResourceStore(context, new SqlSiteClosureStore(context));

        var listed = await store.ListByTypeAsync(type, Ct);

        Assert.Equal(wanted.OrderBy(x => x), listed.Select(r => r.Id).OrderBy(x => x));
        Assert.DoesNotContain(other, listed.Select(r => r.Id));

        // Complete enough to compute availability from without a second load:
        // the seeded rooms open every day, so every day carries a window.
        Assert.All(listed, r => Assert.All(
            Enum.GetValues<DayOfWeek>(),
            day => Assert.NotEmpty(r.Availability.OpenHours.WindowsFor(day))));
    }

    [Fact]
    public async Task Type_listing_is_not_paged()
    {
        fixture.EnsureAvailable();

        // Above the paged list's clamp of 500 would be slow to seed here; the
        // contract that matters is that no paging parameter exists to truncate
        // with, and that everything seeded comes back.
        var type = $"kind-{Guid.NewGuid():N}"[..12];
        var seeded = new List<Guid>();
        for (var i = 0; i < 12; i++)
        {
            seeded.Add(await Seed.EveryDayRoomAsync(fixture, Ct, type: type));
        }

        await using var context = fixture.CreateContext();
        var listed = await new SqlResourceStore(context, new SqlSiteClosureStore(context)).ListByTypeAsync(type, Ct);

        Assert.Equal(seeded.Count, listed.Count);
    }

    [Fact]
    public async Task Racing_service_placements_resolve_to_distinct_resources()
    {
        fixture.EnsureAvailable();

        var type = $"kind-{Guid.NewGuid():N}"[..12];
        var poolIds = new[]
        {
            await Seed.EveryDayRoomAsync(fixture, Ct, type: type, granularityMinutes: 60, minDurationMinutes: 60),
            await Seed.EveryDayRoomAsync(fixture, Ct, type: type, granularityMinutes: 60, minDurationMinutes: 60),
        };

        var service = Service.Create("Consultation", ServiceDuration.Fixed(TimeSpan.FromHours(1)).Value,
            [new ServiceRole(type, 1)]).Value;

        await using (var seedContext = fixture.CreateContext())
        {
            Assert.True((await new SqlServiceManagementStore(seedContext).CreateAsync(service, Ct)).Succeeded);
        }

        var start = Day.AddHours(10);

        // Three racers over a pool of two: each gets its own contexts, so the
        // candidate loop races for real rather than through a shared connection.
        var attempts = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Task.Run(async () =>
        {
            await using var racerContext = fixture.CreateContext();
            var booking = BuildServiceBooking(racerContext, start);

            return await booking.PlaceAsync(
                new ServiceBookingRequest
                {
                    ServiceId = service.Id,
                    Start = start,
                    Duration = TimeSpan.FromHours(1),
                    Booker = Booker.Create(null, "Racer", "racer@example.com").Value,
                },
                Ct);
        }, Ct)));

        var succeeded = attempts.Where(a => a.Succeeded).ToList();

        Assert.Equal(2, succeeded.Count);
        Assert.Equal(
            2,
            succeeded.Select(a => a.Value.Claims[0].ResourceId).Distinct().Count());
        Assert.All(succeeded, a => Assert.Contains(a.Value.Claims[0].ResourceId, poolIds));

        var loser = Assert.Single(attempts, a => !a.Succeeded);
        Assert.Equal(FailureCodes.Conflict, Assert.Single(loser.Failures).Code);

        // Two bookings, not three, and one per resource.
        await using var check = fixture.CreateContext();
        Assert.Equal(1, check.Claims.Count(c => c.ResourceId == poolIds[0]));
        Assert.Equal(1, check.Claims.Count(c => c.ResourceId == poolIds[1]));
    }

    private static ServiceBookingService BuildServiceBooking(
        Persistence.UBookItDbContext context, DateTimeOffset nowUtc)
    {
        var settings = new SiteBookingSettings { TimeZoneId = "UTC" };
        var time = new FixedTimeProvider(nowUtc.AddDays(-1));
        var resources = new SqlResourceStore(context, new SqlSiteClosureStore(context));
        var bookings = new SqlBookingStore(context);

        return new ServiceBookingService(
            new SqlServiceStore(context),
            resources,
            bookings,
            new AvailabilityService(resources, bookings, time, settings),
            new BookingService(resources, bookings, time, settings),
            settings);
    }
}
