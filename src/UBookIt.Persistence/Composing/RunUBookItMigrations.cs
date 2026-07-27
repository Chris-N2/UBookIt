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
public sealed class RunUBookItMigrations(
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

        if (configuration[UBookItPersistenceComposer.TimeZoneSettingKey] is null)
        {
            logger.LogWarning(
                "No '{SettingKey}' configuration value found; uBookIt is defaulting the site booking time zone to UTC.",
                UBookItPersistenceComposer.TimeZoneSettingKey);
        }

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
}
