using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// The read port's <c>ListAsync</c> against real SQL Server: paging returns a
/// page while reporting the unpaged total, and the deterministic ordering
/// (display name, then id) translates to SQL.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class ResourceStoreListTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Lists_paged_with_unpaged_total()
    {
        fixture.EnsureAvailable();

        // The SQL Server database is a shared collection fixture, so assertions
        // are relative to what this test seeds rather than absolute counts.
        var seeded = new[]
        {
            await Seed.EveryDayRoomAsync(fixture, Ct),
            await Seed.EveryDayRoomAsync(fixture, Ct),
            await Seed.EveryDayRoomAsync(fixture, Ct),
        };

        await using var context = fixture.CreateContext();
        var store = new SqlResourceStore(context, new SqlSiteClosureStore(context));

        var all = await store.ListAsync(skip: 0, take: 500, Ct);
        Assert.True(all.Total >= seeded.Length);
        Assert.All(seeded, id => Assert.Contains(id, all.Items.Select(r => r.Id)));

        // The total is the UNPAGED count — independent of take — and the page
        // size is honoured.
        var page = await store.ListAsync(skip: 0, take: 2, Ct);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(all.Total, page.Total);
    }
}
