using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Services;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// The resolution chain over real SQL Server (service-booking spec, "Resolution
/// reports why each resource was excluded", and resource-management spec,
/// "Preview agrees with candidate resolution").
/// <para>
/// The preview endpoint's own assertion of that equivalence lives in the unit
/// suite with every other management endpoint — the mapping from chain to DTO is
/// pure. What only real SQL can prove is the half beneath it: that the chain
/// narrows correctly over resources the read port hydrated and ordered. That is
/// exactly where ⑧'s QA found a defect, when a dropped <c>Include</c> left every
/// capability set empty while the whole unit suite stayed green.
/// </para>
/// </summary>
[Collection(SqlServerCollection.Name)]
public class ServicePreviewTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class FixedIntegrationClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    }

    private static ServiceBookingService BuildResolution(Persistence.UBookItDbContext context)
    {
        var settings = new SiteBookingSettings { TimeZoneId = "UTC" };
        var time = new FixedIntegrationClock();
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

    [Fact]
    public async Task The_chains_final_stage_is_exactly_what_the_booking_path_resolves()
    {
        fixture.EnsureAvailable();

        // A type nobody else in the suite uses, so concurrently-seeded resources
        // cannot join this pool and make the counts non-deterministic.
        //
        // The prefix is deliberately hyphen-free. `ResourceManagementStoreTests`
        // asserts the SQL store's type ordering equals StringComparer.Ordinal's
        // over EVERY key in the shared fixture database, and the two disagree
        // whenever a hyphen in one key aligns with a letter in another: Ordinal
        // ranks '-' (0x2D) below any letter, while a word-sort collation such as
        // Latin1_General_CI_AS gives punctuation a lower weight and compares the
        // following characters instead. `prev-<hex>` against `prevc-<hex>` hit
        // exactly that, failing intermittently on the leading hex digit and only
        // on servers whose collation word-sorts — the column pins no collation,
        // so this is machine-dependent. Sharing a prefix is fine; a hyphen
        // opposite a letter is not.
        var type = $"prevtype{Guid.NewGuid():N}"[..20];

        // Two rooms capped at two hours, one that can go all day. A four-hour
        // service therefore resolves to exactly one of the three, and the two
        // that drop out do so at the duration stage rather than the capability
        // stage — which is the distinction the whole chain exists to make.
        var shortA = await Seed.EveryDayRoomAsync(fixture, Ct, type, maxDurationMinutes: 120);
        var shortB = await Seed.EveryDayRoomAsync(fixture, Ct, type, maxDurationMinutes: 120);
        var long1 = await Seed.EveryDayRoomAsync(fixture, Ct, type, maxDurationMinutes: 480);

        var duration = ServiceDuration.Fixed(TimeSpan.FromHours(4)).Value;
        var role = ServiceRole.Create(type, []).Value;
        var service = Service.Create("Workshop", duration, [role]).Value;

        await using (var write = fixture.CreateContext())
        {
            Assert.True((await new SqlServiceManagementStore(write).CreateAsync(service, Ct)).Succeeded);
        }

        await using var context = fixture.CreateContext();
        var resolution = BuildResolution(context);

        // The chain asked for the configuration directly, as the preview asks it;
        // the pool asked by service id, as the booking path asks it.
        var chain = await resolution.ResolveAsync(role, duration, Ct);
        var pool = await resolution.ResolveCandidatesAsync(service.Id, Ct);

        Assert.True(pool.Succeeded);
        Assert.Equal(
            Assert.Single(pool.Value).Candidates.Select(c => c.ResourceId).OrderBy(id => id),
            chain.Candidates.Select(c => c.ResourceId).OrderBy(id => id));

        // Not a vacuous agreement: the chain narrows, and it narrows at the
        // duration stage. Two identical empty lists would satisfy the equality
        // above while telling us nothing.
        Assert.Equal(3, chain.OfType.Count);
        Assert.Equal(3, chain.WithCapabilities.Count);
        Assert.Equal([long1], chain.Candidates.Select(c => c.ResourceId));

        Assert.Equal(
            new[] { shortA, shortB }.OrderBy(id => id),
            chain.DurationExclusions.Select(e => e.Resource.Id).OrderBy(id => id));
        Assert.All(chain.DurationExclusions, e =>
        {
            Assert.Equal(DurationExclusionReason.ResourceMaximum, e.Reason);
            Assert.Equal(TimeSpan.FromMinutes(120), e.Bound);
        });
    }

    [Fact]
    public async Task Capabilities_hydrated_by_the_read_port_narrow_the_second_stage()
    {
        // If an Include were dropped, every capability set would read as empty
        // and this stage would silently match the whole type — the shape of the
        // defect ⑧'s QA found on the paged reads.
        fixture.EnsureAvailable();

        var type = $"prevcaps{Guid.NewGuid():N}"[..20];
        var tagged = await Seed.EveryDayRoomAsync(fixture, Ct, type);
        var untagged = await Seed.EveryDayRoomAsync(fixture, Ct, type);

        await using (var write = fixture.CreateContext())
        {
            write.ResourceCapabilities.Add(new Persistence.Entities.ResourceCapabilityRow
            {
                ResourceId = tagged,
                Key = "projector",
            });
            await write.SaveChangesAsync(Ct);
        }

        await using var context = fixture.CreateContext();

        var chain = await BuildResolution(context).ResolveAsync(
            ServiceRole.Create(type, ["projector"]).Value,
            ServiceDuration.Fixed(TimeSpan.FromHours(1)).Value,
            Ct);

        Assert.Equal(2, chain.OfType.Count);
        Assert.Equal([tagged], chain.WithCapabilities.Select(r => r.Id));
        Assert.DoesNotContain(untagged, chain.Candidates.Select(c => c.ResourceId));
    }
}
