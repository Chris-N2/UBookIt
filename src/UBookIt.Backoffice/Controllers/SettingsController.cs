using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using UBookIt.Backoffice.Models;
using UBookIt.Backoffice.Settings;
using UBookIt.Core;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Composing;

namespace UBookIt.Backoffice.Controllers;

/// <summary>
/// Reads and writes the site's own settings. Every action is behind
/// <see cref="Constants.VerbPolicies.Settings"/> — its own verb, never the configuration verb.
/// </summary>
/// <remarks>
/// <para>
/// <b>The tier is enforced here, not only rendered by the client.</b> A client that merely
/// declines to draw an input for the retention period is not a control, because this endpoint is
/// reachable directly. Since the tier boundary exists specifically to keep irreversible erasure
/// and the package's anonymous exposure out of an operator's reach, the server holds it.
/// </para>
/// <para>
/// <b>The read reports the CONFIGURED value beside the effective one.</b> That is what stops a
/// stored override turning the configuration file into a silent lie: once anything is stored, the
/// file no longer describes what runs and a deployment changing it has no visible effect, so the
/// divergence is shown where somebody would look for it.
/// </para>
/// <para>
/// <b>Reset removes the stored value; it does not store the configured one.</b> Storing it would
/// freeze the value at the moment somebody clicked, and a later change to the site's configuration
/// would stop taking effect — which is the same silent divergence pointed the other way.
/// </para>
/// </remarks>
[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "UBookIt.Backoffice")]
public class SettingsController(
    ISettingsStore store,
    IConfiguration configuration,
    SiteBookingSettings effectiveSettings,
    SelfServiceCancellationSettings selfServiceCancellation) : UBookItBackofficeApiControllerBase
{
    /// <summary>Not in Core's <c>FailureCodes</c>: these name contract-level errors this controller owns.</summary>
    internal const string UnknownSetting = "setting-unknown";

    /// <inheritdoc cref="UnknownSetting" />
    internal const string SettingNotEditable = "setting-not-editable";

    /// <inheritdoc cref="UnknownSetting" />
    internal const string SettingValueInvalid = "setting-value-invalid";

    [Authorize(Policy = Constants.VerbPolicies.Settings)]
    [HttpGet("settings")]
    [ProducesResponseType<SettingsResponseModel>(StatusCodes.Status200OK)]
    public IActionResult GetSettings()
    {
        var stored = store.GetAll();

        // Resolved TWICE, and the second one is the point of the screen.
        //
        // `effectiveSettings` is injected and is what the package is actually using — store over
        // configuration, every default and fallback applied. `configured` is the same resolution
        // over the site's configuration ALONE, which is what a stored value is overriding and what
        // reset would return the setting to.
        //
        // Both go through ResolveSettings rather than being read as configuration strings. Reading
        // the string was a defect: it applies no default, so a site that had never written
        // UBookIt:AutoConfirm saw an unticked box while every booking auto-confirmed.
        var configured = UBookItPersistenceComposer.ResolveSettings(configuration);

        var settings = SettingCatalogue.All
            .Select(descriptor =>
            {
                var overriddenKeys = SettingText.StoredKeysFor(descriptor, stored);

                return new SettingResponseModel(
                    descriptor.Key,
                    Camel(descriptor.Tier.ToString()),
                    Camel(descriptor.ValueKind.ToString()),
                    EffectiveValue: SettingText.Effective(descriptor, effectiveSettings, configuration),
                    ConfiguredValue: SettingText.Effective(descriptor, configured, configuration),

                    // From the presence of stored rows, NEVER from comparing the two values. A
                    // stored value identical to the configured one is still an override: resetting
                    // it changes whether a later deployment can move the setting.
                    IsOverridden: overriddenKeys.Count > 0,
                    IsConfigured: SettingText.IsConfigured(descriptor, configuration),
                    descriptor.RequiresRestart,

                    // Resolved against the EFFECTIVE settings, not the configured ones: what the
                    // operator needs to know is whether it works on this site as it stands.
                    UnmetDependency: SettingCatalogue.UnmetDependency(
                        descriptor.Key, effectiveSettings, selfServiceCancellation));
            })
            .ToList();

        return Ok(new SettingsResponseModel(settings));
    }

    [Authorize(Policy = Constants.VerbPolicies.Settings)]
    [HttpPut("settings/{key}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutSetting(
        string key, SettingWriteModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        // An unknown key is 404 rather than 400: the resource named does not exist. This also
        // closes the store to arbitrary keys — nothing outside the catalogue can ever be written,
        // which is what makes "no personal data reaches this table" structural rather than a
        // promise, since the key side is a fixed vocabulary of the package's own setting names.
        if (!SettingCatalogue.TryGet(key, out var descriptor))
        {
            return NotFound(Problem(UnknownSetting, $"'{key}' is not a uBookIt setting."));
        }

        // THE TIER BOUNDARY, held by the server. The client does not render an input for these,
        // but the endpoint is reachable without the client.
        if (!descriptor.IsEditable)
        {
            return BadRequest(Problem(
                SettingNotEditable,
                $"'{key}' is read-only and is changed in the site's configuration, not here."));
        }

        if (!SettingValidation.IsValid(descriptor, model.Value, out var error))
        {
            return BadRequest(Problem(SettingValueInvalid, error!));
        }

        // A list setting expands to indexed rows, because the resolver reads it as a configuration
        // ARRAY and can never see a single scalar row. Storing one returned success while the
        // package went on emailing the configured addresses, or nobody.
        //
        // THE PREVIOUS ROWS ARE SWEPT FIRST, and this is the half that was missed the first time.
        // Writing is per-row, so a shorter list written over a longer STORED one used to leave the
        // surplus rows behind — save "a, b, c" then "a, b" and c@ kept receiving mail. Blanking
        // the overflow was measured against the CONFIGURED list, which is empty on exactly the
        // site that manages its recipients through this screen, so the blanks never fired there.
        //
        // Sweeping makes the stored side exact, and leaves the blanks below responsible only for
        // the configured overflow they were actually designed for.
        if (descriptor.ValueKind == SettingValueKind.EmailList)
        {
            foreach (var staleKey in SettingText.StoredKeysFor(descriptor, store.GetAll()))
            {
                await store.RemoveAsync(staleKey, cancellationToken).ConfigureAwait(false);
            }
        }

        foreach (var (rowKey, rowValue) in SettingText.RowsFor(descriptor, model.Value, configuration))
        {
            await store.SetAsync(rowKey, rowValue, cancellationToken).ConfigureAwait(false);
        }

        return Ok();
    }

    [Authorize(Policy = Constants.VerbPolicies.Settings)]
    [HttpDelete("settings/{key}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetSetting(
        string key, CancellationToken cancellationToken = default)
    {
        if (!SettingCatalogue.TryGet(key, out var descriptor))
        {
            return NotFound(Problem(UnknownSetting, $"'{key}' is not a uBookIt setting."));
        }

        // A read-only setting can never have a stored value, so resetting one is a request that
        // makes no sense rather than a no-op worth absorbing — refusing it keeps the boundary
        // stated in one direction only.
        if (!descriptor.IsEditable)
        {
            return BadRequest(Problem(
                SettingNotEditable,
                $"'{key}' is read-only, so it has no stored value to reset."));
        }

        // EVERY row belonging to the setting — a list setting owns its indexed children, and
        // leaving one behind would keep the setting partly overridden while the screen reported it
        // reset. Removing what is not stored is not an error: reset is the ordinary action on a
        // setting nobody has overridden.
        foreach (var storedKey in SettingText.StoredKeysFor(descriptor, store.GetAll()))
        {
            await store.RemoveAsync(storedKey, cancellationToken).ConfigureAwait(false);
        }

        return Ok();
    }

    private static ProblemDetails Problem(string code, string detail) => new()
    {
        Type = code,
        Title = code,
        Detail = detail,
    };

    /// <summary>
    /// Enum names as camelCase, matching the JSON the rest of the management API emits, so the
    /// client compares against the same spelling everywhere.
    /// </summary>
    private static string Camel(string name)
        => char.ToLowerInvariant(name[0]) + name[1..];
}
