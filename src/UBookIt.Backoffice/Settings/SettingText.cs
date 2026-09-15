using Microsoft.Extensions.Configuration;
using UBookIt.Core;
using UBookIt.Persistence.Composing;

namespace UBookIt.Backoffice.Settings;

/// <summary>
/// The one place that knows how a setting is spelled as text, in both directions.
/// </summary>
/// <remarks>
/// <para>
/// <b>Effective values come from the RESOLVED settings, never from the configuration string.</b>
/// Reading <c>configuration[key]</c> and calling it the effective value was a defect: it applies
/// no default, so a site that had never written <c>UBookIt:AutoConfirm</c> saw an unticked box
/// while every booking auto-confirmed. The screen's whole purpose is to say what the package is
/// actually doing, so it asks the thing that decides.
/// </para>
/// <para>
/// <b><c>InternalRecipients</c> is a LIST, and that is why this class exists at all.</b> The
/// resolver reads it as a configuration array — <c>GetSection(key).GetChildren()</c> — so a single
/// scalar row can never be seen by it. Storing one was a defect that failed silently in both
/// directions: the write returned success and the screen showed the new addresses, while the
/// package went on emailing the configured ones, or nobody. Writes therefore expand to indexed
/// keys, and reads join them back.
/// </para>
/// </remarks>
internal static class SettingText
{
    /// <summary>The separator the screen uses for list settings.</summary>
    private const string ListSeparator = ", ";

    /// <summary>
    /// What the package is actually using for <paramref name="descriptor"/>, as text, or
    /// <c>null</c> when it is using nothing — which is a meaningful state for several settings
    /// rather than merely an empty one.
    /// </summary>
    /// <param name="descriptor">The setting.</param>
    /// <param name="settings">The resolved settings this text must agree with.</param>
    /// <param name="configuration">
    /// The configuration the settings were resolved from, for the two keys that are not carried on
    /// <see cref="SiteBookingSettings"/> because they are consumed while the application is built.
    /// </param>
    internal static string? Effective(
        SettingDescriptor descriptor, SiteBookingSettings settings, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(configuration);

        return descriptor.Key switch
        {
            UBookItPersistenceComposer.AutoConfirmSettingKey => Bool(settings.AutoConfirm),

            UBookItPersistenceComposer.SendBookerEmailsSettingKey
                => Bool(settings.Notifications.SendBookerEmails),

            // Joined back from the resolved list, so what the screen shows is exactly what the
            // package will email — including the resolver having dropped an unusable address.
            UBookItPersistenceComposer.InternalRecipientsSettingKey
                => settings.Notifications.InternalRecipients.Count == 0
                    ? null
                    : string.Join(ListSeparator, settings.Notifications.InternalRecipients),

            UBookItPersistenceComposer.PrivacyPolicyUrlSettingKey => settings.PrivacyPolicyUrl,

            UBookItPersistenceComposer.TimeZoneSettingKey => settings.TimeZoneId,

            // Null means no retention, and it is NOT the same as a default — the screen must be
            // able to show "we do not erase" distinctly from a period.
            UBookItPersistenceComposer.RetentionDaysSettingKey
                => settings.RetentionDays?.ToString(System.Globalization.CultureInfo.InvariantCulture),

            UBookItPersistenceComposer.MaxQueryRangeDaysSettingKey
                => settings.MaxQueryRangeDays.ToString(System.Globalization.CultureInfo.InvariantCulture),

            // Not on SiteBookingSettings: exposure is decided while the application model is built,
            // so the resolved value IS what configuration says, and absent means off.
            SettingCatalogue.DeliveryApiEnableReadsKey
                or SettingCatalogue.DeliveryApiEnablePlacementKey
                => Bool(bool.TryParse(configuration[descriptor.Key], out var on) && on),

            _ => configuration[descriptor.Key],
        };
    }

