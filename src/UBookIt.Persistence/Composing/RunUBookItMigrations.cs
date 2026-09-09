using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    ILogger<RunUBookItMigrations> logger) : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (runtimeState.Level is not (RuntimeLevel.Run or RuntimeLevel.Upgrade))
        {
            return;
        }

        WarnIfTimeZoneNotConfigured(configuration, logger);
        ErrorIfRetentionUnreadable(configuration, logger);

        try
        {
            var pending = (await dbContext.Database
                .GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();

            if (pending.Count == 0)
            {
                return;
            }

            await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "uBookIt applied {Count} database migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "uBookIt database migration failed.");
            throw;
        }
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
}
