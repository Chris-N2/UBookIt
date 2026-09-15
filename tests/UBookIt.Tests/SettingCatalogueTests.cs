using System.Reflection;
using UBookIt.Backoffice.Settings;
using UBookIt.Persistence.Composing;

namespace UBookIt.Tests;

/// <summary>
/// The settings catalogue: that it covers every setting the package actually reads, that its tiers
/// say what the capability says, and that the two keys it has to spell by hand cannot drift.
/// </summary>
public class SettingCatalogueTests
{
    [Fact]
    public void Every_setting_key_the_persistence_composer_reads_is_in_the_catalogue()
    {
        // DERIVED from the composer's own key constants, never listed here. A hand-written list
        // would be a second description of the package's settings, and the first thing it would do
        // is fall behind: a setting added to the composer without a tier is exactly the gap this
        // exists to close, and a list somebody has to remember to update cannot see it.
        var declared = typeof(UBookItPersistenceComposer)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Where(f => f.Name.EndsWith("SettingKey", StringComparison.Ordinal))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        // Both directions. An empty or mis-derived set would make the assertion below vacuously
        // true, which is how a guard comes to report green while guarding nothing.
        Assert.NotEmpty(declared);
        Assert.Contains(UBookItPersistenceComposer.AutoConfirmSettingKey, declared);
        Assert.Contains(UBookItPersistenceComposer.RetentionDaysSettingKey, declared);

        var catalogued = SettingCatalogue.All.Select(s => s.Key).ToHashSet(StringComparer.Ordinal);

        var missing = declared.Where(k => !catalogued.Contains(k)).ToList();

        Assert.True(
            missing.Count == 0,
            "These settings are read by the package but carry no tier, so the screen would neither "
            + "show nor guard them: " + string.Join(", ", missing));
    }

    [Fact]
    public void The_settings_read_outside_the_persistence_composer_are_accounted_for()
    {
        // The derivation above can only see UBookItPersistenceComposer's constants. Two settings
        // are read elsewhere in the solution, and a guard that silently could not see them was
        // overclaiming by its own name — so they are accounted for explicitly here.
        //
        // UBookIt:Frontend:PreservedQueryParameters is a DELIBERATE omission, recorded as such in
        // the site-settings capability: a developer's setting about their own page's URLs.
        Assert.False(
            SettingCatalogue.TryGet(UBookIt.Web.FrontendSettings.SectionKey + ":PreservedQueryParameters", out _),
            "PreservedQueryParameters is deliberately not presented; if that changes, change the spec too.");

        // The delivery API pair IS catalogued, read-only and restart-bound.
        Assert.True(SettingCatalogue.TryGet(SettingCatalogue.DeliveryApiEnableReadsKey, out _));
        Assert.True(SettingCatalogue.TryGet(SettingCatalogue.DeliveryApiEnablePlacementKey, out _));
    }

    [Fact]
    public void The_catalogue_names_no_setting_the_package_does_not_read()
    {
        // The other direction: a catalogued key nothing reads would render an input that changes
        // nothing, which is worse than absence because it looks like it worked.
        var composerKeys = typeof(UBookItPersistenceComposer)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Where(f => f.Name.EndsWith("SettingKey", StringComparison.Ordinal))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

        // The delivery API's two keys are read in UBookIt.Web, which this assembly's catalogue
        // cannot reference, so they are the known exceptions rather than an oversight.
        string[] readElsewhere =
        [
            SettingCatalogue.DeliveryApiEnableReadsKey,
            SettingCatalogue.DeliveryApiEnablePlacementKey,
        ];

        var unread = SettingCatalogue.All
            .Select(s => s.Key)
            .Where(k => !composerKeys.Contains(k) && !readElsewhere.Contains(k, StringComparer.Ordinal))
            .ToList();

        Assert.True(unread.Count == 0, "Catalogued but read by nothing: " + string.Join(", ", unread));
    }

    [Fact]
    public void The_delivery_api_keys_match_the_section_the_delivery_api_actually_binds()
    {
        // SettingCatalogue spells these by hand because UBookIt.Backoffice does not reference
        // UBookIt.Web and must not start. That hand-spelling is the drift risk, so it is tied back
        // to the real constant HERE, in the test assembly, which does reference both.
        var section = UBookIt.Web.DeliveryApiSettings.SectionKey;

        Assert.Equal(SettingCatalogue.DeliveryApiEnableReadsKey, $"{section}:EnableReads");
        Assert.Equal(SettingCatalogue.DeliveryApiEnablePlacementKey, $"{section}:EnablePlacement");

        // And the property names the binder actually uses, so a rename on the record is caught too.
        Assert.NotNull(typeof(UBookIt.Web.DeliveryApiSettings).GetProperty("EnableReads"));
        Assert.NotNull(typeof(UBookIt.Web.DeliveryApiSettings).GetProperty("EnablePlacement"));
    }

    [Fact]
    public void The_retention_period_is_not_editable()
    {
        // The property the whole tier boundary exists to produce: nothing reachable from the
        // settings screen destroys data. Asserted directly rather than inferred from the list's
        // shape, because this is the claim the capability makes.
        Assert.True(SettingCatalogue.TryGet(
            UBookItPersistenceComposer.RetentionDaysSettingKey, out var retention));
        Assert.Equal(SettingTier.ReadOnly, retention.Tier);
        Assert.False(retention.IsEditable);
    }

    [Fact]
    public void The_time_zone_is_editable_and_carries_a_consequence()
    {
        Assert.True(SettingCatalogue.TryGet(
            UBookItPersistenceComposer.TimeZoneSettingKey, out var timeZone));
        Assert.Equal(SettingTier.EditableWithConsequence, timeZone.Tier);
        Assert.True(timeZone.IsEditable);
    }

    [Fact]
    public void Restart_bound_is_recorded_separately_from_read_only()
    {
        // Two different facts. RetentionDays is read-only but would take effect immediately;
        // the delivery API settings cannot take effect at all without a restart. Collapsing them
        // would make the screen tell a site to restart for a setting where that is not the reason.
        Assert.True(SettingCatalogue.TryGet(
            UBookItPersistenceComposer.RetentionDaysSettingKey, out var retention));
        Assert.False(retention.RequiresRestart);

        Assert.True(SettingCatalogue.TryGet(SettingCatalogue.DeliveryApiEnableReadsKey, out var reads));
        Assert.True(reads.RequiresRestart);

        // No editable setting is restart-bound — an editable one that needed a restart would be a
        // save button that appears to work and does not.
        Assert.DoesNotContain(SettingCatalogue.All, s => s.IsEditable && s.RequiresRestart);
    }

    [Fact]
    public void No_theme_setting_is_presented()
    {
        // Decided during apply: the theme has no configuration key and lives in UBookIt.Web, which
        // this assembly does not reference. It is absent by decision, so its absence is asserted.
        Assert.DoesNotContain(
            SettingCatalogue.All,
            s => s.Key.Contains("Theme", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void An_unknown_key_is_not_a_setting()
    {
        Assert.False(SettingCatalogue.TryGet("UBookIt:NotASetting", out _));

        // Ordinal: a casing difference is a different key, matching the store and the resolver.
        Assert.False(SettingCatalogue.TryGet("ubookit:autoconfirm", out _));
    }
}
