using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Stores;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Extensions;

namespace UBookIt.Persistence.Composing;

/// <summary>
/// Registers uBookIt persistence and Core services with the Umbraco container.
/// The DbContext shares the site's Umbraco connection (design D1); uBookIt
/// requires SQL Server 2019+ and refuses any other provider with a clear error.
/// </summary>
public sealed class UBookItPersistenceComposer : IComposer
{
    public const string SettingsSection = "UBookIt";
    public const string TimeZoneSettingKey = "UBookIt:TimeZoneId";
    public const string DefaultTimeZoneId = "UTC";
    public const string MaxQueryRangeDaysSettingKey = "UBookIt:MaxQueryRangeDays";
    public const int DefaultMaxQueryRangeDays = 31;

    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddUmbracoDbContext<UBookItDbContext>(
            (serviceProvider, options, connectionString, providerName) =>
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    // Database not configured yet (e.g. install screen); the
                    // migration handler gates on runtime level and won't run.
                    return;
                }

                if (providerName?.Contains("SqlClient", StringComparison.OrdinalIgnoreCase) != true)
                {
                    throw new InvalidOperationException(
                        $"uBookIt requires SQL Server 2019+ but the site database provider is '{providerName}'. " +
                        "SQLite and other providers are not supported.");
                }

                UBookItDbContext.ConfigureSqlServer(options, connectionString);
            },
            shareUmbracoConnection: true);

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(serviceProvider =>
            ResolveSettings(serviceProvider.GetRequiredService<IConfiguration>()));

        builder.Services.AddScoped<IResourceStore, SqlResourceStore>();
        builder.Services.AddScoped<IResourceManagementStore, SqlResourceManagementStore>();
        builder.Services.AddScoped<IBookingStore, SqlBookingStore>();
        builder.Services.AddScoped<IAvailabilityQueryService, AvailabilityService>();
        builder.Services.AddScoped<IBookingService, BookingService>();

        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, RunUBookItMigrations>();
    }

    /// <summary>
    /// Missing or blank <c>UBookIt:TimeZoneId</c> defaults safely to UTC
    /// (persistence spec, "Missing time zone setting defaults safely").
    /// </summary>
    internal static SiteBookingSettings ResolveSettings(IConfiguration configuration) => new()
    {
        TimeZoneId = IsTimeZoneConfigured(configuration)
            ? configuration[TimeZoneSettingKey]!
            : DefaultTimeZoneId,
        MaxQueryRangeDays = ResolveMaxQueryRangeDays(configuration),
    };

    internal static bool IsTimeZoneConfigured(IConfiguration configuration)
        => !string.IsNullOrWhiteSpace(configuration[TimeZoneSettingKey]);

    /// <summary>
    /// A missing, malformed, or non-positive <c>UBookIt:MaxQueryRangeDays</c>
    /// falls back to the default guardrail rather than throwing (mirrors the
    /// safe time-zone default). The setting is an admin-controlled cap.
    /// </summary>
    internal static int ResolveMaxQueryRangeDays(IConfiguration configuration)
        => int.TryParse(configuration[MaxQueryRangeDaysSettingKey], out var days) && days > 0
            ? days
            : DefaultMaxQueryRangeDays;
}
