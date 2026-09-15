using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UBookIt.Persistence.Composing;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Exceptions;

namespace UBookIt.Tests.Integration;

/// <summary>
/// The startup checks, driven through <see cref="RunUBookItMigrations.HandleAsync"/> itself against
/// a real database.
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists because the unit-level guards could not fail.</b> They exercised
/// <c>EffectiveConfigurationOrRaw()</c> and <c>Report()</c> separately, which proves both pieces
/// work and proves nothing about the handler wiring them together — reverting the call site to
/// <c>Report(configuration, logger)</c> left every one of them green. Only running the handler can
/// tell whether a STORED value reaches the checks.
/// </para>
/// <para>
/// It needs a real database because the handler migrates before it reports, which is itself
/// deliberate: the settings table has to exist before the store can be read, and on a fresh install
/// it does not until the migration has run.
/// </para>
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class StartupReportTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class CapturingLogger : ILogger<RunUBookItMigrations>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }

    /// <summary>Same shape as the seed suite's stub: the handler only reads the level.</summary>
    private sealed class RunningRuntimeState : IRuntimeState
    {
        public RuntimeLevel Level => RuntimeLevel.Run;

        public string? CurrentMigrationState => null;

        public string? FinalMigrationState => null;

        public RuntimeLevelReason Reason => RuntimeLevelReason.Run;

        public Umbraco.Cms.Core.Semver.SemVersion SemanticVersion => null!;

        public Version Version => new(17, 0, 0);

        public string VersionComment => string.Empty;

        public BootFailedException? BootFailedException => null;

        public IReadOnlyDictionary<string, object> StartupState => new Dictionary<string, object>();

        public void Configure(RuntimeLevel level, RuntimeLevelReason reason, Exception? bootFailedException = null)
            => throw new NotSupportedException("The handler only reads the level.");

        public void DetermineRuntimeLevel()
            => throw new NotSupportedException("The handler only reads the level.");
    }

    private static IConfiguration SiteConfig(params (string Key, string Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v =>
                new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private async Task<CapturingLogger> RunStartupAsync(
        IConfiguration siteConfiguration, params (string Key, string Value)[] stored)
    {
        await using var context = fixture.CreateContext();
        var store = new SqlSettingsStore(context, TimeProvider.System);

        foreach (var (key, value) in stored)
        {
            await store.SetAsync(key, value, Ct);
        }

        var logger = new CapturingLogger();

        await new RunUBookItMigrations(
                context, new RunningRuntimeState(), siteConfiguration, store, logger)
            .HandleAsync(new UmbracoApplicationStartedNotification(false), Ct);

        foreach (var (key, _) in stored)
        {
            await store.RemoveAsync(key, Ct);
        }

        return logger;
    }

    [Fact]
    public async Task A_malformed_STORED_value_is_reported_at_startup()
    {
        fixture.EnsureAvailable();

        // The whole point: before the handler took the store, a value written through the settings
        // screen could be unreadable and the site would never be told.
        var logger = await RunStartupAsync(
            SiteConfig(),
            (UBookItPersistenceComposer.RetentionDaysSettingKey, "not-a-number"));

        Assert.Contains(
            logger.Entries,
            e => e.Level == LogLevel.Error
                && e.Message.Contains(UBookItPersistenceComposer.RetentionDaysSettingKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_site_whose_time_zone_is_STORED_is_not_warned_that_it_has_none()
    {
        fixture.EnsureAvailable();

        // The other direction, and the one a site would actually notice: a false "not configured,
        // defaulting to UTC" on every boot while the stored zone is what actually runs.
        var logger = await RunStartupAsync(
            SiteConfig(),
            (UBookItPersistenceComposer.TimeZoneSettingKey, "Europe/London"));

        Assert.DoesNotContain(
            logger.Entries,
            e => e.Level == LogLevel.Warning
                && e.Message.Contains(UBookItPersistenceComposer.TimeZoneSettingKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_site_with_no_time_zone_anywhere_is_still_warned()
    {
        fixture.EnsureAvailable();

        // Both directions. A handler that reported nothing at all would satisfy the test above
        // while having silently stopped checking.
        var logger = await RunStartupAsync(SiteConfig());

        Assert.Contains(
            logger.Entries,
            e => e.Level == LogLevel.Warning
                && e.Message.Contains(UBookItPersistenceComposer.TimeZoneSettingKey, StringComparison.Ordinal));
    }
}
