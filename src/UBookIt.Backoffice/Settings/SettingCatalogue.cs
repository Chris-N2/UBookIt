using System.Diagnostics.CodeAnalysis;
using UBookIt.Persistence.Composing;
using UBookIt.Core;

namespace UBookIt.Backoffice.Settings;

/// <summary>
/// What a setting's tier permits.
/// </summary>
public enum SettingTier
{
    /// <summary>Editable, with no consequence beyond the value itself.</summary>
    Editable,

    /// <summary>
    /// Editable, but changing it reinterprets data the site already holds, so the screen states
    /// what that means before the change is made.
    /// </summary>
    EditableWithConsequence,

    /// <summary>
    /// Displayed, never written through the screen. Either because the cost of getting it wrong
    /// falls outside an operator's competence, or because it cannot take effect without a restart.
    /// </summary>
    ReadOnly,
}

/// <summary>How a setting's value is read and written as text.</summary>
public enum SettingValueKind
{
    /// <summary>Free text.</summary>
    Text,

    /// <summary><c>true</c> or <c>false</c>.</summary>
    Boolean,

    /// <summary>A positive whole number.</summary>
    PositiveInteger,

    /// <summary>An IANA time zone id the host can resolve.</summary>
    TimeZoneId,

    /// <summary>An absolute http or https URL.</summary>
    Url,

    /// <summary>A comma-separated list of email addresses.</summary>
    EmailList,
}

/// <summary>One setting, as the screen and the API both understand it.</summary>
/// <param name="Key">The configuration key, verbatim — also the store's row key.</param>
/// <param name="Tier">What may be done with it.</param>
/// <param name="ValueKind">How its text is read.</param>
/// <param name="RequiresRestart">
/// Whether a change cannot take effect until the application restarts. Distinct from the tier: a
/// setting can be read-only because an operator should not judge it (<c>RetentionDays</c>) or
/// because the value is consumed while the application is being built (<c>DeliveryApi</c>), and the
/// screen says which.
/// </param>
public sealed record SettingDescriptor(
    string Key,
    SettingTier Tier,
    SettingValueKind ValueKind,
    bool RequiresRestart = false)
{
    /// <summary>Whether the screen may write this setting.</summary>
    public bool IsEditable => Tier is SettingTier.Editable or SettingTier.EditableWithConsequence;
}

/// <summary>
/// The package's settings, their tiers and their types — declared exactly once, and read by both
/// the management API and the backoffice client.
/// </summary>
/// <remarks>
/// <para>
/// <b>The tier boundary divides settings by who is competent to judge them</b>: policy and
/// communication belong to whoever runs the bookings; cost, safety and data lifetime belong to
/// whoever deploys the site. Stating the principle rather than only the list is deliberate — it is
/// what tells a future setting where it belongs without re-litigating the argument.
/// </para>
/// <para>
/// <b>This is one declaration, not two.</b> A client-side copy of which settings are editable would
/// eventually disagree with the server's, and disagreement here means either a dead input or an
/// unguarded one. The client reads this; the server also enforces it, because a boundary the client
/// alone holds is reachable by anyone who can call the endpoint.
/// </para>
/// <para>
/// <b>The active theme is deliberately absent.</b> It has no configuration key — it is a code call
/// in the site's own composer — and it lives in <c>UBookIt.Web</c>, which this assembly does not
/// reference. Presenting it would couple the management assembly to the rendering one in order to
/// show a developer a string they wrote themselves.
/// </para>
/// </remarks>
public static class SettingCatalogue
{
    /// <summary>The delivery API exposure keys, which live in <c>UBookIt.Web</c>'s settings type.</summary>
    /// <remarks>
    /// Spelled here rather than referenced from <c>DeliveryApiSettings.SectionKey</c>, because this
    /// assembly does not reference <c>UBookIt.Web</c> and must not start — the management API has no
    /// business depending on the rendering assembly. A guard ties these strings to the delivery
    /// API's own constant so the two cannot drift.
    /// </remarks>
    public const string DeliveryApiEnableReadsKey = "UBookIt:DeliveryApi:EnableReads";

    /// <inheritdoc cref="DeliveryApiEnableReadsKey" />
    public const string DeliveryApiEnablePlacementKey = "UBookIt:DeliveryApi:EnablePlacement";

    /// <summary>
    /// Whether a booker may cancel their own booking from the link in their message.
    /// </summary>
    /// <remarks>
    /// Spelled here rather than referenced from <c>SelfServiceCancellationSettings</c> for the
    /// same reason the delivery API keys are: the backoffice assembly does not reference the Web
    /// one, and a guard holds the two spellings equal so they cannot drift.
    /// </remarks>
    public const string SelfServiceCancellationEnabledKey = "UBookIt:SelfServiceCancellation:Enabled";

