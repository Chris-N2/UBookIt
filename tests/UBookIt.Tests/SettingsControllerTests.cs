using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using UBookIt.Backoffice.Controllers;
using UBookIt.Backoffice.Models;
using UBookIt.Backoffice.Settings;
using UBookIt.Core;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Composing;

namespace UBookIt.Tests;

/// <summary>
/// The settings endpoint: that the tier boundary is held by the SERVER, that the read shows what
/// a stored value is overriding, and that resetting removes rather than stores.
/// </summary>
public class SettingsControllerTests
{
    private sealed class FakeSettingsStore : ISettingsStore
    {
        private readonly Dictionary<string, string> _rows = new(StringComparer.Ordinal);

        public List<string> Removed { get; } = [];

        public List<(string Key, string Value)> Written { get; } = [];

        public IReadOnlyDictionary<string, string> GetAll() => _rows;

        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            _rows[key] = value;
            Written.Add((key, value));
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _rows.Remove(key);
            Removed.Add(key);
            return Task.CompletedTask;
        }

        public void Seed(string key, string value) => _rows[key] = value;
    }

    private static IConfiguration Config(params (string Key, string Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v =>
                new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    /// <summary>
    /// The controller wired the way the container wires it: settings resolved through the REAL
    /// <c>EffectiveConfiguration</c> + <c>ResolveSettings</c> path over this store and config.
    /// </summary>
    /// <remarks>
    /// <b>Never hand-build the effective settings here.</b> A test that composes its own would
    /// assert a property the shipped wiring does not have and could not fail when production
    /// stopped having it — which is exactly the defect QA found in the startup reports.
    /// </remarks>
    private static SettingsController Controller(
        ISettingsStore store, IConfiguration configuration, bool selfServiceCancellation = false)
        => new(
            store,
            configuration,
            UBookItPersistenceComposer.ResolveSettings(
                UBookItPersistenceComposer.EffectiveConfiguration(configuration, store)),
            new SelfServiceCancellationSettings { Enabled = selfServiceCancellation });

    private static SettingsResponseModel Read(SettingsController controller)
        => Assert.IsType<SettingsResponseModel>(
            Assert.IsType<OkObjectResult>(controller.GetSettings()).Value);

    private static SettingResponseModel Setting(SettingsResponseModel model, string key)
        => Assert.Single(model.Settings, s => s.Key == key);

    // ---- the tier boundary, held by the server -----------------------------------------------

    [Fact]
    public async Task A_read_only_setting_cannot_be_written()
    {
        // THE claim: the client does not render an input for the retention period, but this
        // endpoint is reachable without the client. If only the client held the boundary, the
        // one setting that irreversibly destroys personal data would be a POST away.
        var store = new FakeSettingsStore();
        var controller = Controller(store, Config());

        var result = await controller.PutSetting(
            UBookItPersistenceComposer.RetentionDaysSettingKey, new SettingWriteModel { Value = "1" });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(store.Written);
    }

    [Fact]
    public async Task Every_read_only_setting_is_refused_a_write()
    {
        // The CLASS, not the instance. Retention is the one that matters most, but a boundary
        // that held for one key and leaked for another would be no boundary — and the delivery
        // API exposure keys are the package's largest anonymous surface.
        var store = new FakeSettingsStore();
        var controller = Controller(store, Config());

        var readOnly = SettingCatalogue.All.Where(s => !s.IsEditable).ToList();

        Assert.NotEmpty(readOnly);

        foreach (var descriptor in readOnly)
        {
            var result = await controller.PutSetting(
                descriptor.Key, new SettingWriteModel { Value = "true" });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        Assert.Empty(store.Written);
    }

    [Fact]
    public async Task A_key_that_is_not_a_uBookIt_setting_is_refused()
    {
        // This is what keeps the settings table closed to arbitrary keys — and so what makes
        // "no personal data reaches this table" structural rather than a promise.
        var store = new FakeSettingsStore();
        var controller = Controller(store, Config());

        Assert.IsType<NotFoundObjectResult>(
            await controller.PutSetting("UBookIt:Whatever", new SettingWriteModel { Value = "x" }));
        Assert.IsType<NotFoundObjectResult>(await controller.ResetSetting("UBookIt:Whatever"));
        Assert.Empty(store.Written);
    }

    // ---- validation on write ------------------------------------------------------------------

    [Theory]
    [InlineData("not-a-timezone")]
    [InlineData("   ")]
    public async Task An_invalid_value_is_refused_rather_than_stored_and_fallen_back_on(string value)
    {
        // Without this the operator types a bad zone, the save appears to work, and the site
        // silently runs on UTC — the exact silent failure the resolver's fallback would produce.
        var store = new FakeSettingsStore();
        var controller = Controller(store, Config());

        var result = await controller.PutSetting(
            UBookItPersistenceComposer.TimeZoneSettingKey, new SettingWriteModel { Value = value });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(store.Written);
    }

    [Fact]
    public async Task A_valid_value_is_stored_verbatim()
    {
        var store = new FakeSettingsStore();
        var controller = Controller(store, Config());

        var result = await controller.PutSetting(
            UBookItPersistenceComposer.AutoConfirmSettingKey, new SettingWriteModel { Value = "false" });

        Assert.IsType<OkResult>(result);

        // Verbatim: the value is composed as a configuration layer and read by the same code that
        // reads appsettings, so any normalisation here would be a second parser.
        Assert.Equal((UBookItPersistenceComposer.AutoConfirmSettingKey, "false"), Assert.Single(store.Written));
    }

    // ---- the policy link: the screen and the site answer "usable?" the same way --------------

    /// <summary>
    /// Usable links with surrounding whitespace: the site trims and uses them, so the screen must
    /// accept them. Seam-only, because <c>SiteSettingsTests.UsablePolicyLinks</c> asserts the
    /// resolved link is identical to what was configured, and here it is the trimmed value.
    /// </summary>
    /// <remarks>
    /// FOUND BY QA (round 2): without these, a validator that refused any padded value passed every
    /// case — the one direction of disagreement the shared lists could not express — while the
    /// design claimed the seam test asserted it.
    /// </remarks>
    public static TheoryData<string> PaddedPolicyLinks => new()
    {
        " /privacy ",
        " https://example.com/privacy ",
    };

    /// <summary>
    /// The seam between the settings screen and the site: for every policy link either side has an
    /// opinion about, the screen accepts it exactly when the site would use it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This was false, on every platform, until the validator started calling the resolver's
    /// rule.</b> The screen demanded an absolute http(s) address while the resolver and the
    /// documentation accepted <c>/privacy</c>, so an editor could not store the documented form.
    /// </para>
    /// <para>
    /// <b>"Would the site use it" is asked of CONFIGURATION, never of what the screen stored.</b>
    /// A refused value is not stored, so resolving through the store would answer "no link" for
    /// every refusal and the two sides would agree vacuously — the assertion would hold for a
    /// screen that refused everything. The site's answer has to be independent of the screen's.
    /// </para>
    /// <para>
    /// The values are <see cref="SiteSettingsTests"/>' own lists, not a copy, so a case added
    /// there is tested at this seam without anyone remembering to — plus
    /// <see cref="PaddedPolicyLinks"/>, which is local because the shared usable list cannot hold a
    /// value whose resolved link differs from its configured text.
    /// </para>
    /// <para>
    /// <b>What it cannot see:</b> the store. <c>FakeSettingsStore</c> holds any length, so the
    /// store's real capacity is pinned elsewhere: the length tests below refuse anything longer
    /// than <c>SettingRow.MaxValueLength</c>, and the integration suite's <c>SettingsStoreTests</c>
    /// prove that constant IS the column's capacity against SQL Server.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(SiteSettingsTests.UsablePolicyLinks), MemberType = typeof(SiteSettingsTests))]
    [MemberData(nameof(SiteSettingsTests.UnusablePolicyLinks), MemberType = typeof(SiteSettingsTests))]
    [MemberData(nameof(PaddedPolicyLinks))]
    public async Task The_screen_accepts_a_policy_link_exactly_when_the_site_would_use_it(string value)
    {
        var key = UBookItPersistenceComposer.PrivacyPolicyUrlSettingKey;

        var siteWouldUse = UBookItPersistenceComposer.ResolveSettings(Config((key, value))).PrivacyPolicyUrl;

        var store = new FakeSettingsStore();
        var result = await Controller(store, Config()).PutSetting(key, new SettingWriteModel { Value = value });

        if (siteWouldUse is null)
        {
            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(store.Written);
            return;
        }

        Assert.IsType<OkResult>(result);

        // And what the screen stored is what the site then links to, through the real
        // store-over-configuration path rather than the value the test happens to hold.
        var resolvedFromStore = UBookItPersistenceComposer.ResolveSettings(
            UBookItPersistenceComposer.EffectiveConfiguration(Config(), store)).PrivacyPolicyUrl;

        Assert.Equal(siteWouldUse, resolvedFromStore);

        // And the screen, reading back, reports as effective what the site links to.
        Assert.Equal(siteWouldUse, Setting(Read(Controller(store, Config())), key).EffectiveValue);
    }

    [Fact]
    public async Task The_policy_link_refusal_names_both_accepted_forms()
    {
        var result = await Controller(new FakeSettingsStore(), Config()).PutSetting(
            UBookItPersistenceComposer.PrivacyPolicyUrlSettingKey,
            new SettingWriteModel { Value = "privacy" });

        var problem = Assert.IsAssignableFrom<Microsoft.AspNetCore.Mvc.ProblemDetails>(
            Assert.IsType<BadRequestObjectResult>(result).Value);

        Assert.Contains("http or https address", problem.Detail);
        Assert.Contains("site-relative path beginning with a single '/'", problem.Detail);
    }

    // ---- nothing reaches the store that the store cannot hold ---------------------------------

    private const int Limit = UBookIt.Persistence.Entities.SettingRow.MaxValueLength;

    /// <summary>A usable policy link exactly <paramref name="length"/> characters long.</summary>
    private static string LinkOfLength(int length)
    {
        const string prefix = "https://example.com/";

        return prefix + new string('a', length - prefix.Length);
    }

    /// <summary>An email address exactly <paramref name="length"/> characters long.</summary>
    private static string AddressOfLength(int length)
    {
        const string domain = "@example.com";

        return new string('a', length - domain.Length) + domain;
    }

    [Fact]
    public async Task A_value_longer_than_the_store_holds_is_refused_before_the_store()
    {
        // Without this the value passed validation and failed at SQL Server (see the integration
        // suite's SettingsStoreTests) instead of being refused with a message against the setting.
        // The link is otherwise usable, so length is the only reason left for the refusal.
        var store = new FakeSettingsStore();
        var value = LinkOfLength(Limit + 1);

        Assert.True(UBookItPersistenceComposer.TryGetUsablePolicyLink(value, out _));

        var result = await Controller(store, Config()).PutSetting(
            UBookItPersistenceComposer.PrivacyPolicyUrlSettingKey, new SettingWriteModel { Value = value });

        var problem = Assert.IsAssignableFrom<ProblemDetails>(
            Assert.IsType<BadRequestObjectResult>(result).Value);

        Assert.Equal(SettingsController.SettingValueInvalid, problem.Type);
        Assert.Contains(Limit.ToString(System.Globalization.CultureInfo.InvariantCulture), problem.Detail);
        Assert.Empty(store.Written);
    }

    [Fact]
    public async Task A_value_at_the_store_limit_is_written_as_the_rows_the_store_will_hold()
    {
        var store = new FakeSettingsStore();
        var key = UBookItPersistenceComposer.PrivacyPolicyUrlSettingKey;
        var value = LinkOfLength(Limit);

        var result = await Controller(store, Config()).PutSetting(key, new SettingWriteModel { Value = value });

        Assert.IsType<OkResult>(result);

        // The rows written are the rows validation measured, and every one fits.
        var expected = SettingText.RowsFor(SettingCatalogue.All.Single(s => s.Key == key), value, Config());

        Assert.Equal(expected.Select(r => (r.Key, r.Value)), store.Written);
        Assert.All(store.Written, row => Assert.True(row.Value.Length <= Limit));
    }

    [Fact]
    public async Task A_recipient_list_longer_than_the_store_limit_in_total_is_still_stored()
    {
        // THE REGRESSION GUARD. A list is stored one row per address, so the store has never
        // limited its total length — only each address. A check on the submitted string would
        // have refused a list that works today, which a patch must never do.
        var addresses = Enumerable.Range(0, 200).Select(i => $"person{i}@example.com").ToList();
        var value = string.Join(", ", addresses);

        Assert.True(value.Length > Limit);

        var store = new FakeSettingsStore();
        var result = await Controller(store, Config()).PutSetting(
            UBookItPersistenceComposer.InternalRecipientsSettingKey, new SettingWriteModel { Value = value });

        Assert.IsType<OkResult>(result);
        Assert.Equal(addresses, store.Written.Select(row => row.Value));
    }

    [Fact]
    public async Task A_recipient_list_with_one_address_the_store_cannot_hold_is_refused_whole()
    {
        var value = $"ops@example.com, {AddressOfLength(Limit + 1)}";
        var store = new FakeSettingsStore();

        var result = await Controller(store, Config()).PutSetting(
            UBookItPersistenceComposer.InternalRecipientsSettingKey, new SettingWriteModel { Value = value });

        var problem = Assert.IsAssignableFrom<ProblemDetails>(
            Assert.IsType<BadRequestObjectResult>(result).Value);

        Assert.Contains("Each address", problem.Detail);
        Assert.Contains(Limit.ToString(System.Globalization.CultureInfo.InvariantCulture), problem.Detail);

        // Whole: not the first address alone. Nothing is written, so no stale rows are swept either.
        Assert.Empty(store.Written);
        Assert.Empty(store.Removed);
    }

    [Theory]
    [InlineData(SettingValueKind.Text)]
    [InlineData(SettingValueKind.Url)]
    public void The_limit_is_the_boundary_whatever_the_setting_type(SettingValueKind kind)
    {
        // No catalogue setting is free text today, but the kind exists and the requirement says
        // every type. A constructed descriptor reaches it without inventing a setting. The free
        // text is plain letters, so nothing but length can be in play for it (QA round 1).
        var descriptor = new SettingDescriptor("UBookIt:Test", SettingTier.Editable, kind);
        Func<int, string> ofLength = kind == SettingValueKind.Url
            ? LinkOfLength
            : length => new string('a', length);

        Assert.True(SettingValidation.IsValid(descriptor, ofLength(Limit), out var atLimit), atLimit);
        Assert.False(SettingValidation.IsValid(descriptor, ofLength(Limit + 1), out var over));
        Assert.Equal($"Must be no longer than {Limit} characters.", over);
    }

    [Fact]
    public void A_blank_value_is_still_told_it_is_blank()
    {
        var descriptor = SettingCatalogue.All.Single(
            s => s.Key == UBookItPersistenceComposer.PrivacyPolicyUrlSettingKey);

        Assert.False(SettingValidation.IsValid(descriptor, "   ", out var error));
        Assert.StartsWith("A value is required.", error);
    }

    // ---- the read shows what is being overridden ----------------------------------------------

    [Fact]
    public void The_read_reports_the_configured_value_beside_the_effective_one()
    {
        // What stops the configuration file becoming a silent lie: once anything is stored, the
        // file no longer describes what runs, so the divergence is shown where somebody looks.
        var store = new FakeSettingsStore();
        store.Seed(UBookItPersistenceComposer.TimeZoneSettingKey, "America/New_York");

        var controller = Controller(store, Config((UBookItPersistenceComposer.TimeZoneSettingKey, "Europe/London")));

        var timeZone = Setting(Read(controller), UBookItPersistenceComposer.TimeZoneSettingKey);

        Assert.Equal("America/New_York", timeZone.EffectiveValue);
        Assert.Equal("Europe/London", timeZone.ConfiguredValue);
        Assert.True(timeZone.IsOverridden);
    }

    [Fact]
    public void An_unstored_setting_reports_the_configured_value_as_effective()
    {
        var controller = Controller(new FakeSettingsStore(), Config((UBookItPersistenceComposer.TimeZoneSettingKey, "Europe/London")));

        var timeZone = Setting(Read(controller), UBookItPersistenceComposer.TimeZoneSettingKey);

        Assert.Equal("Europe/London", timeZone.EffectiveValue);
        Assert.Equal("Europe/London", timeZone.ConfiguredValue);
        Assert.False(timeZone.IsOverridden);
    }

    [Fact]
    public void A_stored_value_identical_to_the_configured_one_is_still_an_override()
    {
        // NOT derived by comparing the two values. Resetting this setting changes whether a later
        // deployment can move it, so "overridden" has to come from the presence of a row — a
        // screen that compared values would hide a reset the operator needs to make.
        var store = new FakeSettingsStore();
        store.Seed(UBookItPersistenceComposer.TimeZoneSettingKey, "Europe/London");

        var controller = Controller(store, Config((UBookItPersistenceComposer.TimeZoneSettingKey, "Europe/London")));

        Assert.True(Setting(Read(controller), UBookItPersistenceComposer.TimeZoneSettingKey).IsOverridden);
    }

    [Fact]
    public void Nothing_configured_is_reported_even_when_the_default_is_not_empty()
    {
        // ConfiguredValue now carries the DEFAULT for a key nobody configured (that is the
        // must-fix from round 1), so null no longer distinguishes "configured as nothing" from
        // "never configured". Without a separate flag the screen would tell an operator they were
        // overriding a configured `true` on a site that has never mentioned AutoConfirm.
        var store = new FakeSettingsStore();
        store.Seed(UBookItPersistenceComposer.AutoConfirmSettingKey, "false");

        var autoConfirm = Setting(
            Read(Controller(store, Config())), UBookItPersistenceComposer.AutoConfirmSettingKey);

        Assert.Equal("false", autoConfirm.EffectiveValue);
        Assert.Equal("true", autoConfirm.ConfiguredValue);
        Assert.True(autoConfirm.IsOverridden);
        Assert.False(autoConfirm.IsConfigured);
    }

    [Fact]
    public void A_setting_the_site_HAS_configured_reports_as_configured()
    {
        // The other direction, so the flag cannot be hard-wired false.
        var configured = Setting(
            Read(Controller(new FakeSettingsStore(), Config((UBookItPersistenceComposer.AutoConfirmSettingKey, "false")))),
            UBookItPersistenceComposer.AutoConfirmSettingKey);

        Assert.True(configured.IsConfigured);

        // And a list counts as configured through its children, not a scalar value.
        var recipients = Setting(
            Read(Controller(new FakeSettingsStore(), Config(
                ($"{UBookItPersistenceComposer.InternalRecipientsSettingKey}:0", "ops@example.com")))),
            UBookItPersistenceComposer.InternalRecipientsSettingKey);

        Assert.True(recipients.IsConfigured);
    }

    [Fact]
    public void A_setting_with_nothing_configured_beneath_it_reports_no_configured_value()
    {
        var store = new FakeSettingsStore();
        store.Seed(UBookItPersistenceComposer.PrivacyPolicyUrlSettingKey, "https://example.com/privacy");

        var controller = Controller(store, Config());

        var privacy = Setting(Read(controller), UBookItPersistenceComposer.PrivacyPolicyUrlSettingKey);

        Assert.Equal("https://example.com/privacy", privacy.EffectiveValue);
        Assert.Null(privacy.ConfiguredValue);
        Assert.True(privacy.IsOverridden);
    }

    [Fact]
    public void The_read_covers_the_whole_catalogue_and_reports_its_tiers()
    {
        var model = Read(Controller(new FakeSettingsStore(), Config()));

        Assert.Equal(
            SettingCatalogue.All.Select(s => s.Key).OrderBy(k => k, StringComparer.Ordinal),
            model.Settings.Select(s => s.Key).OrderBy(k => k, StringComparer.Ordinal));

        Assert.Equal(
            "readOnly",
            Setting(model, UBookItPersistenceComposer.RetentionDaysSettingKey).Tier);
        Assert.Equal(
            "editableWithConsequence",
            Setting(model, UBookItPersistenceComposer.TimeZoneSettingKey).Tier);

        // Restart-bound is reported separately from read-only, because they are different facts.
        Assert.True(Setting(model, SettingCatalogue.DeliveryApiEnableReadsKey).RequiresRestart);
        Assert.False(Setting(model, UBookItPersistenceComposer.RetentionDaysSettingKey).RequiresRestart);
    }

    [Fact]
    public void No_theme_setting_is_returned()
    {
        var model = Read(Controller(new FakeSettingsStore(), Config()));

        Assert.DoesNotContain(
            model.Settings, s => s.Key.Contains("Theme", StringComparison.OrdinalIgnoreCase));
    }

    // ---- the effective value is the RESOLVED value ------------------------------------------

    /// <summary>
    /// Every setting, on a site that configures NOTHING: what the screen reports must equal what
    /// the package resolves.
    /// </summary>
    /// <remarks>
    /// <b>This is the case the whole first round of tests missed</b>, because every one of them
    /// supplied an explicit value. The screen read <c>configuration[key]</c> and called it the
    /// effective value, which applies no default — so <c>AutoConfirm</c> rendered UNTICKED on a
    /// site where every booking was auto-confirming, and an operator who wanted approval would
    /// have seen the box already in the state they wanted and changed nothing.
    /// </remarks>
    [Fact]
    public void On_a_site_that_configures_nothing_every_reported_value_is_the_resolved_one()
    {
        var store = new FakeSettingsStore();
        var config = Config();
        var resolved = UBookItPersistenceComposer.ResolveSettings(config);
        var model = Read(Controller(store, config));

        Assert.Equal("true", Setting(model, UBookItPersistenceComposer.AutoConfirmSettingKey).EffectiveValue);
        Assert.True(resolved.AutoConfirm, "The defaults moved; this test is asserting the wrong thing.");

        Assert.Equal(
            resolved.MaxQueryRangeDays.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Setting(model, UBookItPersistenceComposer.MaxQueryRangeDaysSettingKey).EffectiveValue);

        Assert.Equal(
            resolved.TimeZoneId,
            Setting(model, UBookItPersistenceComposer.TimeZoneSettingKey).EffectiveValue);

        // Null stays null where null is the meaning, not a missing default.
        Assert.Null(Setting(model, UBookItPersistenceComposer.RetentionDaysSettingKey).EffectiveValue);
        Assert.Null(Setting(model, UBookItPersistenceComposer.PrivacyPolicyUrlSettingKey).EffectiveValue);
    }

    // ---- the list setting round-trips ---------------------------------------------------------

    /// <summary>
    /// A stored recipient list must actually reach <c>SiteBookingSettings.Notifications</c>.
    /// </summary>
    /// <remarks>
    /// The resolver reads this key as a configuration ARRAY, so the first implementation's single
    /// scalar row was invisible to it: the write returned 200, the screen showed the new addresses,
    /// and the package emailed the configured ones — or nobody. Asserted through the REAL
    /// EffectiveConfiguration + ResolveSettings path, because a test that inspected the stored rows
    /// would have passed against the broken version too.
    /// </remarks>
    [Fact]
    public async Task A_stored_recipient_list_reaches_the_resolved_settings()
    {
        var store = new FakeSettingsStore();
        var config = Config();

        Assert.IsType<OkResult>(await Controller(store, config).PutSetting(
            UBookItPersistenceComposer.InternalRecipientsSettingKey,
            new SettingWriteModel { Value = "one@example.com, two@example.com" }));

        var resolved = UBookItPersistenceComposer.ResolveSettings(
            UBookItPersistenceComposer.EffectiveConfiguration(config, store));

        Assert.Equal(
            ["one@example.com", "two@example.com"],
            resolved.Notifications.InternalRecipients);

        // And the screen agrees with the package rather than with what was typed.
        Assert.Equal(
            "one@example.com, two@example.com",
            Setting(Read(Controller(store, config)), UBookItPersistenceComposer.InternalRecipientsSettingKey)
                .EffectiveValue);
    }

    [Fact]
    public async Task A_stored_recipient_list_replaces_a_longer_configured_one()
    {
        // The overflow case. Composing a shorter array over a longer one does not shorten it, so
        // without blanking the surplus index the removed address would keep receiving mail while
        // the screen reported it gone — the failure this change exists to prevent.
        var store = new FakeSettingsStore();
        var config = Config(
            ($"{UBookItPersistenceComposer.InternalRecipientsSettingKey}:0", "old-a@example.com"),
            ($"{UBookItPersistenceComposer.InternalRecipientsSettingKey}:1", "old-b@example.com"),
            ($"{UBookItPersistenceComposer.InternalRecipientsSettingKey}:2", "old-c@example.com"));

        Assert.IsType<OkResult>(await Controller(store, config).PutSetting(
            UBookItPersistenceComposer.InternalRecipientsSettingKey,
            new SettingWriteModel { Value = "kept@example.com" }));

        var resolved = UBookItPersistenceComposer.ResolveSettings(
            UBookItPersistenceComposer.EffectiveConfiguration(config, store));

        Assert.Equal(["kept@example.com"], resolved.Notifications.InternalRecipients);
    }

    /// <summary>
    /// Save, then save a SHORTER list, with nothing configured underneath.
    /// </summary>
    /// <remarks>
    /// <b>The case every earlier list test missed, because every one of them wrote once.</b>
    /// Writing is per-row and never removed the previous rows, so a shortened list left its
    /// surplus behind: save "a, b, c" then "a, b" and c@ kept receiving mail. The overflow blanks
    /// did not save it — they were measured against the CONFIGURED list, which is empty on exactly
    /// the site that manages its recipients through this screen.
    ///
    /// Asserted through the resolver rather than the rows, because a row-count assertion would
    /// pass against a store that kept the surplus under a key the resolver still reads.
    /// </remarks>
    [Fact]
    public async Task Shortening_a_previously_stored_list_actually_shortens_it()
    {
        var store = new FakeSettingsStore();
        var config = Config();

        await Controller(store, config).PutSetting(
            UBookItPersistenceComposer.InternalRecipientsSettingKey,
            new SettingWriteModel { Value = "a@example.com, b@example.com, c@example.com" });

        Assert.IsType<OkResult>(await Controller(store, config).PutSetting(
            UBookItPersistenceComposer.InternalRecipientsSettingKey,
            new SettingWriteModel { Value = "a@example.com, b@example.com" }));

        var resolved = UBookItPersistenceComposer.ResolveSettings(
            UBookItPersistenceComposer.EffectiveConfiguration(config, store));

        Assert.Equal(["a@example.com", "b@example.com"], resolved.Notifications.InternalRecipients);
        Assert.DoesNotContain("c@example.com", resolved.Notifications.InternalRecipients);
    }

    [Fact]
    public async Task Shortening_a_stored_list_below_a_shorter_configured_one_still_shortens_it()
    {
        // The mixed case: one configured address, a longer stored list, then a shorter one. The
        // sweep handles the stored surplus and the blanks handle the configured overflow, and the
        // two must not cancel each other out.
        var store = new FakeSettingsStore();
        var config = Config(
            ($"{UBookItPersistenceComposer.InternalRecipientsSettingKey}:0", "configured@example.com"));

        await Controller(store, config).PutSetting(
            UBookItPersistenceComposer.InternalRecipientsSettingKey,
            new SettingWriteModel { Value = "a@example.com, b@example.com, c@example.com" });

        Assert.IsType<OkResult>(await Controller(store, config).PutSetting(
            UBookItPersistenceComposer.InternalRecipientsSettingKey,
            new SettingWriteModel { Value = "a@example.com" }));

        var resolved = UBookItPersistenceComposer.ResolveSettings(
            UBookItPersistenceComposer.EffectiveConfiguration(config, store));

        Assert.Equal(["a@example.com"], resolved.Notifications.InternalRecipients);
    }

    [Fact]
    public async Task Growing_a_previously_stored_list_keeps_every_new_address()
    {
        // The opposite direction, so the sweep cannot be "fixed" by simply removing everything and
        // writing nothing back.
        var store = new FakeSettingsStore();
        var config = Config();

        await Controller(store, config).PutSetting(
            UBookItPersistenceComposer.InternalRecipientsSettingKey,
            new SettingWriteModel { Value = "a@example.com" });

        await Controller(store, config).PutSetting(
            UBookItPersistenceComposer.InternalRecipientsSettingKey,
            new SettingWriteModel { Value = "a@example.com, b@example.com, c@example.com" });

        var resolved = UBookItPersistenceComposer.ResolveSettings(
            UBookItPersistenceComposer.EffectiveConfiguration(config, store));

        Assert.Equal(
            ["a@example.com", "b@example.com", "c@example.com"],
            resolved.Notifications.InternalRecipients);
    }

    [Fact]
    public void A_configured_recipient_list_is_reported_as_the_configured_value()
    {
        // Reading configuration[key] returned null for an array node, so a site WITH recipients
        // was told it had configured none.
        var config = Config(
            ($"{UBookItPersistenceComposer.InternalRecipientsSettingKey}:0", "ops@example.com"));

        var recipients = Setting(
            Read(Controller(new FakeSettingsStore(), config)),
            UBookItPersistenceComposer.InternalRecipientsSettingKey);

        Assert.Equal("ops@example.com", recipients.EffectiveValue);
        Assert.Equal("ops@example.com", recipients.ConfiguredValue);
        Assert.False(recipients.IsOverridden);
    }

    [Fact]
    public async Task Resetting_a_recipient_list_removes_every_indexed_row()
    {
        var store = new FakeSettingsStore();
        var config = Config(
            ($"{UBookItPersistenceComposer.InternalRecipientsSettingKey}:0", "ops@example.com"));

        var controller = Controller(store, config);
        await controller.PutSetting(
            UBookItPersistenceComposer.InternalRecipientsSettingKey,
            new SettingWriteModel { Value = "a@example.com, b@example.com" });

        Assert.IsType<OkResult>(
            await Controller(store, config).ResetSetting(
                UBookItPersistenceComposer.InternalRecipientsSettingKey));

        // Not one row left behind: a surviving index would keep the setting partly overridden
        // while the screen reported it reset.
        Assert.Empty(store.GetAll());

        var resolved = UBookItPersistenceComposer.ResolveSettings(
            UBookItPersistenceComposer.EffectiveConfiguration(config, store));

        Assert.Equal(["ops@example.com"], resolved.Notifications.InternalRecipients);
    }

    // ---- reset -------------------------------------------------------------------------------

    [Fact]
    public async Task Reset_removes_the_stored_value_rather_than_storing_the_configured_one()
    {
        // Storing the configured value would freeze it at the moment somebody clicked, and a
        // later change to the site's configuration would stop taking effect — the same silent
        // divergence, pointed the other way.
        var store = new FakeSettingsStore();
        store.Seed(UBookItPersistenceComposer.TimeZoneSettingKey, "America/New_York");

        var config = Config((UBookItPersistenceComposer.TimeZoneSettingKey, "Europe/London"));
        var controller = Controller(store, config);

        Assert.IsType<OkResult>(
            await controller.ResetSetting(UBookItPersistenceComposer.TimeZoneSettingKey));

        Assert.Equal(UBookItPersistenceComposer.TimeZoneSettingKey, Assert.Single(store.Removed));
        Assert.Empty(store.Written);

        // A FRESH controller, because SiteBookingSettings is SCOPED: in production the next
        // request resolves it again over the changed store. Re-reading through the same instance
        // would assert against settings captured before the reset and would say nothing about
        // whether the configured value actually shows through again.
        var afterReset = Setting(
            Read(Controller(store, config)), UBookItPersistenceComposer.TimeZoneSettingKey);

        Assert.Equal("Europe/London", afterReset.EffectiveValue);
        Assert.False(afterReset.IsOverridden);
    }

    [Fact]
    public async Task Resetting_a_setting_that_was_never_overridden_is_not_an_error()
    {
        // The ordinary case for most settings.
        var store = new FakeSettingsStore();
        var controller = Controller(store, Config());

        Assert.IsType<OkResult>(
            await controller.ResetSetting(UBookItPersistenceComposer.AutoConfirmSettingKey));
    }

    [Fact]
    public async Task A_read_only_setting_cannot_be_reset_either()
    {
        var store = new FakeSettingsStore();
        var controller = Controller(store, Config());

        Assert.IsType<BadRequestObjectResult>(
            await controller.ResetSetting(UBookItPersistenceComposer.RetentionDaysSettingKey));
        Assert.Empty(store.Removed);
    }
}
