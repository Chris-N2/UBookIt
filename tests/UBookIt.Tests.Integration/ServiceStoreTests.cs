using Microsoft.EntityFrameworkCore;
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
        => Service.Create(name, minutes is { } m ? TimeSpan.FromMinutes(m) : null, [new ServiceRole(type, 1)]).Value;

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
        Assert.Equal(TimeSpan.FromMinutes(60), fetched.Duration);
        Assert.Equal("person", Assert.Single(fetched.Roles).ResourceType);
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

        var updated = Service.Create("After", TimeSpan.FromMinutes(90), [new ServiceRole("room", 1)], service.Id).Value;
        await using (var c2 = fixture.CreateContext())
        {
            var result = await new SqlServiceManagementStore(c2).UpdateAsync(updated, Ct);
            Assert.True(result.Succeeded);
        }

        await using var read = fixture.CreateContext();
        var fetched = await new SqlServiceStore(read).GetAsync(service.Id, Ct);

        Assert.Equal("After", fetched!.Name);
        Assert.Equal(TimeSpan.FromMinutes(90), fetched.Duration);
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

    [Fact]
    public async Task Delete_unknown_id_fails()
    {
        fixture.EnsureAvailable();

        await using var context = fixture.CreateContext();
        var result = await new SqlServiceManagementStore(context).DeleteAsync(Guid.NewGuid(), Ct);

        Assert.False(result.Succeeded);
    }
}
