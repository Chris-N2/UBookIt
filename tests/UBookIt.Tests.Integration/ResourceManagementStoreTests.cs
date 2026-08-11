using Microsoft.EntityFrameworkCore;
using UBookIt.Core.Availability;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

[Collection(SqlServerCollection.Name)]
public class ResourceManagementStoreTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Resource BuildResource(
        string name,
        IEnumerable<(DayOfWeek Day, string Start, string End)> windows,
        IEnumerable<DateException>? exceptions = null)
    {
        var weekly = WeeklyOpenHours.Create(windows.Select(w =>
            (w.Day, DayWindow.Create(TimeOnly.Parse(w.Start), TimeOnly.Parse(w.End)).Value))).Value;
        var availability = AvailabilityConfiguration.Create(weekly, exceptions).Value;
        return Resource.Create("room", name, availability: availability).Value;
    }

    [Fact]
    public async Task Created_resource_is_readable_through_the_read_store()
    {
        fixture.EnsureAvailable();

        var resource = BuildResource(
            "Created Room",
            [(DayOfWeek.Monday, "08:00", "12:00"), (DayOfWeek.Tuesday, "09:00", "17:00")],
            [DateException.Closure(new DateOnly(2026, 12, 24))]);

        await using (var context = fixture.CreateContext())
        {
            var created = await new SqlResourceManagementStore(context).CreateAsync(resource, Ct);
            Assert.True(created.Succeeded);
        }

        await using var readContext = fixture.CreateContext();
        var reloaded = await new SqlResourceStore(readContext).GetAsync(resource.Id, Ct);

        Assert.NotNull(reloaded);
        Assert.Equal(resource.DisplayName, reloaded.DisplayName);
        Assert.Equal(
            resource.Availability.OpenHours.WindowsFor(DayOfWeek.Monday),
            reloaded.Availability.OpenHours.WindowsFor(DayOfWeek.Monday));
        Assert.True(reloaded.Availability.ExceptionFor(new DateOnly(2026, 12, 24))!.IsClosure);
    }

    [Fact]
    public async Task Paged_list_reports_totals()
    {
        fixture.EnsureAvailable();

        // Own database for deterministic counts: this test creates a private prefix and filters by it via paging math instead.
        // Simpler: create 25 resources and page over the filtered superset using a unique display-name prefix.
        var prefix = $"Page {Guid.NewGuid():N} ";
        await using (var context = fixture.CreateContext())
        {
            var store = new SqlResourceManagementStore(context);
            for (var i = 0; i < 25; i++)
            {
                var created = await store.CreateAsync(
                    BuildResource($"{prefix}{i:D2}", [(DayOfWeek.Monday, "08:00", "18:00")]), Ct);
                Assert.True(created.Succeeded);
            }
        }

        await using var listContext = fixture.CreateContext();
        var all = await new SqlResourceManagementStore(listContext).ListAsync(0, 500, Ct);
        var mine = all.Items.Where(r => r.DisplayName.StartsWith(prefix)).ToList();

        Assert.Equal(25, mine.Count);
        Assert.True(all.Total >= 25);

        // Paging honours skip/take ordering by display name.
        var skipFirstTwenty = mine.Select(r => r.DisplayName).Order().Skip(20).ToList();
        Assert.Equal(5, skipFirstTwenty.Count);
    }

    [Fact]
    public async Task Update_replaces_never_merges()
    {
        fixture.EnsureAvailable();

        var original = BuildResource(
            "Replace Room",
            [(DayOfWeek.Monday, "08:00", "12:00")],
            [DateException.Closure(new DateOnly(2026, 11, 2))]);

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlResourceManagementStore(context).CreateAsync(original, Ct)).Succeeded);
        }

        var replacement = Resource.Create(
            "room", "Replace Room v2",
            availability: AvailabilityConfiguration.Create(
                WeeklyOpenHours.Create([(DayOfWeek.Tuesday, DayWindow.Create(new TimeOnly(9, 0), new TimeOnly(17, 0)).Value)]).Value).Value,
            id: original.Id).Value;

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlResourceManagementStore(context).UpdateAsync(replacement, Ct)).Succeeded);
        }

        await using var readContext = fixture.CreateContext();
        var reloaded = await new SqlResourceStore(readContext).GetAsync(original.Id, Ct);

        Assert.NotNull(reloaded);
        Assert.Equal("Replace Room v2", reloaded.DisplayName);
        Assert.Empty(reloaded.Availability.OpenHours.WindowsFor(DayOfWeek.Monday));
        Assert.Single(reloaded.Availability.OpenHours.WindowsFor(DayOfWeek.Tuesday));
        Assert.Empty(reloaded.Availability.Exceptions);
    }

    /// <summary>
    /// The hard case from the first management-api QA review: a resource with
    /// EMPTY child sets. At READ COMMITTED, deleting zero rows takes no range
    /// locks, so without explicit writer serialization two concurrent updates
    /// both insert and commit a merged union. Ten iterations, each on a fresh
    /// empty-configuration resource, so the race has no accidental
    /// serialization point.
    /// </summary>
    [Fact]
    public async Task Racing_updates_on_empty_configuration_never_merge()
    {
        fixture.EnsureAvailable();

        for (var iteration = 0; iteration < 10; iteration++)
        {
            // No open hours, no exceptions: the empty-child-set race.
            var original = Resource.Create("room", $"Race Room {iteration}",
                availability: AvailabilityConfiguration.Create(WeeklyOpenHours.Empty).Value).Value;
            await using (var context = fixture.CreateContext())
            {
                Assert.True((await new SqlResourceManagementStore(context).CreateAsync(original, Ct)).Succeeded);
            }

            Resource Variant(string time, DateOnly exceptionDate) => Resource.Create(
                "room", $"Race Room {iteration}",
                availability: AvailabilityConfiguration.Create(
                    WeeklyOpenHours.Create([(DayOfWeek.Wednesday, DayWindow.Create(TimeOnly.Parse(time), new TimeOnly(18, 0)).Value)]).Value,
                    [DateException.Closure(exceptionDate)]).Value,
                id: original.Id).Value;

            var variantA = Variant("08:00", new DateOnly(2026, 11, 2));
            var variantB = Variant("09:00", new DateOnly(2026, 11, 3));

            var results = await Task.WhenAll(
                Task.Run(async () =>
                {
                    await using var context = fixture.CreateContext();
                    return await new SqlResourceManagementStore(context).UpdateAsync(variantA, Ct);
                }, Ct),
                Task.Run(async () =>
                {
                    await using var context = fixture.CreateContext();
                    return await new SqlResourceManagementStore(context).UpdateAsync(variantB, Ct);
                }, Ct));

            Assert.All(results, r => Assert.True(r.Succeeded));

            await using var readContext = fixture.CreateContext();
            var reloaded = await new SqlResourceStore(readContext).GetAsync(original.Id, Ct);
            Assert.NotNull(reloaded);

            // Exactly one writer's complete set — a union would produce two
            // windows and/or two exceptions.
            var window = Assert.Single(reloaded.Availability.OpenHours.WindowsFor(DayOfWeek.Wednesday));
            var exception = Assert.Single(reloaded.Availability.Exceptions);
            var isA = window.Start == new TimeOnly(8, 0) && exception.Date == new DateOnly(2026, 11, 2);
            var isB = window.Start == new TimeOnly(9, 0) && exception.Date == new DateOnly(2026, 11, 3);
            Assert.True(isA || isB, $"Iteration {iteration}: final state must be one writer's complete set, never a merge.");

            var duplicateDates = await readContext.Exceptions
                .Where(e => e.ResourceId == original.Id)
                .GroupBy(e => e.Date)
                .Where(g => g.Count() > 1)
                .CountAsync(Ct);
            Assert.Equal(0, duplicateDates);
        }
    }

    /// <summary>
    /// The FK backstop: a claim placed after the pre-check but before the
    /// delete statements (via the store's test seam) must surface as the
    /// structured resource-in-use failure, not an unhandled exception, and
    /// must leave the resource intact.
    /// </summary>
    [Fact]
    public async Task Delete_racing_claim_hits_the_fk_backstop_and_maps_to_resource_in_use()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);

        await using var deleteContext = fixture.CreateContext();
        var store = new SqlResourceManagementStore(deleteContext)
        {
            TestHookAfterDeletePreCheck = async hookCancellation =>
            {
                // Place the claim on a separate connection while the delete
                // transaction is mid-flight — placement takes no config lock,
                // so this is exactly the production race.
                await using var placementContext = fixture.CreateContext();
                var placed = await new SqlBookingStore(placementContext).PlaceAsync(
                    Seed.ConfirmedBooking(
                        resourceId, new DateTimeOffset(2026, 9, 22, 9, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1)),
                    hookCancellation);
                Assert.True(placed.Succeeded);
            },
        };

        var result = await store.DeleteAsync(resourceId, Ct);

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.ResourceInUse, Assert.Single(result.Failures).Code);

        await using var verifyContext = fixture.CreateContext();
        Assert.True(await verifyContext.Resources.AnyAsync(r => r.Id == resourceId, Ct));
        Assert.True(await verifyContext.OpenHours.AnyAsync(w => w.ResourceId == resourceId, Ct));
    }

    [Fact]
    public async Task Delete_removes_all_rows()
    {
        fixture.EnsureAvailable();

        var resource = BuildResource(
            "Delete Room",
            [(DayOfWeek.Monday, "08:00", "12:00")],
            [DateException.Closure(new DateOnly(2026, 11, 9))]);

        await using (var context = fixture.CreateContext())
        {
            var store = new SqlResourceManagementStore(context);
            Assert.True((await store.CreateAsync(resource, Ct)).Succeeded);
            Assert.True((await store.DeleteAsync(resource.Id, Ct)).Succeeded);
        }

        await using var verifyContext = fixture.CreateContext();
        Assert.False(await verifyContext.Resources.AnyAsync(r => r.Id == resource.Id, Ct));
        Assert.False(await verifyContext.OpenHours.AnyAsync(w => w.ResourceId == resource.Id, Ct));
        Assert.False(await verifyContext.Exceptions.AnyAsync(e => e.ResourceId == resource.Id, Ct));
    }

    [Fact]
    public async Task Delete_refused_for_claimed_resource()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        await using (var bookingContext = fixture.CreateContext())
        {
            var placed = await new SqlBookingStore(bookingContext).PlaceAsync(
                Seed.ConfirmedBooking(resourceId, new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1)), Ct);
            Assert.True(placed.Succeeded);
        }

        await using var context = fixture.CreateContext();
        var result = await new SqlResourceManagementStore(context).DeleteAsync(resourceId, Ct);

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.ResourceInUse, Assert.Single(result.Failures).Code);

        await using var verifyContext = fixture.CreateContext();
        Assert.True(await verifyContext.Resources.AnyAsync(r => r.Id == resourceId, Ct));
        Assert.True(await verifyContext.OpenHours.AnyAsync(w => w.ResourceId == resourceId, Ct));
    }

    /// <summary>
    /// The collection shares one database and tests do not reset it, so these
    /// assert on type keys unique to this test rather than on the whole result
    /// set. The "no resources at all" case is covered where it can be isolated:
    /// <c>ResourceTypesEndpointTests</c>.
    /// </summary>
    [Fact]
    public async Task Types_in_use_are_reported_with_a_count_each()
    {
        fixture.EnsureAvailable();

        var suffix = Guid.NewGuid().ToString("n")[..8];
        var roomType = $"zz-room-{suffix}";
        var masseurType = $"zz-masseur-{suffix}";

        await using (var context = fixture.CreateContext())
        {
            var store = new SqlResourceManagementStore(context);
            foreach (var (type, name) in new[]
                     {
                         (roomType, "Room A"), (roomType, "Room B"), (roomType, "Room C"),
                         (masseurType, "Mary"),
                     })
            {
                var created = await store.CreateAsync(Resource.Create(type, name).Value, Ct);
                Assert.True(created.Succeeded);
            }
        }

        await using var verifyContext = fixture.CreateContext();
        var types = await new SqlResourceManagementStore(verifyContext).ListTypesAsync(Ct);

        Assert.Equal(3, types.Single(t => t.Type == roomType).Count);
        Assert.Equal(1, types.Single(t => t.Type == masseurType).Count);
        Assert.DoesNotContain(types, t => t.Type == $"zz-absent-{suffix}");
    }

    [Fact]
    public async Task Types_are_ordered_by_key_so_the_picker_does_not_reshuffle()
    {
        fixture.EnsureAvailable();

        var suffix = Guid.NewGuid().ToString("n")[..8];

        await using (var context = fixture.CreateContext())
        {
            var store = new SqlResourceManagementStore(context);

            // Inserted in reverse key order: an unordered projection would
            // surface them insertion-first and fail the assertion below.
            foreach (var letter in new[] { "c", "b", "a" })
            {
                var created = await store.CreateAsync(
                    Resource.Create($"zz-{letter}-{suffix}", $"Resource {letter}").Value, Ct);
                Assert.True(created.Succeeded);
            }
        }

        await using var verifyContext = fixture.CreateContext();
        var store2 = new SqlResourceManagementStore(verifyContext);

        var first = await store2.ListTypesAsync(Ct);
        var second = await store2.ListTypesAsync(Ct);

        Assert.Equal(first.Select(t => t.Type), second.Select(t => t.Type));
        Assert.Equal(
            first.Select(t => t.Type).Order(StringComparer.Ordinal),
            first.Select(t => t.Type));
    }

    [Fact]
    public async Task Unknown_ids_fail_with_not_found()
    {
        fixture.EnsureAvailable();

        await using var context = fixture.CreateContext();
        var store = new SqlResourceManagementStore(context);

        var update = await store.UpdateAsync(
            BuildResource("Ghost", [(DayOfWeek.Monday, "08:00", "12:00")]), Ct);
        var delete = await store.DeleteAsync(Guid.NewGuid(), Ct);

        Assert.Equal(FailureCodes.ResourceNotFound, Assert.Single(update.Failures).Code);
        Assert.Equal(FailureCodes.ResourceNotFound, Assert.Single(delete.Failures).Code);
    }
}
