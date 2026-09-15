using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// The settings store against real SQL Server, on the site-settings capability's terms: at most
/// one row per key, and the ABSENCE of a row is the only way to say "not overridden".
/// </summary>
/// <remarks>
/// The absence rule is what the reset action depends on, and it is why these tests assert on the
/// key being gone rather than on some stored marker. A store that recorded "cleared" would pass a
/// round-trip test and still break reset, because the configured value would never show through
/// again.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class SettingsStoreTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Key() => $"UBookIt:TestSetting:{Guid.NewGuid():N}";

    [Fact]
    public async Task A_setting_round_trips()
    {
        fixture.EnsureAvailable();
        var key = Key();

        await using (var context = fixture.CreateContext())
        {
            await new SqlSettingsStore(context, TimeProvider.System).SetAsync(key, "first", Ct);
        }

        await using var read = fixture.CreateContext();
        var all = new SqlSettingsStore(read, TimeProvider.System).GetAll();

        Assert.Equal("first", all[key]);
    }

    [Fact]
    public async Task Storing_the_same_key_twice_replaces_rather_than_duplicates()
    {
        fixture.EnsureAvailable();
        var key = Key();

        await using var context = fixture.CreateContext();
        var store = new SqlSettingsStore(context, TimeProvider.System);

        await store.SetAsync(key, "first", Ct);
        await store.SetAsync(key, "second", Ct);

        var all = store.GetAll();

        Assert.Equal("second", all[key]);

        // One row, enforced by the key being the primary key rather than by the store's code.
        await using var read = fixture.CreateContext();
        Assert.Equal(1, read.Settings.Count(s => s.Key == key));
    }

    [Fact]
    public async Task Removing_a_setting_leaves_no_trace_of_it()
    {
        fixture.EnsureAvailable();
        var key = Key();

        await using var context = fixture.CreateContext();
        var store = new SqlSettingsStore(context, TimeProvider.System);

        await store.SetAsync(key, "stored", Ct);
        await store.RemoveAsync(key, Ct);

        // ABSENT, not present-and-blank. A row carrying an empty string would be a second way of
        // spelling "not overridden", and the two would resolve differently.
        Assert.DoesNotContain(key, (store.GetAll()).Keys);

        await using var read = fixture.CreateContext();
        Assert.Equal(0, read.Settings.Count(s => s.Key == key));
    }

    [Fact]
    public async Task Removing_a_setting_that_was_never_stored_is_not_an_error()
    {
        fixture.EnsureAvailable();

        await using var context = fixture.CreateContext();

        // The ordinary case for most settings: reset on something never overridden. The requested
        // state and the resulting state are the same either way.
        await new SqlSettingsStore(context, TimeProvider.System).RemoveAsync(Key(), Ct);
    }

    [Fact]
    public async Task Keys_are_returned_verbatim()
    {
        fixture.EnsureAvailable();
        var key = Key();

        await using var context = fixture.CreateContext();
        var store = new SqlSettingsStore(context, TimeProvider.System);

        await store.SetAsync(key, "value", Ct);

        // Verbatim matters: these keys are composed over IConfiguration, whose section separator
        // is meaningful. A store that normalised case or separators would silently fail to
        // override the setting it named.
        Assert.Contains(key, (store.GetAll()).Keys);
    }
}