    /// <summary>
    /// The rows to store for <paramref name="descriptor"/> when the operator submits
    /// <paramref name="value"/>, given what the site's own configuration already holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A scalar setting is one row. A list setting expands to <c>key:0</c>, <c>key:1</c> … because
    /// that is the only shape the resolver can read.
    /// </para>
    /// <para>
    /// <b>The overflow blanks cover the CONFIGURED list only, and that is deliberate now rather
    /// than by omission.</b> Composing a shorter array over a longer configured one does not
    /// shorten it — index 2 of the configured list would keep showing through beneath a stored list
    /// of two — so every configured index past the end of the new list is stored blank, which the
    /// resolver already treats as a hole rather than a mistyped address.
    /// </para>
    /// <para>
    /// <b>The STORED list is handled by sweeping, not by blanking</b>, because measuring the
    /// overflow against the configured list gets the stored case wrong: a site that manages its
    /// recipients entirely through the screen configures nothing, so the blanks never fire and a
    /// shortened list leaves its surplus rows behind. The caller removes every stored row for the
    /// setting before writing the new set — see <c>SettingsController.PutSetting</c>.
    /// </para>
    /// </remarks>
    internal static IReadOnlyList<KeyValuePair<string, string>> RowsFor(
        SettingDescriptor descriptor, string value, IConfiguration siteConfiguration)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(siteConfiguration);

        if (descriptor.ValueKind != SettingValueKind.EmailList)
        {
            return [new KeyValuePair<string, string>(descriptor.Key, value)];
        }

        var entries = SplitList(value);
        var rows = new List<KeyValuePair<string, string>>(entries.Count);

        for (var i = 0; i < entries.Count; i++)
        {
            rows.Add(new KeyValuePair<string, string>(IndexedKey(descriptor.Key, i), entries[i]));
        }

        var configuredCount = siteConfiguration.GetSection(descriptor.Key).GetChildren().Count();

        for (var i = entries.Count; i < configuredCount; i++)
        {
            rows.Add(new KeyValuePair<string, string>(IndexedKey(descriptor.Key, i), string.Empty));
        }

        return rows;
    }

    /// <summary>
    /// Every stored key that belongs to <paramref name="descriptor"/> — the key itself for a
    /// scalar, and every indexed child for a list. Used to decide whether a setting is overridden
    /// and to remove it on reset.
    /// </summary>
    internal static IReadOnlyList<string> StoredKeysFor(
        SettingDescriptor descriptor, IReadOnlyDictionary<string, string> stored)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(stored);

        if (descriptor.ValueKind != SettingValueKind.EmailList)
        {
            return stored.ContainsKey(descriptor.Key) ? [descriptor.Key] : [];
        }

        var prefix = descriptor.Key + ":";

        // The bare key is included as well as the indexed children. A list setting is only ever
        // WRITTEN as indexed rows, so a bare row should not exist — but one that did (a hand-edited
        // database, or a row written before this shape was settled) would be invisible to reset
        // while still overriding nothing, leaving the setting stuck in a state the screen denies.
        return stored.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal)
                || string.Equals(k, descriptor.Key, StringComparison.Ordinal))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Whether the site's own configuration actually carries this key — a scalar value, or at least
    /// one child for a list.
    /// </summary>
    /// <remarks>
    /// Asked of the configuration directly rather than inferred from the resolved value, because
    /// the resolved value carries defaults: a site that has never written <c>UBookIt:AutoConfirm</c>
    /// still resolves it to <c>true</c>, and the screen must be able to say that nothing is
    /// configured beneath the override.
    /// </remarks>
    internal static bool IsConfigured(SettingDescriptor descriptor, IConfiguration siteConfiguration)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(siteConfiguration);

        return descriptor.ValueKind == SettingValueKind.EmailList
            ? siteConfiguration.GetSection(descriptor.Key).GetChildren().Any()
            : siteConfiguration[descriptor.Key] is not null;
    }

    /// <summary>The entries of a submitted list, trimmed, with empties dropped.</summary>
    internal static IReadOnlyList<string> SplitList(string value)
        => (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    private static string IndexedKey(string key, int index)
        => $"{key}:{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    /// <summary>Lower-case, matching how a configuration file spells a boolean.</summary>
    private static string Bool(bool value) => value ? "true" : "false";
}
