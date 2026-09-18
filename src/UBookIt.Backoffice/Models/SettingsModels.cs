namespace UBookIt.Backoffice.Models;

/// <summary>One setting, as the screen presents it.</summary>
/// <param name="Key">The configuration key, verbatim. Also what a write names.</param>
/// <param name="Tier">
/// <c>editable</c>, <c>editableWithConsequence</c> or <c>readOnly</c>. The client renders from
/// this; the server enforces it regardless.
/// </param>
/// <param name="ValueKind">How the value's text is read — the client picks its control from this.</param>
/// <param name="EffectiveValue">
/// What the package is actually using, as text. <c>null</c> when nothing is set from either
/// source — which is meaningful for several settings ("no retention", "no privacy link") rather
/// than merely empty.
/// </param>
/// <param name="ConfiguredValue">
/// What the site's own configuration says, ignoring anything stored. Presented beside the
/// effective value so that a stored override never makes the configuration file silently wrong.
/// <c>null</c> when the site configures nothing for this key.
/// </param>
/// <param name="IsOverridden">
/// Whether a stored value is in effect. <b>Not derivable from comparing the two values</b>: a
/// stored value identical to the configured one is still an override, and resetting it would
/// change which one a later deployment could move.
/// </param>
/// <param name="IsConfigured">
/// Whether the site's own configuration actually carries this key.
/// <para>
/// <b>Not derivable from <paramref name="ConfiguredValue"/> being null.</b> That value is now the
/// setting RESOLVED over the site configuration alone, so it carries the documented default for a
/// key nobody configured — <c>AutoConfirm</c> reads "true" on a site that has never mentioned it.
/// Useful (it is what resetting would give you) but it means "null" no longer distinguishes
/// "configured as nothing" from "never configured", which the screen needs in order to say whether
/// there is anything to reset TO.
/// </para>
/// </param>
/// <param name="RequiresRestart">
/// Whether a change cannot take effect until the application restarts. Separate from the tier: a
/// setting can be read-only because an operator should not judge it, or because the value is
/// consumed while the application is being built.
/// </param>
/// <param name="UnmetDependency">
/// Why this setting cannot take effect on this site, or <c>null</c> where nothing stops it.
/// <para>
/// <b>A setting reporting itself as on while doing nothing describes a configuration the site does
/// not have</b>, which is the failure this screen exists to avoid. Self-service cancellation is the
/// first setting with a dependency of this shape: its link travels in the booker's message, so
/// where the site sends the booker no message there is no vehicle and no link — and an
/// administrator who enables it deserves to be told that rather than left wondering.
/// </para>
/// <para>
/// A SENTENCE rather than a flag, because the client cannot compose an accurate one: the reason
/// belongs to whoever knows which other setting is involved and why, and a client that assembled it
/// would be a second place for the explanation to drift.
/// </para>
/// </param>
public sealed record SettingResponseModel(
    string Key,
    string Tier,
    string ValueKind,
    string? EffectiveValue,
    string? ConfiguredValue,
    bool IsOverridden,
    bool IsConfigured,
    bool RequiresRestart,
    string? UnmetDependency = null);

/// <summary>Every setting the screen knows about.</summary>
public sealed record SettingsResponseModel(IReadOnlyList<SettingResponseModel> Settings);

/// <summary>A value to store for one setting.</summary>
public sealed record SettingWriteModel
{
    /// <summary>
    /// The value, as text — the same text a configuration source would supply, so that it is read
    /// by exactly the same code.
    /// </summary>
    /// <remarks>
    /// There is deliberately no way to write a blank or null through this. Clearing a setting is
    /// not the same act as returning it to its configured value, and conflating them would give
    /// the store a second spelling of "not overridden". Resetting is its own endpoint.
    /// </remarks>
    public required string Value { get; init; }
}
