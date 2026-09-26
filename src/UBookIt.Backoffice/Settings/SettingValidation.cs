using Microsoft.Extensions.Configuration;
using UBookIt.Persistence.Composing;
using UBookIt.Persistence.Entities;

namespace UBookIt.Backoffice.Settings;

/// <summary>
/// Whether a submitted value may be stored for a setting, and why not when it may not.
/// </summary>
/// <remarks>
/// <para>
/// <b>This does not replace the resolution fallbacks, and the two are deliberately separate
/// checks.</b> Validation stops the screen creating a value that would silently fall back — an
/// operator who types a bad time zone should be told, not quietly given UTC. The fallbacks still
/// cover a row written by a migration, edited directly in the database, or valid when it was stored
/// and not any more.
/// </para>
/// <para>
/// So the two are allowed to disagree in one direction only: everything this accepts must be
/// readable by the resolver, but the resolver must keep coping with values this would have refused.
/// Validation is the gate; the fallback is the floor.
/// </para>
/// </remarks>
public static class SettingValidation
{
    /// <summary>
    /// A configuration with nothing in it, for asking <see cref="SettingText.RowsFor"/> which rows a
    /// value would be stored as. The only rows a site's configuration adds are blank overflow rows,
    /// and a blank row cannot be too long, so leaving them out changes nothing this is used for.
    /// </summary>
    private static readonly IConfiguration NoConfiguration = new ConfigurationBuilder().Build();

    /// <summary>
    /// Whether <paramref name="value"/> may be stored for <paramref name="descriptor"/>.
    /// </summary>
    /// <param name="descriptor">The setting being written.</param>
    /// <param name="value">The submitted text.</param>
    /// <param name="error">Why the value was refused, when it was.</param>
    public static bool IsValid(SettingDescriptor descriptor, string value, out string? error)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        error = null;

        // A blank is never a way of spelling "not overridden" — removing the row is. Accepting one
        // would give the store a second representation of absence, and the two would resolve
        // differently: the row would fall back to the setting's default while the configured value
        // the operator meant to restore stayed hidden beneath it.
        if (string.IsNullOrWhiteSpace(value))
        {
            error = "A value is required. To return this setting to its configured value, reset it "
                + "rather than clearing it.";
            return false;
        }

        // What the store can hold, measured on the rows this value would BECOME rather than on the
        // submitted text. A recipient list is stored one row per address, so measuring the whole
        // string would refuse a long list the store holds perfectly well. The rows come from the
        // same call the controller stores with, so the two cannot disagree about the shape.
        if (SettingText.RowsFor(descriptor, value, NoConfiguration)
            .Any(row => row.Value.Length > SettingRow.MaxValueLength))
        {
            error = descriptor.ValueKind == SettingValueKind.EmailList
                ? $"Each address must be no longer than {SettingRow.MaxValueLength} characters."
                : $"Must be no longer than {SettingRow.MaxValueLength} characters.";
            return false;
        }

        switch (descriptor.ValueKind)
        {
            case SettingValueKind.Boolean:
                if (!bool.TryParse(value, out _))
                {
                    error = "Must be true or false.";
                }

                break;

            case SettingValueKind.PositiveInteger:
                if (!int.TryParse(value, out var number) || number <= 0)
                {
                    error = "Must be a whole number greater than zero.";
                }

                break;

            case SettingValueKind.TimeZoneId:
                // Resolved against the host, because that is what the resolver will do with it.
                // Accepting an id this machine cannot find would store a value guaranteed to fall
                // back to UTC — the exact silent failure validation exists to prevent.
                try
                {
                    TimeZoneInfo.FindSystemTimeZoneById(value);
                }
                catch (Exception exception)
                    when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
                {
                    error = "Not a time zone this server recognises. Use an IANA id such as "
                        + "Europe/London.";
                }

                break;

            case SettingValueKind.Url:
                // The resolver's own rule, CALLED rather than restated: the screen must accept a
                // value exactly when the site would use it. This case used to say it "mirrors the
                // resolver" while applying a different rule — absolute http(s) only — and so
                // refused every site-relative link the resolver and the documentation accept.
                // A mirror is a second copy, and a second copy drifts; a call cannot.
                if (!UBookItPersistenceComposer.TryGetUsablePolicyLink(value, out _))
                {
                    error = "Must be an http or https address, or a site-relative path beginning "
                        + "with a single '/'.";
                }

                break;

            case SettingValueKind.EmailList:
                var addresses = value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (addresses.Length == 0)
                {
                    error = "Must be one or more email addresses, separated by commas.";
                }
                else
                {
                    var bad = addresses.FirstOrDefault(a => !IsEmailAddress(a));

                    if (bad is not null)
                    {
                        error = $"'{bad}' is not an email address.";
                    }
                }

                break;

            case SettingValueKind.Text:
            default:
                break;
        }

        return error is null;
    }

    /// <summary>
    /// Deliberately shallow: one <c>@</c>, something either side, no whitespace. Anything stricter
    /// is a well-known way to refuse addresses that are perfectly valid, and the authority on
    /// whether an address works is whether mail reaches it.
    /// </summary>
    private static bool IsEmailAddress(string candidate)
    {
        if (candidate.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var at = candidate.IndexOf('@', StringComparison.Ordinal);

        return at > 0
            && at == candidate.LastIndexOf('@')
            && at < candidate.Length - 1;
    }
}
