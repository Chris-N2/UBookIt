using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Services;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Jobs;
using UBookIt.Persistence.Notifications;
using UBookIt.Persistence.Stores;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
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
    public const string RetentionDaysSettingKey = "UBookIt:RetentionDays";

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
        builder.Services.AddScoped<IServiceStore, SqlServiceStore>();
        builder.Services.AddScoped<IServiceManagementStore, SqlServiceManagementStore>();
        builder.Services.AddScoped<IBookingStore, SqlBookingStore>();
        builder.Services.AddScoped<IBookingManagementStore, SqlBookingManagementStore>();
        builder.Services.AddScoped<IAvailabilityQueryService, AvailabilityService>();
        // Replaces Core's no-op default, so a booking placed or cancelled through any path
        // raises an Umbraco notification a site can handle. Registered here rather than as a
        // decorator around IBookingService: the observation is a domain fact, and Core
        // reports it whether or not Umbraco composed the application.
        builder.Services.AddScoped<IBookingObserver, UmbracoBookingObserver>();
        builder.Services.AddScoped<IBookingService, BookingService>();
        builder.Services.AddScoped<IServiceBookingService, ServiceBookingService>();

        // Registered UNCONDITIONALLY, whether or not retention is configured — the job reads the
        // setting itself and returns immediately when there is none. Registering it only when a
        // period is configured would make the setting's effect depend on the state of
        // configuration at startup in a second, invisible way: a site that corrected a mistyped
        // value would still have no job to run, and would be waiting on a restart for a different
        // reason than the one it thought.
        //
        // AddSingleton rather than an extension method: Umbraco has no AddDistributedBackgroundJob
        // helper and registers its own the same way.
        builder.Services.AddSingleton<IDistributedBackgroundJob, BookerRetentionJob>();

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
        RetentionDays = ResolveRetentionDays(configuration),
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

    /// <summary>
    /// Whether the site wrote anything at all for <c>UBookIt:RetentionDays</c>, readable or not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Separate from <see cref="ResolveRetentionDays"/> so that "nobody configured retention" and
    /// "somebody configured retention and it could not be read" stay distinguishable. They resolve
    /// to the same setting — off — but only one of them is a fault, and reporting the ordinary
    /// choice as a fault would train a site owner to ignore the message that matters.
    /// </para>
    /// <para>
    /// <b>Presence, not non-blankness</b> — which is deliberately unlike
    /// <see cref="IsTimeZoneConfigured"/>. A blank value is ambiguous: it looks like an
    /// unconfigured setting and it looks like an environment variable that resolved to nothing on
    /// a site that meant to set 90. Treating it as absent would answer that ambiguity with
    /// silence, and silence is the wrong answer for a setting whose failure mode is a site
    /// believing its data is being erased when nothing is erasing it. The blank still resolves to
    /// off; it just does not do so quietly.
    /// </para>
    /// </remarks>
    internal static bool IsRetentionConfigured(IConfiguration configuration)
        => configuration[RetentionDaysSettingKey] is not null;

    /// <summary>
    /// The configured retention period in days, or <c>null</c> for no retention.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Deliberately NOT shaped like <see cref="ResolveMaxQueryRangeDays"/>.</b> That method
    /// substitutes a working default for a value it cannot read, because the cost of being wrong
    /// is a rejected query. Here the cost of being wrong is erasing personal data irreversibly on
    /// a period nobody wrote, so every unreadable form resolves to <c>null</c> and no default is
    /// ever substituted. Being wrong in this direction keeps data longer than intended, which the
    /// site can fix; being wrong in the other direction cannot be undone by anyone.
    /// </para>
    /// <para>
    /// <b>Zero is refused rather than read as "erase as soon as a booking ends".</b> That is a
    /// coherent policy, but <c>RetentionDays: 0</c> is far likelier to be somebody writing "off"
    /// than somebody asking for immediate erasure — and only one of those two misreadings can be
    /// recovered from. A site wanting the aggressive policy writes <c>1</c>.
    /// </para>
    /// </remarks>
    internal static int? ResolveRetentionDays(IConfiguration configuration)
        => int.TryParse(configuration[RetentionDaysSettingKey], out var days) && days > 0
            ? days
            : null;
}
