using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UBookIt.Core.Stores;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace UBookIt.Persistence.Composing;

/// <summary>
/// Applies uBookIt's EF Core migrations at startup once the site database is
/// configured. Idempotent: pending migrations are checked first. Records into
/// the package-private history table (see <see cref="UBookItDbContext"/>).
/// </summary>
internal sealed class RunUBookItMigrations(
    UBookItDbContext dbContext,
    IRuntimeState runtimeState,
    IConfiguration configuration,
    ISettingsStore settingsStore,
    ILogger<RunUBookItMigrations> logger) : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (runtimeState.Level is not (RuntimeLevel.Run or RuntimeLevel.Upgrade))
        {
            return;
        }

        // MIGRATIONS FIRST, REPORTS SECOND, and the order is forced rather than tidy.
        //
        // The reports must read the EFFECTIVE configuration — the site's configuration with stored
        // settings composed over it — or a site whose value lives in the store is told nothing
        // about a malformed one, and a site whose time zone is stored is warned every boot that it
        // has no time zone while running the stored one. Reading the store means the settings table
        // has to exist, and on a fresh install it does not until the migration below has run.
        try
        {
            var pending = (await dbContext.Database
                .GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();

            if (pending.Count > 0)
            {
                await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
                logger.LogInformation(
                    "uBookIt applied {Count} database migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "uBookIt database migration failed.");
            throw;
        }

        Report(EffectiveConfigurationOrRaw(), logger);
    }

    /// <summary>
    /// The effective configuration, falling back to the site's own if the store cannot be read.
    /// </summary>
    /// <remarks>
    /// The fallback is not defensive clutter: these reports exist to tell a site about a value it
    /// wrote and is not getting, and refusing to report anything because the store was unreachable
    /// would silence the message on exactly the boot where something is already wrong. A site whose
    /// store is unreadable still hears about its configuration file.
    /// </remarks>
    internal IConfiguration EffectiveConfigurationOrRaw()
    {
        try
        {
            return UBookItPersistenceComposer.EffectiveConfiguration(configuration, settingsStore);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "uBookIt could not read its stored settings while checking configuration at startup. "
                + "The checks below cover the site's configuration file only.");

            return configuration;
        }
    }

    /// <summary>The startup checks, over whichever configuration was resolvable.</summary>
    internal static void Report(IConfiguration configuration, ILogger logger)
    {
        WarnIfTimeZoneNotConfigured(configuration, logger);
        ErrorIfRetentionUnreadable(configuration, logger);
        ErrorIfPrivacyPolicyUrlUnusable(configuration, logger);
        ErrorIfAutoConfirmUnreadable(configuration, logger);
    }

    internal static void WarnIfTimeZoneNotConfigured(IConfiguration configuration, ILogger logger)
    {
        if (!UBookItPersistenceComposer.IsTimeZoneConfigured(configuration))
        {
            logger.LogWarning(
                "No '{SettingKey}' configuration value found; uBookIt is defaulting the site booking time zone to UTC.",
                UBookItPersistenceComposer.TimeZoneSettingKey);
        }
    }

    /// <summary>
    /// Reports a retention period that was written and could not be read. Says nothing when none
    /// was written.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An error rather than a warning</b>, which is louder than the time-zone case above and
    /// meant to be. A missing time zone defaults to UTC and the site keeps working; an unreadable
    /// retention period leaves retention silently off, and a later change publishes the configured
    /// period in a privacy notice — so the site would be telling visitors their data is erased
    /// after a period that nothing is enforcing. That is a fault worth interrupting somebody for.
    /// </para>
    /// <para>
    /// <b>Silent when the setting is absent.</b> Not configuring retention is an ordinary choice
    /// and the package's default; complaining about it every startup would bury the message that
    /// matters among ones that do not.
    /// </para>
    /// </remarks>
    internal static void ErrorIfRetentionUnreadable(IConfiguration configuration, ILogger logger)
    {
        if (UBookItPersistenceComposer.IsRetentionConfigured(configuration)
            && UBookItPersistenceComposer.ResolveRetentionDays(configuration) is null)
        {
            logger.LogError(
                "uBookIt could not read '{SettingKey}' as a positive whole number of days. "
                + "Retention is OFF and no booking's personal data will be erased automatically. "
                + "No default period has been substituted, because erasure cannot be undone.",
                UBookItPersistenceComposer.RetentionDaysSettingKey);
        }
    }

    /// <summary>
    /// Reports an AutoConfirm value that was written and could not be read as a boolean. Says
    /// nothing when none was written.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An error, on the retention precedent</b>: the site wrote something and is not getting
    /// what it wrote. The fallback is auto-confirm ON — today's behaviour — because neither
    /// misreading is safe and only one of them is silent: accidental auto-confirm sends
    /// confirmations somebody can see and correct, while accidental approval parks customers'
    /// bookings in a state nobody is watching for. A site that wrote an unreadable "false" is
    /// therefore confirming bookings it meant to vet, and this line is how it finds out.
    /// </para>
    /// <para>
    /// <b>Silent when the setting is absent</b>, like every other setting here: not configuring
    /// approval is the default and an ordinary choice.
    /// </para>
    /// </remarks>
    internal static void ErrorIfAutoConfirmUnreadable(IConfiguration configuration, ILogger logger)
    {
        if (UBookItPersistenceComposer.IsAutoConfirmConfigured(configuration)
            && !bool.TryParse(configuration[UBookItPersistenceComposer.AutoConfirmSettingKey], out _))
        {
            logger.LogError(
                "uBookIt could not read '{SettingKey}' as a boolean. Auto-confirm is ON — every "
                + "placed booking is confirmed immediately, today's default behaviour. If this "
                + "site meant to require approval, set the value to false.",
                UBookItPersistenceComposer.AutoConfirmSettingKey);
        }
    }

    /// <summary>
    /// Reports a privacy policy link that was written and cannot be used. Says nothing when none
    /// was written.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Same shape and same reasoning as the retention case above: a value that was written and
    /// could not be understood is a fault, while its absence is an ordinary choice. What differs
    /// is where the consequence lands — a site that mistypes this gets a privacy notice with no
    /// link to its own policy, on the page where the package asks people for their contact
    /// details, and nothing on that page says anything is missing.
    /// </para>
    /// <para>
    /// The message names the schemes that are accepted rather than the one that was refused,
    /// because the refusal is an allow-list: telling somebody "javascript: is not allowed" would
    /// invite them to try the next scheme, while telling them what IS allowed answers the
    /// question they actually have.
    /// </para>
    /// </remarks>
    internal static void ErrorIfPrivacyPolicyUrlUnusable(IConfiguration configuration, ILogger logger)
    {
        if (UBookItPersistenceComposer.IsPrivacyPolicyUrlConfigured(configuration)
            && UBookItPersistenceComposer.ResolvePrivacyPolicyUrl(configuration) is null)
        {
            logger.LogError(
                "uBookIt could not use '{SettingKey}' as a link. The booking form's privacy notice "
                + "will render without a link to the site's privacy policy. Accepted values are an "
                + "absolute http or https URL, or a site-relative path beginning with a single '/'.",
                UBookItPersistenceComposer.PrivacyPolicyUrlSettingKey);
        }
    }
}
