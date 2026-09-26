using Microsoft.EntityFrameworkCore;
using UBookIt.Persistence.Entities;
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

    /// <summary>
    /// The store's real capacity is <see cref="SettingRow.MaxValueLength"/> — the constant the
    /// settings screen's validation refuses beyond. Measured against the column the migrations
    /// build, so a migration that changed it would fail here rather than let the two disagree.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_value_at_the_declared_capacity_round_trips_intact(bool surrogatePairs)
    {
        fixture.EnsureAvailable();
        var key = Key();

        // U+1F600 is two UTF-16 code units, so this is MaxValueLength units and half as many
        // characters a reader would count: nvarchar and string.Length both count units, and this
        // proves they agree rather than asserting it.
        var value = surrogatePairs
            ? string.Concat(Enumerable.Repeat("\U0001F600", SettingRow.MaxValueLength / 2))
            : new string('a', SettingRow.MaxValueLength);

        Assert.Equal(SettingRow.MaxValueLength, value.Length);

        await using (var context = fixture.CreateContext())
        {
            await new SqlSettingsStore(context, TimeProvider.System).SetAsync(key, value, Ct);
        }

        await using var read = fixture.CreateContext();

        Assert.Equal(value, new SqlSettingsStore(read, TimeProvider.System).GetAll()[key]);
    }

    /// <summary>
    /// One unit past the capacity fails AT THE STORE — the defect validation now prevents, shown
    /// to be real rather than inferred from the schema.
    /// </summary>
    [Fact]
    public async Task A_value_past_the_declared_capacity_fails_at_the_store()
    {
        fixture.EnsureAvailable();

        await using var context = fixture.CreateContext();
        var store = new SqlSettingsStore(context, TimeProvider.System);

        // Specific about WHY it failed: any exception would also be thrown by a broken fixture, and
        // a test that passed on that would prove nothing about the column.
        var failure = await Assert.ThrowsAsync<DbUpdateException>(
            () => store.SetAsync(Key(), new string('a', SettingRow.MaxValueLength + 1), Ct));

        Assert.Contains("truncated", failure.InnerException?.Message, StringComparison.OrdinalIgnoreCase);
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
