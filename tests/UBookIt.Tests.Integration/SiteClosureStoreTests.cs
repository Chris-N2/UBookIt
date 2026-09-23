using Microsoft.EntityFrameworkCore;
using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Persistence;
using UBookIt.Persistence.Entities;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// The closure tables, their stores, and the hydration that carries closures into
/// availability, against real SQL Server.
/// </summary>
/// <remarks>
/// <b>Closures are site-wide, and this database is shared by every suite in the
/// collection.</b> A closure left behind would close a date for tests that know nothing
/// about it, so every test here removes the closures it created. The collection attribute
/// serialises these classes, so no other suite is mid-flight while one exists.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class SiteClosureStoreTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Far from the dates other suites book on, and inside the default horizon.</summary>
    private static readonly DateOnly ClosureDate = new(2026, 9, 24);

    private static readonly DateTimeOffset Now = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

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

    private (SqlSiteClosureManagementStore Management, SqlSiteClosureStore Read) Stores(UBookItDbContext context)
        => (new SqlSiteClosureManagementStore(context), new SqlSiteClosureStore(context));

    // ---- the store's own behaviour ----

    [Fact]
    public async Task A_closure_round_trips_with_its_date_and_label()
    {
        fixture.EnsureAvailable();

        await using var context = fixture.CreateContext();
        var (management, _) = Stores(context);

        var created = await management.CreateAsync(SiteClosure.Create(ClosureDate, "Christmas Day").Value, Ct);
        Assert.True(created.Succeeded, string.Join(", ", created.Failures.Select(f => f.Code)));

        await using var readContext = fixture.CreateContext();
        var fetched = await new SqlSiteClosureManagementStore(readContext).GetAsync(created.Value.Id, Ct);

        Assert.NotNull(fetched);
        Assert.Equal(ClosureDate, fetched!.Date);
        Assert.Equal("Christmas Day", fetched.Label);
        Assert.Equal(created.Value.Id, fetched.Id);
    }

    [Fact]
    public async Task A_second_closure_on_one_date_is_refused()
    {
        fixture.EnsureAvailable();

        await using var context = fixture.CreateContext();
        var (management, _) = Stores(context);

        Assert.True((await management.CreateAsync(SiteClosure.Create(ClosureDate, "First").Value, Ct)).Succeeded);

        var second = await management.CreateAsync(SiteClosure.Create(ClosureDate, "Second").Value, Ct);

        Assert.False(second.Succeeded);
        Assert.Equal(FailureCodes.DuplicateClosureDate, Assert.Single(second.Failures).Code);
    }

    /// <summary>
    /// The race the pre-check cannot close. The unique index is what actually
    /// guarantees one closure per date; this asserts the loser is reported with the
    /// stable code rather than escaping as a database exception.
    /// </summary>
    [Fact]
    public async Task Racing_creates_for_one_date_yield_exactly_one_closure_and_a_reported_duplicate()
    {
        fixture.EnsureAvailable();

        var attempts = Enumerable.Range(0, 8).Select(i => Task.Run(async () =>
        {
            await using var context = fixture.CreateContext();
            return await new SqlSiteClosureManagementStore(context)
                .CreateAsync(SiteClosure.Create(ClosureDate, $"Racer {i}").Value, Ct);
        }, Ct));

        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(r => r.Succeeded));
        Assert.All(
            results.Where(r => !r.Succeeded),
            r => Assert.Equal(FailureCodes.DuplicateClosureDate, Assert.Single(r.Failures).Code));

        await using var verifyContext = fixture.CreateContext();
        Assert.Equal(1, await verifyContext.SiteClosures.CountAsync(c => c.Date == ClosureDate, Ct));
    }

    [Fact]
    public async Task Updating_a_closure_keeps_its_id_so_exemptions_follow_it()
    {
        fixture.EnsureAvailable();

        await using var context = fixture.CreateContext();
        var (management, _) = Stores(context);
        var created = (await management.CreateAsync(SiteClosure.Create(ClosureDate, "Stocktake").Value, Ct)).Value;

        var moved = await management.UpdateAsync(
            SiteClosure.Create(ClosureDate.AddDays(1), "Stocktake (moved)", created.Id).Value, Ct);

        Assert.True(moved.Succeeded);
        Assert.Equal(created.Id, moved.Value.Id);

        await using var verifyContext = fixture.CreateContext();
        var row = await verifyContext.SiteClosures.AsNoTracking().SingleAsync(c => c.Id == created.Id, Ct);
        Assert.Equal(ClosureDate.AddDays(1), row.Date);
    }

    [Fact]
    public async Task An_unknown_closure_is_reported_as_not_found()
    {
        fixture.EnsureAvailable();

        await using var context = fixture.CreateContext();
        var (management, _) = Stores(context);

        var updated = await management.UpdateAsync(SiteClosure.Create(ClosureDate, "Ghost", Guid.NewGuid()).Value, Ct);
        var deleted = await management.DeleteAsync(Guid.NewGuid(), Ct);

        Assert.Equal(FailureCodes.ClosureNotFound, Assert.Single(updated.Failures).Code);
        Assert.Equal(FailureCodes.ClosureNotFound, Assert.Single(deleted.Failures).Code);
    }

    [Fact]
    public async Task The_upcoming_filter_is_applied_by_the_server()
    {
        fixture.EnsureAvailable();

        await using var context = fixture.CreateContext();
        var (management, _) = Stores(context);

        await management.CreateAsync(SiteClosure.Create(ClosureDate.AddYears(-1), "Last year").Value, Ct);
        await management.CreateAsync(SiteClosure.Create(ClosureDate, "This year").Value, Ct);

        var upcoming = await management.ListAsync(from: ClosureDate.AddDays(-1), Ct);
        var all = await management.ListAsync(from: null, Ct);

        Assert.Equal("This year", Assert.Single(upcoming).Label);
        Assert.Equal(2, all.Count);

        // Ordered by date, deterministically, in both readings.
        Assert.Equal(all.OrderBy(c => c.Date).Select(c => c.Id), all.Select(c => c.Id));
    }

    [Fact]
    public async Task Deleting_a_closure_removes_its_opt_outs()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);

        await using var context = fixture.CreateContext();
        var (management, _) = Stores(context);
        var closure = (await management.CreateAsync(SiteClosure.Create(ClosureDate, "Bank holiday").Value, Ct)).Value;

        await using (var optOutContext = fixture.CreateContext())
        {
            optOutContext.ResourceClosureOptOuts.Add(
                new ResourceClosureOptOutRow { ResourceId = resourceId, ClosureId = closure.Id });
            await optOutContext.SaveChangesAsync(Ct);
        }

        await using (var deleteContext = fixture.CreateContext())
        {
            Assert.True((await new SqlSiteClosureManagementStore(deleteContext).DeleteAsync(closure.Id, Ct)).Succeeded);
        }

        await using var verifyContext = fixture.CreateContext();
        Assert.Empty(await verifyContext.ResourceClosureOptOuts
            .Where(o => o.ClosureId == closure.Id).ToListAsync(Ct));

        // The resource itself is untouched by its exemption disappearing.
        Assert.True(await verifyContext.Resources.AnyAsync(r => r.Id == resourceId, Ct));
    }

    [Fact]
    public async Task Deleting_a_resource_removes_its_opt_outs_and_leaves_the_closure()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);

        await using var context = fixture.CreateContext();
        var closure = (await new SqlSiteClosureManagementStore(context)
            .CreateAsync(SiteClosure.Create(ClosureDate, "Bank holiday").Value, Ct)).Value;

        await using (var optOutContext = fixture.CreateContext())
        {
            optOutContext.ResourceClosureOptOuts.Add(
                new ResourceClosureOptOutRow { ResourceId = resourceId, ClosureId = closure.Id });
            await optOutContext.SaveChangesAsync(Ct);
        }

        await using (var deleteContext = fixture.CreateContext())
        {
            Assert.True((await new SqlResourceManagementStore(deleteContext, new SqlSiteClosureStore(deleteContext))
                .DeleteAsync(resourceId, Ct)).Succeeded);
        }

        await using var verifyContext = fixture.CreateContext();
        Assert.Empty(await verifyContext.ResourceClosureOptOuts
            .Where(o => o.ResourceId == resourceId).ToListAsync(Ct));
        Assert.True(await verifyContext.SiteClosures.AnyAsync(c => c.Id == closure.Id, Ct));
    }

    // ---- hydration, and the trap it exists to avoid ----

    [Fact]
    public async Task A_hydrated_resource_carries_the_closures_it_has_not_opted_out_of()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);

        await using var context = fixture.CreateContext();
        await new SqlSiteClosureManagementStore(context)
            .CreateAsync(SiteClosure.Create(ClosureDate, "Bank holiday").Value, Ct);

        await using var readContext = fixture.CreateContext();
        var resource = await new SqlResourceStore(readContext, new SqlSiteClosureStore(readContext))
            .GetAsync(resourceId, Ct);

        Assert.NotNull(resource);
        Assert.Equal(ClosureDate, Assert.Single(resource!.Availability.Closures).Date);
        Assert.Empty(resource.Availability.EffectiveWindows(ClosureDate));
    }

    [Fact]
    public async Task An_opted_out_resource_is_hydrated_without_the_closure()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);

        await using var context = fixture.CreateContext();
        var closure = (await new SqlSiteClosureManagementStore(context)
            .CreateAsync(SiteClosure.Create(ClosureDate, "Bank holiday").Value, Ct)).Value;

        await using (var optOutContext = fixture.CreateContext())
        {
            optOutContext.ResourceClosureOptOuts.Add(
                new ResourceClosureOptOutRow { ResourceId = resourceId, ClosureId = closure.Id });
            await optOutContext.SaveChangesAsync(Ct);
        }

        await using var readContext = fixture.CreateContext();
        var resource = await new SqlResourceStore(readContext, new SqlSiteClosureStore(readContext))
            .GetAsync(resourceId, Ct);

        Assert.NotNull(resource);
        Assert.Empty(resource!.Availability.Closures);
        Assert.Equal(closure.Id, Assert.Single(resource.ClosureOptOuts));

        // 08:00–18:00, exactly as it would be on a site that never had the closure.
        Assert.Single(resource.Availability.EffectiveWindows(ClosureDate));
    }

    /// <summary>
    /// <b>The write-back trap.</b> Loading a resource that inherits a closure and saving it
    /// unchanged must not write that date into the resource's own exception rows — which is
    /// what a design merging closures into the exception set would have done, permanently and
    /// invisibly. Asserted by deleting the closure afterwards and finding the date open again.
    /// </summary>
    [Fact]
    public async Task Saving_a_resource_does_not_absorb_an_inherited_closure()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);

        await using var context = fixture.CreateContext();
        var closure = (await new SqlSiteClosureManagementStore(context)
            .CreateAsync(SiteClosure.Create(ClosureDate, "Bank holiday").Value, Ct)).Value;

        Resource loaded;
        await using (var readContext = fixture.CreateContext())
        {
            loaded = (await new SqlResourceStore(readContext, new SqlSiteClosureStore(readContext))
                .GetAsync(resourceId, Ct))!;
        }

        await using (var writeContext = fixture.CreateContext())
        {
            var saved = await new SqlResourceManagementStore(writeContext, new SqlSiteClosureStore(writeContext))
                .UpdateAsync(loaded, Ct);
            Assert.True(saved.Succeeded, string.Join(", ", saved.Failures.Select(f => f.Code)));
        }

        // Nothing landed in the resource's own exception rows.
        await using (var verifyContext = fixture.CreateContext())
        {
            Assert.Empty(await verifyContext.Exceptions.Where(e => e.ResourceId == resourceId).ToListAsync(Ct));
        }

        await using (var deleteContext = fixture.CreateContext())
        {
            await new SqlSiteClosureManagementStore(deleteContext).DeleteAsync(closure.Id, Ct);
        }

        await using var finalContext = fixture.CreateContext();
        var after = await new SqlResourceStore(finalContext, new SqlSiteClosureStore(finalContext))
            .GetAsync(resourceId, Ct);

        Assert.Empty(after!.Availability.Exceptions);
        Assert.Single(after.Availability.EffectiveWindows(ClosureDate));
    }

    // ---- opt-out writes ----

    [Fact]
    public async Task An_update_replaces_the_opt_out_set_rather_than_merging()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);

        await using var context = fixture.CreateContext();
        var management = new SqlSiteClosureManagementStore(context);
        var first = (await management.CreateAsync(SiteClosure.Create(ClosureDate, "One").Value, Ct)).Value;
        var second = (await management.CreateAsync(SiteClosure.Create(ClosureDate.AddDays(1), "Two").Value, Ct)).Value;

        await SaveOptOutsAsync(resourceId, [first.Id, second.Id]);
        await SaveOptOutsAsync(resourceId, [first.Id]);

        await using var verifyContext = fixture.CreateContext();
        var rows = await verifyContext.ResourceClosureOptOuts
            .Where(o => o.ResourceId == resourceId).ToListAsync(Ct);

        Assert.Equal(first.Id, Assert.Single(rows).ClosureId);
    }

    [Fact]
    public async Task Racing_updates_leave_exactly_one_writers_opt_out_set()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);

        await using var context = fixture.CreateContext();
        var management = new SqlSiteClosureManagementStore(context);
        var first = (await management.CreateAsync(SiteClosure.Create(ClosureDate, "One").Value, Ct)).Value;
        var second = (await management.CreateAsync(SiteClosure.Create(ClosureDate.AddDays(1), "Two").Value, Ct)).Value;

        var writers = new[]
        {
            Task.Run(() => SaveOptOutsAsync(resourceId, [first.Id]), Ct),
            Task.Run(() => SaveOptOutsAsync(resourceId, [second.Id]), Ct),
            Task.Run(() => SaveOptOutsAsync(resourceId, [first.Id, second.Id]), Ct),
        };

        await Task.WhenAll(writers);

        await using var verifyContext = fixture.CreateContext();
        var stored = (await verifyContext.ResourceClosureOptOuts
            .Where(o => o.ResourceId == resourceId)
            .Select(o => o.ClosureId)
            .ToListAsync(Ct))
            .Order()
            .ToList();

        // Exactly one writer's complete set — never a merge of two, never a duplicate.
        Assert.Distinct(stored);

        var completeSets = new[]
        {
            new[] { first.Id }.Order().ToList(),
            new[] { second.Id }.Order().ToList(),
            new[] { first.Id, second.Id }.Order().ToList(),
        };

        Assert.Contains(completeSets, expected => expected.SequenceEqual(stored));
    }

    /// <summary>
    /// <b>The seam.</b> Not a test of hydration and a test of projection, but one path from a
    /// stored closure through the production read port into the production availability query —
    /// because a guard over each half stays green while nothing connects them.
    /// </summary>
    [Fact]
    public async Task A_stored_closure_removes_the_date_from_a_real_availability_query()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);

        await using var context = fixture.CreateContext();
        await new SqlSiteClosureManagementStore(context)
            .CreateAsync(SiteClosure.Create(ClosureDate, "Bank holiday").Value, Ct);

        var (_, availability) = fixture.CreateServices(Now);

        var onClosure = await availability.GetFreeTimeAsync(resourceId, ClosureDate, ClosureDate, Ct);
        var dayBefore = ClosureDate.AddDays(-1);
        var onOpenDay = await availability.GetFreeTimeAsync(resourceId, dayBefore, dayBefore, Ct);

        Assert.True(onClosure.Succeeded);
        Assert.Empty(onClosure.Value);

        // The neighbouring day still answers, so the empty result above is the closure
        // rather than a query that returns nothing.
        Assert.True(onOpenDay.Succeeded);
        Assert.NotEmpty(onOpenDay.Value);
    }

    private async Task SaveOptOutsAsync(Guid resourceId, Guid[] closureIds)
    {
        await using var context = fixture.CreateContext();
        var store = new SqlResourceStore(context, new SqlSiteClosureStore(context));
        var resource = (await store.GetAsync(resourceId, Ct))!;

        var updated = Resource.Create(
            type: resource.Type,
            displayName: resource.DisplayName,
            description: resource.Description,
            capabilities: resource.Capabilities.Keys,
            availability: resource.Availability,
            directlyBookable: resource.DirectlyBookable,
            id: resource.Id,
            closureOptOuts: closureIds).Value;

        await using var writeContext = fixture.CreateContext();
        var saved = await new SqlResourceManagementStore(writeContext, new SqlSiteClosureStore(writeContext))
            .UpdateAsync(updated, Ct);

        Assert.True(saved.Succeeded, string.Join(", ", saved.Failures.Select(f => f.Code)));
    }

    /// <summary>
    /// <b>The N+1 the design promised not to have, counted rather than asserted.</b> A candidate
    /// pool is what <c>ListByTypeAsync</c> serves, so a closure read per resource would turn one
    /// availability question into one query per candidate. Moving the closure read inside the
    /// projection would pass every other test in this repository; it fails here.
    /// </summary>
    [Fact]
    public async Task Listing_by_type_reads_closures_once_for_the_whole_batch()
    {
        fixture.EnsureAvailable();

        var type = $"closure-n1-{Guid.NewGuid():N}"[..20];
        for (var i = 0; i < 5; i++)
        {
            await Seed.EveryDayRoomAsync(fixture, Ct, type: type);
        }

        await using (var setup = fixture.CreateContext())
        {
            await new SqlSiteClosureManagementStore(setup)
                .CreateAsync(SiteClosure.Create(ClosureDate, "Bank holiday").Value, Ct);
        }

        var interceptor = new CommandRecordingInterceptor();
        await using var context = fixture.CreateContext(interceptor);

        var resources = await new SqlResourceStore(context, new SqlSiteClosureStore(context))
            .ListByTypeAsync(type, Ct);

        Assert.Equal(5, resources.Count);
        Assert.All(resources, resource => Assert.Single(resource.Availability.Closures));

        // ONE closure query, whatever the pool size.
        var closureReads = interceptor.Commands
            .Count(text => text.Contains("uBookItSiteClosure", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(1, closureReads);
    }

    /// <summary>
    /// Precedence is decided in the domain, so no query may filter by it. Asserted over what was
    /// actually SENT, because the rule is about the SQL rather than about the code that wrote it.
    /// </summary>
    [Fact]
    public async Task No_query_joins_closures_to_decide_precedence()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);

        await using (var setup = fixture.CreateContext())
        {
            await new SqlSiteClosureManagementStore(setup)
                .CreateAsync(SiteClosure.Create(ClosureDate, "Bank holiday").Value, Ct);
        }

        var interceptor = new CommandRecordingInterceptor();
        await using var context = fixture.CreateContext(interceptor);

        await new SqlResourceStore(context, new SqlSiteClosureStore(context)).GetAsync(resourceId, Ct);

        // The closure read stands alone: it never joins the exception or open-hours tables, which
        // is what a storage-layer precedence rule would have to do.
        var closureCommands = interceptor.Commands
            .Where(text => text.Contains("uBookItSiteClosure", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // THE GUARD'S OWN PRECONDITION, and it was missing: `Assert.All` over an empty sequence
        // passes, so a closure read that stopped touching the table at all — renamed, raw SQL in
        // different casing, a compiled query — would retire this guard silently while it went on
        // reporting green. QA proved exactly that by replacing the store's body with `return []`.
        Assert.NotEmpty(closureCommands);

        Assert.All(closureCommands, text =>
        {
            Assert.DoesNotContain("uBookItResourceException", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("uBookItResourceOpenHours", text, StringComparison.OrdinalIgnoreCase);
        });
    }
}
