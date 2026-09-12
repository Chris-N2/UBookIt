using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// The one-shot flag store against real SQL Server, on the persistence delta's terms: a
/// flag exists or it does not, carries only when it was applied, and writing twice is a
/// no-op rather than an error.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class FlagStoreTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_flag_round_trips()
    {
        fixture.EnsureAvailable();
        var key = $"test-flag-{Guid.NewGuid():N}";

        await using (var context = fixture.CreateContext())
        {
            var store = new SqlFlagStore(context, TimeProvider.System);

            Assert.False(await store.ExistsAsync(key, Ct));
            await store.SetAsync(key, Ct);
        }

        await using var read = fixture.CreateContext();

        Assert.True(await new SqlFlagStore(read, TimeProvider.System).ExistsAsync(key, Ct));
    }

    [Fact]
    public async Task Setting_a_flag_twice_is_a_no_op()
    {
        fixture.EnsureAvailable();
        var key = $"test-flag-{Guid.NewGuid():N}";

        await using var context = fixture.CreateContext();
        var store = new SqlFlagStore(context, TimeProvider.System);

        await store.SetAsync(key, Ct);
        await store.SetAsync(key, Ct); // must not throw on the existing primary key

        Assert.True(await store.ExistsAsync(key, Ct));
    }

    [Fact]
    public async Task An_absent_flag_is_absent()
    {
        fixture.EnsureAvailable();

        await using var context = fixture.CreateContext();

        Assert.False(await new SqlFlagStore(context, TimeProvider.System)
            .ExistsAsync($"never-set-{Guid.NewGuid():N}", Ct));
    }
}
