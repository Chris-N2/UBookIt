using Microsoft.EntityFrameworkCore;
using UBookIt.Core.Common;
using UBookIt.Core.Services;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// Service stores against real SQL Server: create→get round-trip, paged list
/// with unpaged total, update, and delete cascading the role rows.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class ServiceStoreTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Service NewService(string name = "Massage", int? minutes = 60, string type = "person")
        => Service.Create(
            name,
            minutes is { } m ? ServiceDuration.Fixed(TimeSpan.FromMinutes(m)).Value : ServiceDuration.Unbounded,
            [new ServiceRole(type, 1)]).Value;

    [Fact]
    public async Task Create_then_get_round_trips()
    {
        fixture.EnsureAvailable();
        var service = NewService();

        await using (var context = fixture.CreateContext())
        {
            var created = await new SqlServiceManagementStore(context).CreateAsync(service, Ct);
            Assert.True(created.Succeeded);
        }

        await using var read = fixture.CreateContext();
        var fetched = await new SqlServiceStore(read).GetAsync(service.Id, Ct);

        Assert.NotNull(fetched);
        Assert.Equal("Massage", fetched!.Name);
        Assert.Equal(ServiceDurationKind.Fixed, fetched.Duration.Kind);
        Assert.Equal(TimeSpan.FromMinutes(60), fetched.Duration.FixedLength);
        Assert.Equal("person", Assert.Single(fetched.Roles).ResourceType);
    }

    [Fact]
    public async Task Bounded_variable_duration_round_trips()
    {
        fixture.EnsureAvailable();
        var duration = ServiceDuration.Variable(TimeSpan.FromMinutes(45), TimeSpan.FromMinutes(120)).Value;
        var service = Service.Create("Room hire", duration, [new ServiceRole("room", 1)]).Value;

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlServiceManagementStore(context).CreateAsync(service, Ct)).Succeeded);
        }

        await using var read = fixture.CreateContext();
        var fetched = await new SqlServiceStore(read).GetAsync(service.Id, Ct);

        Assert.NotNull(fetched);
        Assert.Equal(ServiceDurationKind.Variable, fetched!.Duration.Kind);
        Assert.Equal(TimeSpan.FromMinutes(45), fetched.Duration.Min);
        Assert.Equal(TimeSpan.FromMinutes(120), fetched.Duration.Max);
    }

    [Fact]
    public async Task Unbounded_variable_duration_round_trips()
    {
        fixture.EnsureAvailable();
        var service = Service.Create("Hot desk", ServiceDuration.Unbounded, [new ServiceRole("desk", 1)]).Value;

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlServiceManagementStore(context).CreateAsync(service, Ct)).Succeeded);
        }

        await using var read = fixture.CreateContext();
        var fetched = await new SqlServiceStore(read).GetAsync(service.Id, Ct);

        Assert.NotNull(fetched);
        Assert.Equal(ServiceDurationKind.Variable, fetched!.Duration.Kind);
        Assert.Null(fetched.Duration.Min);
        Assert.Null(fetched.Duration.Max);
    }

    [Fact]
    public async Task Lists_paged_with_unpaged_total()
    {
        fixture.EnsureAvailable();
        var seeded = new[] { NewService("A"), NewService("B"), NewService("C") };

        await using (var context = fixture.CreateContext())
        {
            var store = new SqlServiceManagementStore(context);
            foreach (var s in seeded)
            {
                await store.CreateAsync(s, Ct);
            }
        }

        await using var read = fixture.CreateContext();
        var all = await new SqlServiceStore(read).ListAsync(0, 500, Ct);

        Assert.True(all.Total >= seeded.Length);
        Assert.All(seeded, s => Assert.Contains(s.Id, all.Items.Select(i => i.Id)));

        var page = await new SqlServiceStore(read).ListAsync(0, 2, Ct);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(all.Total, page.Total);
    }

    [Fact]
    public async Task Update_replaces_scalars_and_role()
    {
        fixture.EnsureAvailable();
        var service = NewService("Before", 30, "person");

        await using (var c1 = fixture.CreateContext())
        {
            await new SqlServiceManagementStore(c1).CreateAsync(service, Ct);
        }

        var updated = Service.Create(
            "After",
            ServiceDuration.Fixed(TimeSpan.FromMinutes(90)).Value,
            [new ServiceRole("room", 1)],
            service.Id).Value;
        await using (var c2 = fixture.CreateContext())
        {
            var result = await new SqlServiceManagementStore(c2).UpdateAsync(updated, Ct);
            Assert.True(result.Succeeded);
        }

        await using var read = fixture.CreateContext();
        var fetched = await new SqlServiceStore(read).GetAsync(service.Id, Ct);

        Assert.Equal("After", fetched!.Name);
        Assert.Equal(TimeSpan.FromMinutes(90), fetched.Duration.FixedLength);
        Assert.Equal("room", Assert.Single(fetched.Roles).ResourceType); // old role replaced, not merged
    }

    [Fact]
    public async Task Delete_removes_service_and_roles()
    {
        fixture.EnsureAvailable();
        var service = NewService();

        await using (var c1 = fixture.CreateContext())
        {
            await new SqlServiceManagementStore(c1).CreateAsync(service, Ct);
        }

        await using (var c2 = fixture.CreateContext())
        {
            var result = await new SqlServiceManagementStore(c2).DeleteAsync(service.Id, Ct);
            Assert.True(result.Succeeded);
        }

        await using var verify = fixture.CreateContext();
        Assert.Null(await new SqlServiceStore(verify).GetAsync(service.Id, Ct));
        Assert.Equal(0, await verify.ServiceRoles.CountAsync(r => r.ServiceId == service.Id, Ct));
    }

    /// <summary>
    /// The single-role merge race (the ③ lesson, applied to service roles): at
    /// READ COMMITTED, two concurrent updates each delete the one role row
    /// (taking no range lock over a 0/1-row set) and both insert, committing a
    /// merged two-role set that violates the single-role invariant. The
    /// per-service app lock must serialize them. Ten iterations on a fresh
    /// service each, so there is no accidental serialization point.
    /// </summary>
    [Fact]
    public async Task Racing_updates_never_merge_roles()
    {
        fixture.EnsureAvailable();

        for (var iteration = 0; iteration < 10; iteration++)
        {
            var original = NewService($"Race {iteration}", 30, "person");
            await using (var context = fixture.CreateContext())
            {
                Assert.True((await new SqlServiceManagementStore(context).CreateAsync(original, Ct)).Succeeded);
            }

            Service Variant(string type) =>
                Service.Create(
                    $"Race {iteration}",
                    ServiceDuration.Fixed(TimeSpan.FromMinutes(30)).Value,
                    [new ServiceRole(type, 1)],
                    original.Id).Value;

            var results = await Task.WhenAll(
                Task.Run(async () =>
                {
                    await using var context = fixture.CreateContext();
                    return await new SqlServiceManagementStore(context).UpdateAsync(Variant("room"), Ct);
                }, Ct),
                Task.Run(async () =>
                {
                    await using var context = fixture.CreateContext();
                    return await new SqlServiceManagementStore(context).UpdateAsync(Variant("person"), Ct);
                }, Ct));

            Assert.All(results, r => Assert.True(r.Succeeded));

            await using var read = fixture.CreateContext();
            var reloaded = await new SqlServiceStore(read).GetAsync(original.Id, Ct);
            Assert.NotNull(reloaded);

            // Exactly one writer's role — a merge would leave two.
            var role = Assert.Single(reloaded!.Roles);
            Assert.Contains(role.ResourceType, new[] { "room", "person" });
        }
    }

    [Fact]
    public async Task Delete_unknown_id_fails()
    {
        fixture.EnsureAvailable();

        await using var context = fixture.CreateContext();
        var result = await new SqlServiceManagementStore(context).DeleteAsync(Guid.NewGuid(), Ct);

        Assert.False(result.Succeeded);
    }

    /// <summary>
    /// Two roles of one resource type, and a count above 1, against real SQL
    /// Server — the shapes the domain rule forbade until this change.
    /// <para>
    /// The claim being tested is that <b>no migration is needed</b>: <c>Count</c> is
    /// an existing column from ⑥ and the role table carries only a non-unique index
    /// on <c>ServiceId</c>. That is verifiable from the model snapshot, but a
    /// snapshot says what EF believes rather than what the database will accept, and
    /// a unique constraint added by hand would be invisible to it. So the round trip
    /// is run rather than reasoned about.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Two_same_type_roles_and_a_count_round_trip_without_a_migration()
    {
        fixture.EnsureAvailable();

        var senior = new ServiceRole("therapist", 1)
        {
            RequiredCapabilities = CapabilitySet.Create(["cert-x"], CapabilitySet.RequiredField).Value,
        };

        var service = Service.Create(
            "Joint session",
            ServiceDuration.Fixed(TimeSpan.FromMinutes(60)).Value,
            [senior, new ServiceRole("therapist", 2), new ServiceRole("room", 1)]).Value;

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlServiceManagementStore(context).CreateAsync(service, Ct)).Succeeded);
        }

        // Re-read twice, from separate contexts, because a stable order that came
        // from a change tracker rather than from the aggregate would satisfy one
        // read and not two.
        await using var first = fixture.CreateContext();
        var once = await new SqlServiceStore(first).GetAsync(service.Id, Ct);

        await using var second = fixture.CreateContext();
        var twice = await new SqlServiceStore(second).GetAsync(service.Id, Ct);

        Assert.NotNull(once);
        Assert.NotNull(twice);

        Assert.Equal(3, once!.Roles.Count);
        Assert.Equal(once.Roles, twice!.Roles);

        // The canonical order: `room` before `therapist` ordinally, then the two
        // therapist roles separated by their capabilities — empty before {cert-x}.
        Assert.Equal(["room", "therapist", "therapist"], once.Roles.Select(r => r.ResourceType));
        Assert.Equal([1, 2, 1], once.Roles.Select(r => r.Count));
        Assert.Equal(["cert-x"], once.Roles[2].RequiredCapabilities.Keys);
    }

    [Fact]
    public async Task A_visitor_selectable_role_round_trips_and_others_load_as_not_selectable()
    {
        fixture.EnsureAvailable();

        var service = Service.Create(
            "Massage",
            ServiceDuration.Fixed(TimeSpan.FromMinutes(60)).Value,
            [
                new ServiceRole("room", 1),
                new ServiceRole("therapist", 1) { VisitorSelectable = true },
            ]).Value;

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlServiceManagementStore(context).CreateAsync(service, Ct)).Succeeded);
        }

        await using var read = fixture.CreateContext();
        var fetched = await new SqlServiceStore(read).GetAsync(service.Id, Ct);

        Assert.NotNull(fetched);

        // Value-equal to what was saved, which is the whole claim: the flag is
        // part of the role's identity, so a column that failed to round-trip would
        // make the re-read service compare unequal.
        Assert.Equal(service.Roles, fetched!.Roles);

        Assert.True(Assert.Single(fetched.Roles, r => r.ResourceType == "therapist").VisitorSelectable);
        Assert.False(Assert.Single(fetched.Roles, r => r.ResourceType == "room").VisitorSelectable);
    }

    [Fact]
    public async Task A_role_row_written_without_the_column_loads_as_not_selectable()
    {
        // The migration's promise: the column is a `bit` with a false default, so
        // every role stored before it existed loads as not selectable and no
        // service changes behaviour. Written by an INSERT that names no
        // VisitorSelectable at all — which is exactly what a pre-upgrade row is.
        fixture.EnsureAvailable();

        var service = Service.Create(
            "Legacy",
            ServiceDuration.Fixed(TimeSpan.FromMinutes(60)).Value,
            [new ServiceRole("room", 1)]).Value;

        await using (var context = fixture.CreateContext())
        {
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO uBookItService (Id, Name, DurationKind, MinDurationMinutes, MaxDurationMinutes) "
                + "VALUES ({0}, {1}, {2}, {3}, {4})",
                [service.Id, service.Name, nameof(ServiceDurationKind.Fixed), 60, 60],
                Ct);

            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO uBookItServiceRole (ServiceId, ResourceType, Count) VALUES ({0}, {1}, {2})",
                [service.Id, "room", 1],
                Ct);
        }

        await using var read = fixture.CreateContext();
        var fetched = await new SqlServiceStore(read).GetAsync(service.Id, Ct);

        Assert.NotNull(fetched);
        Assert.False(Assert.Single(fetched!.Roles).VisitorSelectable);
    }
}
