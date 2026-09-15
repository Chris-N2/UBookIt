using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UBookIt.Core;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Composing;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The two-source resolution: stored settings composed over the site's configuration, so a stored
/// value wins and an unstored key falls through.
/// </summary>
/// <remarks>
/// <para>
/// <b>The claim these tests exist to protect is not "the database wins".</b> It is that a stored
/// value and a configured value go through the <i>same reader and the same fallbacks</i> — because
/// the package's fallbacks are deliberately asymmetric (an unreadable query range becomes a working
/// default; an unreadable retention period becomes NO retention, since the cost of being wrong is
/// destroying personal data), and a second implementation of those would drift silently.
/// </para>
/// <para>
/// So the fallback tests below assert <i>equality between the two sources</i> rather than asserting
/// a particular value twice. A test that checked "stored garbage gives no retention" would still
/// pass if the stored path grew its own private copy of the rule; one that checks the stored and
/// configured paths agree cannot.
/// </para>
/// </remarks>
public class EffectiveConfigurationTests
{
    /// <summary>A store holding exactly what it is given. Nothing here touches a database.</summary>
    private sealed class StubSettingsStore(params (string Key, string Value)[] rows) : ISettingsStore
    {
        public IReadOnlyDictionary<string, string> GetAll()
            => rows.ToDictionary(r => r.Key, r => r.Value, StringComparer.Ordinal);

        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private static IConfiguration SiteConfig(params (string Key, string Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v =>
                new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private static SiteBookingSettings Resolve(
        IConfiguration site, params (string Key, string Value)[] stored)
        => UBookItPersistenceComposer.ResolveSettings(
            UBookItPersistenceComposer.EffectiveConfiguration(site, new StubSettingsStore(stored)));

    /// <summary>
    /// A value-comparable description of every setting on the record.
    /// </summary>
    /// <remarks>
    /// <b>Record equality cannot be used here, and finding out why is the point.</b>
    /// <c>SiteBookingSettings</c> is a record, but <c>Notifications.InternalRecipients</c> is an
    /// <c>IReadOnlyList&lt;string&gt;</c>, and a synthesised record equality compares that member
    /// by REFERENCE. Two separately resolved records are therefore never equal — not even two
    /// resolutions of the identical configuration. Asserting on record equality would have made
    /// every comparison below fail for a reason that has nothing to do with the two sources.
    ///
    /// This projects every field, so the comparison stays whole-record in the sense that matters:
    /// a stored path that got one fallback right and another wrong is still caught.
    /// </remarks>
    private static object Describe(SiteBookingSettings settings) => new
    {
        settings.TimeZoneId,
        settings.MaxQueryRangeDays,
        settings.RetentionDays,
        settings.PrivacyPolicyUrl,
        settings.AutoConfirm,
        settings.Notifications.SendBookerEmails,
        InternalRecipients = string.Join("|", settings.Notifications.InternalRecipients),
    };

    // ---- precedence -------------------------------------------------------------------------

    [Fact]
    public void A_stored_value_overrides_a_configured_one()
    {
        var settings = Resolve(
            SiteConfig((UBookItPersistenceComposer.TimeZoneSettingKey, "Europe/London")),
            (UBookItPersistenceComposer.TimeZoneSettingKey, "America/New_York"));

        Assert.Equal("America/New_York", settings.TimeZoneId);
    }

    [Fact]
    public void An_unstored_key_falls_through_to_configuration()
    {
        // The store holds a DIFFERENT key, so this also proves the overlay is per-key rather than
        // wholesale — a layer that replaced the section would blank everything it did not carry.
        var settings = Resolve(
            SiteConfig((UBookItPersistenceComposer.TimeZoneSettingKey, "Europe/London")),
            (UBookItPersistenceComposer.AutoConfirmSettingKey, "false"));

        Assert.Equal("Europe/London", settings.TimeZoneId);
        Assert.False(settings.AutoConfirm);
    }

    [Fact]
    public void An_empty_store_changes_nothing()
    {
        var site = SiteConfig(
            (UBookItPersistenceComposer.TimeZoneSettingKey, "Europe/London"),
            (UBookItPersistenceComposer.RetentionDaysSettingKey, "90"),
            (UBookItPersistenceComposer.AutoConfirmSettingKey, "false"));

        var withStore = Resolve(site);
        var withoutStore = UBookItPersistenceComposer.ResolveSettings(site);

        // Every field, not one: this is the "a fresh install resolves exactly as before"
        // guarantee, and asserting one property would let the overlay quietly alter another.
        Assert.Equal(Describe(withoutStore), Describe(withStore));
    }

    [Fact]
    public void Configuration_from_outside_appsettings_still_shows_through()
    {
        // Environment variables reach IConfiguration as ordinary providers, so what this really
        // pins is that the site's configuration is composed as the BASE LAYER rather than being
        // replaced — a site using env vars or a secret store keeps them.
        var site = new ConfigurationBuilder()
            .AddInMemoryCollection([
                new KeyValuePair<string, string?>(UBookItPersistenceComposer.TimeZoneSettingKey, "Europe/London"),
            ])
            .Build();

        Assert.Equal("Europe/London", Resolve(site).TimeZoneId);
    }

    // ---- the same path, proved by equality ---------------------------------------------------

    public static TheoryData<string, string> UnreadableValues => new()
    {
        { UBookItPersistenceComposer.RetentionDaysSettingKey, "not-a-number" },
        { UBookItPersistenceComposer.RetentionDaysSettingKey, "-5" },
        { UBookItPersistenceComposer.RetentionDaysSettingKey, "0" },
        { UBookItPersistenceComposer.AutoConfirmSettingKey, "banana" },
        { UBookItPersistenceComposer.MaxQueryRangeDaysSettingKey, "not-a-number" },
        { UBookItPersistenceComposer.MaxQueryRangeDaysSettingKey, "-1" },
        { UBookItPersistenceComposer.PrivacyPolicyUrlSettingKey, "javascript:alert(1)" },
        { UBookItPersistenceComposer.TimeZoneSettingKey, "   " },
    };

    [Theory]
    [MemberData(nameof(UnreadableValues))]
    public void A_stored_value_and_a_configured_value_resolve_identically(string key, string value)
    {
        var asConfigured = UBookItPersistenceComposer.ResolveSettings(SiteConfig((key, value)));
        var asStored = Resolve(SiteConfig(), (key, value));

        // Every field, not just the one under test. The point is that NO fallback differs
        // between the sources, and comparing only the field named would miss a stored path that
        // got one right and another wrong.
        Assert.Equal(Describe(asConfigured), Describe(asStored));
    }

    [Theory]
    [MemberData(nameof(UnreadableValues))]
    public void A_stored_value_and_a_configured_value_report_identically(string key, string value)
    {
        // The startup reports read the configuration, not the settings record, so they have to be
        // pointed at the EFFECTIVE configuration or a site would be told nothing about a bad value
        // it saved through the backoffice. Same text, same source-independent complaint.
        var configuredLog = new CapturingLogger();
        var storedLog = new CapturingLogger();

        RunAllReports(SiteConfig((key, value)), configuredLog);
        RunAllReports(
            UBookItPersistenceComposer.EffectiveConfiguration(
                SiteConfig(), new StubSettingsStore((key, value))),
            storedLog);

        Assert.Equal(
            configuredLog.Entries.Select(e => (e.Level, e.Message)),
            storedLog.Entries.Select(e => (e.Level, e.Message)));
    }

    /// <summary>The same shape as the one in SiteSettingsTests, whose reports these are.</summary>
    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }

    /// <summary>
    /// The startup checks, through <see cref="RunUBookItMigrations.Report"/> — the SAME entry point
    /// production calls.
    /// </summary>
    /// <remarks>
    /// <b>This used to call the four checks individually.</b> That made the test assert a property
    /// the shipped code did not have: production passed the RAW configuration to those checks, so a
    /// malformed stored value was never reported and a site whose time zone was stored was warned
    /// every boot that it had none. The test could not fail, because it composed the effective
    /// configuration itself and then checked its own wiring. Going through the production entry
    /// point is what makes the assertion about production.
    /// </remarks>
    private static void RunAllReports(IConfiguration configuration, ILogger logger)
        => RunUBookItMigrations.Report(configuration, logger);

    // ---- the lifetime ------------------------------------------------------------------------

    /// <summary>A store whose rows can be changed between reads, as the backoffice would.</summary>
    private sealed class MutableSettingsStore : ISettingsStore
    {
        private readonly Dictionary<string, string> _rows = new(StringComparer.Ordinal);

        public int Reads { get; private set; }

        public IReadOnlyDictionary<string, string> GetAll()
        {
            Reads++;
            return new Dictionary<string, string>(_rows, StringComparer.Ordinal);
        }

        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            _rows[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _rows.Remove(key);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Two_scopes_either_side_of_a_change_see_old_and_new()
    {
        // The whole point of the lifetime change: a setting saved in the backoffice takes effect
        // on the next request, not the next deployment.
        //
        // This builds its OWN container, so it proves the composition behaves correctly WHEN
        // registered as scoped — it does not prove the composer registers it that way. That is a
        // separate claim and has its own guard below, because a test asserting over a container it
        // configured itself can never notice the production registration changing underneath it.
        var store = new MutableSettingsStore();
        var site = SiteConfig((UBookItPersistenceComposer.TimeZoneSettingKey, "Europe/London"));

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(site);
        services.AddSingleton<ISettingsStore>(store);
        services.AddScoped(sp => UBookItPersistenceComposer.ResolveSettings(
            UBookItPersistenceComposer.EffectiveConfiguration(
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<ISettingsStore>())));

        var provider = services.BuildServiceProvider();

        using (var before = provider.CreateScope())
        {
            Assert.Equal(
                "Europe/London",
                before.ServiceProvider.GetRequiredService<SiteBookingSettings>().TimeZoneId);
        }

        await store.SetAsync(UBookItPersistenceComposer.TimeZoneSettingKey, "America/New_York");

        using (var after = provider.CreateScope())
        {
            Assert.Equal(
                "America/New_York",
                after.ServiceProvider.GetRequiredService<SiteBookingSettings>().TimeZoneId);
        }

        // The application never restarted, and the provider was never rebuilt.
        Assert.True(store.Reads >= 2, "The settings must be re-read per scope, not cached for the provider's life.");
    }

    [Fact]
    public void One_scope_reads_the_store_once_however_many_consumers_resolve_the_settings()
    {
        // Scoped, not transient. Fifteen consumers take SiteBookingSettings by value, and a
        // transient registration would query the settings table once per constructor.
        var store = new MutableSettingsStore();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(SiteConfig());
        services.AddSingleton<ISettingsStore>(store);
        services.AddScoped(sp => UBookItPersistenceComposer.ResolveSettings(
            UBookItPersistenceComposer.EffectiveConfiguration(
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<ISettingsStore>())));

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<SiteBookingSettings>();
        scope.ServiceProvider.GetRequiredService<SiteBookingSettings>();
        scope.ServiceProvider.GetRequiredService<SiteBookingSettings>();

        Assert.Equal(1, store.Reads);
    }

    [Fact]
    public void The_composer_registers_the_settings_as_SCOPED()
    {
        // The claim the test above cannot make. Asserted against the real composer's registration
        // list: if this reverts to AddSingleton, a setting saved in the backoffice silently stops
        // taking effect until the site restarts, and every behavioural test would still pass
        // because they build their own containers.
        var registrations = new ServiceCollection();
        new UBookItPersistenceComposer().Compose(new ServicesOnlyUmbracoBuilder(registrations));

        var settings = Assert.Single(
            registrations, d => d.ServiceType == typeof(SiteBookingSettings));

        Assert.Equal(ServiceLifetime.Scoped, settings.Lifetime);
    }

    /// <summary>A store that throws, to exercise the startup fallback.</summary>
    private sealed class ThrowingSettingsStore : ISettingsStore
    {
        public IReadOnlyDictionary<string, string> GetAll()
            => throw new InvalidOperationException("the settings table is not reachable");

        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private static RunUBookItMigrations Handler(IConfiguration configuration, ISettingsStore store)
        => new(null!, null!, configuration, store, NullLogger<RunUBookItMigrations>.Instance);

    [Fact]
    public void The_startup_handler_reports_against_the_EFFECTIVE_configuration_not_the_raw_one()
    {
        // BEHAVIOURAL, not structural. An earlier version of this test asserted only that the
        // constructor took ISettingsStore — which a regression could satisfy while leaving the
        // parameter unused, so the guard stayed green through the exact fault it names.
        //
        // Here the store holds a value the site's configuration does not, and the configuration
        // the handler produces must carry it.
        var effective = Handler(
                SiteConfig(),
                new StubSettingsStore((UBookItPersistenceComposer.TimeZoneSettingKey, "America/New_York")))
            .EffectiveConfigurationOrRaw();

        Assert.Equal("America/New_York", effective[UBookItPersistenceComposer.TimeZoneSettingKey]);
    }

    [Fact]
    public void A_stored_value_is_what_the_startup_checks_see()
    {
        // The consequence that matters: a malformed STORED value must be reported, and a site whose
        // time zone is stored must not be warned every boot that it has none.
        var logger = new CapturingLogger();
        var handler = Handler(
            SiteConfig(),
            new StubSettingsStore(
                (UBookItPersistenceComposer.TimeZoneSettingKey, "Europe/London"),
                (UBookItPersistenceComposer.RetentionDaysSettingKey, "not-a-number")));

        RunUBookItMigrations.Report(handler.EffectiveConfigurationOrRaw(), logger);

        Assert.DoesNotContain(
            logger.Entries,
            e => e.Level == LogLevel.Warning
                && e.Message.Contains(UBookItPersistenceComposer.TimeZoneSettingKey, StringComparison.Ordinal));

        Assert.Contains(
            logger.Entries,
            e => e.Level == LogLevel.Error
                && e.Message.Contains(UBookItPersistenceComposer.RetentionDaysSettingKey, StringComparison.Ordinal));
    }

    [Fact]
    public void An_unreachable_store_falls_back_to_the_site_configuration_rather_than_failing_boot()
    {
        // The catch arm, which otherwise has no test and runs only when something is already wrong.
        // Falling silent here would suppress the message on exactly the boot that needs it.
        var handler = Handler(
            SiteConfig((UBookItPersistenceComposer.TimeZoneSettingKey, "Europe/London")),
            new ThrowingSettingsStore());

        var effective = handler.EffectiveConfigurationOrRaw();

        Assert.Equal("Europe/London", effective[UBookItPersistenceComposer.TimeZoneSettingKey]);
    }

    // ---- the absence rule --------------------------------------------------------------------

    [Fact]
    public void Removing_a_stored_value_restores_the_configured_one()
    {
        var site = SiteConfig((UBookItPersistenceComposer.TimeZoneSettingKey, "Europe/London"));

        var overridden = Resolve(site, (UBookItPersistenceComposer.TimeZoneSettingKey, "UTC"));
        var restored = Resolve(site);

        Assert.Equal("UTC", overridden.TimeZoneId);
        Assert.Equal("Europe/London", restored.TimeZoneId);
    }

    [Fact]
    public void A_stored_blank_is_a_value_and_not_an_absence()
    {
        // Storing a blank must NOT be a way of spelling "not overridden" — that is what removing
        // the row is for. A blank time zone is an unreadable value, so it falls back to UTC and
        // does not resurrect the configured London.
        var settings = Resolve(
            SiteConfig((UBookItPersistenceComposer.TimeZoneSettingKey, "Europe/London")),
            (UBookItPersistenceComposer.TimeZoneSettingKey, "  "));

        Assert.Equal("UTC", settings.TimeZoneId);
    }
}