    /// <summary>
    /// Every setting the screen knows about, in the order it presents them: what an operator
    /// decides first, then what they are shown but do not decide.
    /// </summary>
    public static IReadOnlyList<SettingDescriptor> All { get; } =
    [
        // --- The operator's: policy and communication. -------------------------------------
        new(UBookItPersistenceComposer.AutoConfirmSettingKey,
            SettingTier.Editable, SettingValueKind.Boolean),

        new(UBookItPersistenceComposer.SendBookerEmailsSettingKey,
            SettingTier.Editable, SettingValueKind.Boolean),

        new(UBookItPersistenceComposer.InternalRecipientsSettingKey,
            SettingTier.Editable, SettingValueKind.EmailList),

        new(UBookItPersistenceComposer.PrivacyPolicyUrlSettingKey,
            SettingTier.Editable, SettingValueKind.Url),

        // --- Editable, but it reinterprets what the site already holds. ---------------------
        // Availability rules are wall-clock in this zone and carry no zone of their own, so
        // changing it changes what every existing rule MEANS. Nothing is rewritten and it is
        // reversible, which is why it is editable at all rather than read-only.
        new(UBookItPersistenceComposer.TimeZoneSettingKey,
            SettingTier.EditableWithConsequence, SettingValueKind.TimeZoneId),

        // --- The developer's: data lifetime, cost, exposure. --------------------------------
        // Read-only because erasure is IRREVERSIBLE and its effect is deferred to the next
        // sweep rather than visible at save time. This is what makes "nothing reachable from
        // this screen destroys data" a property of the catalogue rather than of a dialog.
        new(UBookItPersistenceComposer.RetentionDaysSettingKey,
            SettingTier.ReadOnly, SettingValueKind.PositiveInteger),

        // Read-only because it is a cost guardrail: getting it wrong does not look broken, it
        // just makes the site slower, and an operator has no basis on which to judge it.
        new(UBookItPersistenceComposer.MaxQueryRangeDaysSettingKey,
            SettingTier.ReadOnly, SettingValueKind.PositiveInteger),

        // Read-only AND restart-bound, which are two separate facts about it. Exposure is
        // decided while the MVC application model is built — disabled directions have their
        // selectors removed — so a runtime toggle would not merely be unwise, it would not work.
        new(DeliveryApiEnableReadsKey,
            SettingTier.ReadOnly, SettingValueKind.Boolean, RequiresRestart: true),

        new(DeliveryApiEnablePlacementKey,
            SettingTier.ReadOnly, SettingValueKind.Boolean, RequiresRestart: true),

        // Read-only for the SAME reason as the two above, and it is the exposure reason rather
        // than a judgement about operators: this opens a route that cancels a site's bookings for
        // a caller the package cannot identify beyond a secret. Restart-bound for the same reason
        // too — the route is removed from the application model while it is being built, so a
        // runtime toggle would not merely be unwise, it would not work.
        new(SelfServiceCancellationEnabledKey,
            SettingTier.ReadOnly, SettingValueKind.Boolean, RequiresRestart: true),
    ];

    /// <summary>
    /// Why a setting cannot take effect given the rest of the configuration, or <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <b>Stated here rather than computed in the client</b>, because the reason involves another
    /// setting and a client that assembled the sentence would be a second place for the
    /// explanation to drift from the behaviour it describes.
    /// <para>
    /// Only self-service cancellation has one today. It is deliberately not generalised into a
    /// dependency graph: one case is not a pattern, and inventing the abstraction now would guess
    /// at the shape of cases that do not exist.
    /// </para>
    /// </remarks>
    public static string? UnmetDependency(string key, SiteBookingSettings effective)
    {
        if (!string.Equals(key, SelfServiceCancellationEnabledKey, StringComparison.Ordinal))
        {
            return null;
        }

        return effective.Notifications.SendBookerEmails
            ? null
            : "This has no effect while booker emails are off: the cancellation link travels in "
              + "the message sent to the booker, so where no message is sent there is no link.";
    }

    /// <summary>
    /// The descriptor for <paramref name="key"/>, or <c>false</c> when the key is not a uBookIt
    /// setting at all.
    /// </summary>
    /// <remarks>
    /// Ordinal, matching the store and the configuration keys everywhere else: these are the
    /// package's own constants, so a casing difference is a bug to surface rather than absorb.
    /// </remarks>
    public static bool TryGet(string key, [NotNullWhen(true)] out SettingDescriptor? descriptor)
    {
        descriptor = All.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal));
        return descriptor is not null;
    }
}
