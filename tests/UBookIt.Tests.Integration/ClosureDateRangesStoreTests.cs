using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UBookIt.Core.Availability;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Persistence;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// Closure ranges and resources' closed periods against real SQL Server (closure-date-ranges:
/// the <c>persistence</c> delta, and the store halves of the <c>site-closures</c> one).
/// </summary>
/// <remarks>
/// Closures are site-wide and the database is shared by the collection, so every closure made
/// here is purged before and after, on the terms <see cref="SiteClosureStoreTests"/> sets.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class ClosureDateRangesStoreTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly Start = new(2027, 2, 1);

    public ValueTask InitializeAsync() => PurgeAsync();

    public ValueTask DisposeAsync() => PurgeAsync();

    private async ValueTask PurgeAsync()
    {
        if (fixture.SkipReason is not null)
        {
            return;
        }

        await using var context = fixture.CreateContext();
        await context.SiteClosures.ExecuteDeleteAsync(CancellationToken.None);
    }

    private static SiteClosure Range(int fromDay, int toDay, string label = "Refurbishment", Guid? id = null)
        => SiteClosure.Create(Start.AddDays(fromDay), label, id, Start.AddDays(toDay)).Value;

    // ── Closure ranges ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_range_round_trips()
    {
        fixture.EnsureAvailable();

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlSiteClosureManagementStore(context).CreateAsync(Range(0, 58), Ct)).Succeeded);
        }

        await using var read = fixture.CreateContext();
        var closure = Assert.Single(await new SqlSiteClosureStore(read).ListAsync(Ct));

        Assert.Equal(Start, closure.Date);
        Assert.Equal(Start.AddDays(58), closure.EndDate);
    }

    [Fact]
    public async Task An_overlapping_range_is_refused_and_the_existing_one_is_unchanged()
    {
        fixture.EnsureAvailable();

        await using var context = fixture.CreateContext();
        var store = new SqlSiteClosureManagementStore(context);
        var first = (await store.CreateAsync(Range(0, 58), Ct)).Value;

        var second = await store.CreateAsync(Range(52, 63, "Spring"), Ct);

        Assert.Equal(FailureCodes.DuplicateClosureDate, Assert.Single(second.Failures).Code);

        await using var read = fixture.CreateContext();
        var stored = Assert.Single(await new SqlSiteClosureStore(read).ListAsync(Ct));
        Assert.Equal(first.Id, stored.Id);
        Assert.Equal(Start.AddDays(58), stored.EndDate);
    }

    [Fact]
    public async Task Extending_a_closure_is_not_an_overlap_with_itself()
    {
        fixture.EnsureAvailable();

        await using var context = fixture.CreateContext();
        var store = new SqlSiteClosureManagementStore(context);
        var created = (await store.CreateAsync(Range(0, 58), Ct)).Value;

        var extended = await store.UpdateAsync(Range(0, 88, id: created.Id), Ct);

        Assert.True(extended.Succeeded, string.Join(", ", extended.Failures.Select(f => f.Code)));

        await using var read = fixture.CreateContext();
        Assert.Equal(Start.AddDays(88), Assert.Single(await new SqlSiteClosureStore(read).ListAsync(Ct)).EndDate);
    }

    /// <summary>
    /// The race a unique index cannot see: different starts, overlapping ranges. The lock is
    /// what guarantees exactly one is stored.
    /// </summary>
    [Fact]
    public async Task Concurrent_overlapping_ranges_with_different_starts_cannot_both_be_stored()
    {
        fixture.EnsureAvailable();

        var attempts = Enumerable.Range(0, 8).Select(i => Task.Run(async () =>
        {
            await using var context = fixture.CreateContext();
            return await new SqlSiteClosureManagementStore(context)
                .CreateAsync(Range(i, i + 20, $"Racer {i}"), Ct);
        }, Ct));

        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(r => r.Succeeded));
        Assert.All(
            results.Where(r => !r.Succeeded),
            r => Assert.Equal(FailureCodes.DuplicateClosureDate, Assert.Single(r.Failures).Code));

        await using var verify = fixture.CreateContext();
        Assert.Equal(1, await verify.SiteClosures.CountAsync(Ct));
    }

    [Fact]
    public async Task A_closure_in_progress_is_upcoming()
    {
        fixture.EnsureAvailable();

        await using var context = fixture.CreateContext();
        var store = new SqlSiteClosureManagementStore(context);
        await store.CreateAsync(Range(0, 20, "In progress"), Ct);
        await store.CreateAsync(Range(-30, -25, "Over"), Ct);

        var upcoming = await store.ListAsync(Start.AddDays(10), Ct);

        Assert.Equal("In progress", Assert.Single(upcoming).Label);
    }

    [Fact]
    public async Task A_resource_read_closes_every_date_in_a_range()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        await using (var setup = fixture.CreateContext())
        {
            await new SqlSiteClosureManagementStore(setup).CreateAsync(Range(0, 9), Ct);
        }

        await using var context = fixture.CreateContext();
        var resource = await new SqlResourceStore(context, new SqlSiteClosureStore(context)).GetAsync(resourceId, Ct);

        Assert.Empty(resource!.Availability.EffectiveWindows(Start));
        Assert.Empty(resource.Availability.EffectiveWindows(Start.AddDays(9)));
        Assert.NotEmpty(resource.Availability.EffectiveWindows(Start.AddDays(10)));
    }

    // ── Closed periods ───────────────────────────────────────────────────────────────────

    private async Task<Resource> LoadAsync(Guid resourceId)
    {
        await using var context = fixture.CreateContext();
        return (await new SqlResourceStore(context, new SqlSiteClosureStore(context)).GetAsync(resourceId, Ct))!;
    }

    private static Resource With(Resource resource, IEnumerable<ClosedPeriod> periods, IEnumerable<DateException>? exceptions = null)
        => Resource.Create(
            type: resource.Type,
            displayName: resource.DisplayName,
            description: resource.Description,
            capabilities: resource.Capabilities.Keys,
            availability: AvailabilityConfiguration.Create(
                resource.Availability.OpenHours,
                exceptions ?? resource.Availability.Exceptions,
                resource.Availability.Constraints,
                closedPeriods: periods).Value,
            directlyBookable: resource.DirectlyBookable,
            id: resource.Id,
            closureOptOuts: resource.ClosureOptOuts).Value;

    private static ClosedPeriod Period(int fromDay, int toDay, string? note = null)
        => ClosedPeriod.Create(Start.AddDays(fromDay), Start.AddDays(toDay), note).Value;

    [Fact]
    public async Task Closed_periods_round_trip_through_the_read_port()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var resource = await LoadAsync(resourceId);

        await using (var context = fixture.CreateContext())
        {
            var saved = await new SqlResourceManagementStore(context, new SqlSiteClosureStore(context))
                .UpdateAsync(With(resource, [Period(0, 13, "Being painted"), Period(20, 22)]), Ct);
            Assert.True(saved.Succeeded);
        }

        var reloaded = await LoadAsync(resourceId);

        Assert.Equal(2, reloaded.Availability.ClosedPeriods.Count);
        Assert.Equal("Being painted", reloaded.Availability.ClosedPeriods.First().Note);
        Assert.Empty(reloaded.Availability.EffectiveWindows(Start.AddDays(5)));
    }

    [Fact]
    public async Task Keeping_closed_periods_leaves_their_rows_untouched()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        await using (var context = fixture.CreateContext())
        {
            await new SqlResourceManagementStore(context, new SqlSiteClosureStore(context)).UpdateAsync(With(await LoadAsync(resourceId), [Period(0, 13)]), Ct);
        }

        var idsBefore = await PeriodRowIdsAsync(resourceId);

        // The resource carries NO periods, as a caller that never sent them would produce; the
        // store must not take that as "none".
        await using (var context = fixture.CreateContext())
        {
            var kept = await new SqlResourceManagementStore(context, new SqlSiteClosureStore(context))
                .UpdateKeepingClosedPeriodsAsync(With(await LoadAsync(resourceId), []), Ct);
            Assert.True(kept.Succeeded);
        }

        Assert.Equal(idsBefore, await PeriodRowIdsAsync(resourceId));
    }

    /// <summary>
    /// The race design D6 closes. The caller judged its exceptions against periods it read
    /// earlier; a period stored since must still refuse them, under the lock.
    /// </summary>
    [Fact]
    public async Task Keeping_closed_periods_re_judges_exceptions_against_the_stored_ones()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var before = await LoadAsync(resourceId);

        await using (var context = fixture.CreateContext())
        {
            await new SqlResourceManagementStore(context, new SqlSiteClosureStore(context)).UpdateAsync(With(before, [Period(0, 13)]), Ct);
        }

        // Built from the read BEFORE the period existed, as the losing writer's would be.
        var stale = With(before, [], [DateException.Closure(Start.AddDays(4))]);

        await using var write = fixture.CreateContext();
        var result = await new SqlResourceManagementStore(write, new SqlSiteClosureStore(write)).UpdateKeepingClosedPeriodsAsync(stale, Ct);

        Assert.Equal(FailureCodes.ClosedPeriodConflictsWithException, Assert.Single(result.Failures).Code);
        Assert.Empty((await LoadAsync(resourceId)).Availability.Exceptions);
    }

    [Fact]
    public async Task A_failed_update_leaves_the_closed_periods_unchanged()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        await using (var context = fixture.CreateContext())
        {
            await new SqlResourceManagementStore(context, new SqlSiteClosureStore(context)).UpdateAsync(With(await LoadAsync(resourceId), [Period(0, 13)]), Ct);
        }

        var idsBefore = await PeriodRowIdsAsync(resourceId);

        // An opt-out naming no closure fails at SaveChanges on its foreign key — AFTER the child
        // rows, closed periods included, were deleted inside the transaction.
        var current = await LoadAsync(resourceId);
        var broken = Resource.Create(
            type: current.Type,
            displayName: current.DisplayName,
            description: current.Description,
            capabilities: current.Capabilities.Keys,
            availability: AvailabilityConfiguration.Create(
                current.Availability.OpenHours, closedPeriods: [Period(30, 31)]).Value,
            directlyBookable: current.DirectlyBookable,
            id: current.Id,
            closureOptOuts: [Guid.NewGuid()]).Value;

        await using (var context = fixture.CreateContext())
        {
            await Assert.ThrowsAnyAsync<DbUpdateException>(
                () => new SqlResourceManagementStore(context, new SqlSiteClosureStore(context)).UpdateAsync(broken, Ct));
        }

        Assert.Equal(idsBefore, await PeriodRowIdsAsync(resourceId));
    }

    [Fact]
    public async Task Listing_hydrates_closed_periods_without_a_query_per_resource()
    {
        fixture.EnsureAvailable();

        var type = $"cdr-{Guid.NewGuid():N}"[..20];
        for (var i = 0; i < 5; i++)
        {
            var id = await Seed.EveryDayRoomAsync(fixture, Ct, type: type);
            await using var context = fixture.CreateContext();
            await new SqlResourceManagementStore(context, new SqlSiteClosureStore(context)).UpdateAsync(With(await LoadAsync(id), [Period(i, i)]), Ct);
        }

        var interceptor = new CommandRecordingInterceptor();
        await using var read = fixture.CreateContext(interceptor);
        var resources = await new SqlResourceStore(read, new SqlSiteClosureStore(read)).ListByTypeAsync(type, Ct);

        Assert.Equal(5, resources.Count);
        Assert.All(resources, r => Assert.Single(r.Availability.ClosedPeriods));

        var periodReads = interceptor.Commands
            .Count(text => text.Contains("uBookItResourceClosedPeriod", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, periodReads);
    }

    [Fact]
    public async Task Deleting_a_resource_removes_its_closed_periods()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        await using (var context = fixture.CreateContext())
        {
            await new SqlResourceManagementStore(context, new SqlSiteClosureStore(context)).UpdateAsync(With(await LoadAsync(resourceId), [Period(0, 3)]), Ct);
        }

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlResourceManagementStore(context, new SqlSiteClosureStore(context)).DeleteAsync(resourceId, Ct)).Succeeded);
        }

        Assert.Empty(await PeriodRowIdsAsync(resourceId));
    }

    private async Task<long[]> PeriodRowIdsAsync(Guid resourceId)
    {
        await using var context = fixture.CreateContext();
        return await context.ClosedPeriods
            .Where(p => p.ResourceId == resourceId)
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToArrayAsync(Ct);
    }

    // ── The migration, on a database that already holds closures ─────────────────────────

    /// <summary>
    /// On the terms <see cref="BookerErasureMigrationTests"/> set: a database of its own, migrated
    /// to the version before, written to with raw SQL, then upgraded.
    /// </summary>
    [Fact]
    public async Task The_end_date_migration_keeps_every_existing_closure_to_its_one_date()
    {
        fixture.EnsureAvailable();

        const string previous = "20260923094150_AddSiteClosures";
        const string current = "20261011182653_ClosureDateRanges";

        await using var probe = fixture.CreateContext();
        var baseConnection = probe.Database.GetConnectionString()!;
        var database = $"uBookItUpgrade_{Guid.NewGuid():N}";
        var master = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = "master" }.ConnectionString;
        var builder = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = database };

        await ExecuteAsync(master, $"CREATE DATABASE [{database}]");

        try
        {
            var optionsBuilder = new DbContextOptionsBuilder<UBookItDbContext>();
            UBookItDbContext.ConfigureSqlServer(optionsBuilder, builder.ConnectionString);
            var options = optionsBuilder.Options;

            await using (var old = new UBookItDbContext(options))
            {
                await old.GetService<IMigrator>().MigrateAsync(previous, Ct);
                Assert.DoesNotContain(current, await old.Database.GetAppliedMigrationsAsync(Ct));
            }

            var resourceId = Guid.NewGuid();
            var christmas = Guid.NewGuid();
            var boxingDay = Guid.NewGuid();

            await ExecuteAsync(
                builder.ConnectionString,
                $"""
                 INSERT INTO uBookItResource
                     (Id, Type, DisplayName, Description, GranularityMinutes, MinDurationMinutes,
                      MaxDurationMinutes, LeadTimeMinutes, HorizonDays, DirectlyBookable)
                 VALUES ('{resourceId}', 'room', 'Upgrade Room', NULL, 15, 30, 480, 0, 90, 1);

                 INSERT INTO uBookItSiteClosure (Id, Date, Label)
                 VALUES ('{christmas}', '2026-12-25', 'Christmas Day'),
                        ('{boxingDay}', '2026-12-26', 'Boxing Day');

                 INSERT INTO uBookItResourceClosureOptOut (ResourceId, ClosureId)
                 VALUES ('{resourceId}', '{boxingDay}');
                 """);

            await using (var upgraded = new UBookItDbContext(options))
            {
                await upgraded.Database.MigrateAsync(Ct);
            }

            await using var after = new UBookItDbContext(options);
            var closures = await after.SiteClosures.OrderBy(c => c.Date).ToListAsync(Ct);

            Assert.Equal(2, closures.Count);
            Assert.All(closures, c => Assert.Equal(c.Date, c.EndDate));
            Assert.Equal(1, await after.ResourceClosureOptOuts.CountAsync(o => o.ClosureId == boxingDay, Ct));

            // And availability reads exactly as before: closed on Christmas, open on Boxing Day,
            // which this resource opted out of.
            var resource = await new SqlResourceStore(after, new SqlSiteClosureStore(after)).GetAsync(resourceId, Ct);
            Assert.NotNull(resource!.Availability.ClosureFor(new DateOnly(2026, 12, 25)));
            Assert.Null(resource.Availability.ClosureFor(new DateOnly(2026, 12, 26)));
            Assert.Null(resource.Availability.ClosureFor(new DateOnly(2026, 12, 27)));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await ExecuteAsync(
                master,
                $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]");
        }
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(Ct);
    }
}
