using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Services;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Jobs;
using UBookIt.Persistence.Notifications;
using UBookIt.Persistence.Responsibility;
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
    public const string AutoConfirmSettingKey = "UBookIt:AutoConfirm";
    public const string PrivacyPolicyUrlSettingKey = "UBookIt:PrivacyPolicyUrl";
    public const string SendBookerEmailsSettingKey = "UBookIt:Notifications:SendBookerEmails";
    public const string InternalRecipientsSettingKey = "UBookIt:Notifications:InternalRecipients";

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
        // Replaces Core's no-op default, so a booking placed, confirmed, declined or cancelled
        // through any path raises an Umbraco notification a site can handle. Registered here rather than as a
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

        // Registered UNCONDITIONALLY, like the retention job and for the same reason: the check
        // reads the configuration itself, so a site that corrects a mistyped setting is told about
        // it on the next boot rather than waiting on a registration decision made before the
        // correction existed.
        builder.AddNotificationHandler<UmbracoApplicationStartedNotification, UBookItNotificationBootCheck>();

        // Registered UNCONDITIONALLY for the same reason as the retention job and the boot check:
        // the handler reads the settings itself and returns immediately when a site has asked for
        // nothing. Registering it only when sending is configured would make the setting's effect
        // depend on the state of configuration at startup in a second, invisible way.
        builder.Services.AddScoped<BookingMessageComposer>();
        builder.Services.AddScoped<IResponsibilityStore, SqlResponsibilityStore>();
        builder.Services.AddScoped<IUmbracoUserDirectory, UmbracoUserDirectory>();
        builder.Services.AddScoped<IResponsibleRecipientResolver, ResponsibleRecipientResolver>();
        builder.AddNotificationAsyncHandler<BookingPlacedNotification, BookingEmailHandler>();
        builder.AddNotificationAsyncHandler<BookingConfirmedNotification, BookingEmailHandler>();
        builder.AddNotificationAsyncHandler<BookingDeclinedNotification, BookingEmailHandler>();
        builder.AddNotificationAsyncHandler<BookingCancelledNotification, BookingEmailHandler>();

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
        AutoConfirm = ResolveAutoConfirm(configuration),
        PrivacyPolicyUrl = ResolvePrivacyPolicyUrl(configuration),
        Notifications = ResolveNotifications(configuration).Settings,
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
    /// Whether the site wrote anything at all for <c>UBookIt:AutoConfirm</c>, readable or not.
    /// Separate from <see cref="ResolveAutoConfirm"/> for the same reason the retention pair is
    /// split: the startup report complains only about a value that was written and could not be
    /// read, never about an ordinary absence.
    /// </summary>
    internal static bool IsAutoConfirmConfigured(IConfiguration configuration)
        => configuration[AutoConfirmSettingKey] is not null;

    /// <summary>
    /// A missing or unreadable <c>UBookIt:AutoConfirm</c> resolves to <c>true</c> — the
    /// default, and every prior version's behaviour.
    /// </summary>
    /// <remarks>
    /// The fallback direction is chosen for which failure is silent rather than which is safe,
    /// because neither is safe: a site accidentally auto-confirming sends confirmations it can
    /// see arriving and correct, while a site accidentally requiring approval parks customers'
    /// bookings in a state nobody is watching for. So "off" must be explicit and readable, and
    /// a written value that cannot be read is reported at startup — see
    /// <see cref="RunUBookItMigrations.ErrorIfAutoConfirmUnreadable"/> — rather than silently
    /// standing in for either intention.
    /// </remarks>
    internal static bool ResolveAutoConfirm(IConfiguration configuration)
        => !bool.TryParse(configuration[AutoConfirmSettingKey], out var autoConfirm) || autoConfirm;

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
    /// <remarks>
    /// <para>
    /// <b>An upper bound, because the job subtracts this from the current instant.</b>
    /// <c>DateTimeOffset.AddDays</c> throws once the result leaves the representable range, so a
    /// nonsense-but-positive value — a pasted timestamp, a millisecond count — would give a site
    /// an exception every hour instead of a retention policy. Refused here, where the answer is
    /// "off" and an error is logged, rather than thrown hourly out of a background job.
    /// </para>
    /// <para>
    /// A century, and the number is not arbitrary in the direction that matters: any value above
    /// it is either a mistake or an attempt to say "never", and **the setting already has a way
    /// to say never** — leave it out. So nothing expressible is lost, and the failure it removes
    /// is real.
    /// </para>
    /// </remarks>
    internal const int MaxRetentionDays = 36525;

    internal static int? ResolveRetentionDays(IConfiguration configuration)
        => int.TryParse(configuration[RetentionDaysSettingKey], out var days)
            && days > 0
            && days <= MaxRetentionDays
                ? days
                : null;

    /// <summary>
    /// Whether the site wrote anything at all for <c>UBookIt:PrivacyPolicyUrl</c>, usable or not.
    /// </summary>
    /// <remarks>
    /// Presence rather than non-blankness, for the reason
    /// <see cref="IsRetentionConfigured"/> uses it: a blank value looks like an unconfigured
    /// setting and like an environment variable that resolved to nothing, and only one of those
    /// is worth telling somebody about.
    /// </remarks>
    internal static bool IsPrivacyPolicyUrlConfigured(IConfiguration configuration)
        => configuration[PrivacyPolicyUrlSettingKey] is not null;

    /// <summary>
    /// The configured privacy policy link, or <c>null</c> when none is configured or the
    /// configured value cannot be used as a link.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the only setting whose value reaches an <c>href</c> on a public page</b>, so
    /// what it REFUSES matters more than what it accepts. A permissive resolver here is not an
    /// inconvenience, it is a script-injection vector: <c>javascript:</c> in an anchor's href
    /// executes on click, and the value arrives from configuration a site may template from an
    /// environment variable.
    /// </para>
    /// <para>
    /// So the rule is an allow-list, never a block-list. Absolute URIs are accepted only for
    /// <c>http</c> and <c>https</c>; anything else absolute — <c>javascript:</c>, <c>data:</c>,
    /// <c>file:</c>, and every scheme nobody has thought of yet — is refused by not being on the
    /// list. A block-list would have to enumerate the dangerous schemes correctly and forever.
    /// </para>
    /// <para>
    /// Site-relative paths are accepted, because a site's policy page is usually its own, and
    /// they must begin with a single <c>/</c>: <c>//evil.example</c> is protocol-relative and
    /// navigates off-site while looking local.
    /// </para>
    /// </remarks>
    internal static string? ResolvePrivacyPolicyUrl(IConfiguration configuration)
    {
        var configured = configuration[PrivacyPolicyUrlSettingKey];

        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        var value = configured.Trim();

        // CONTROL CHARACTERS AND BACKSLASHES, REFUSED BEFORE ANYTHING ELSE — and this is not
        // belt-and-braces, it closes a measured bypass of the rule below.
        //
        // `Trim()` removes only leading and trailing whitespace, so `/<TAB>/evil.example` reached
        // the relative branch, satisfied "starts with one slash and not two", and rendered as
        // `href="/&#x9;/evil.example"`. The HTML parser decodes that back to a raw tab inside the
        // attribute, and the URL parser then strips every ASCII tab and newline from its input
        // BEFORE parsing (URL Standard, "Remove all ASCII tab or newline") — leaving
        // `//evil.example`, which is exactly the protocol-relative value the rule below exists to
        // refuse. One character defeated it.
        //
        // The backslash goes with them for a related reason. `/\evil.example/x` is refused today
        // on Windows only by accident: .NET parses it as an implicit UNC `file:` URI, so the
        // scheme allow-list catches it. That parsing is Windows-specific, and Umbraco 17 on
        // .NET 10 is routinely hosted on Linux, where the value would fall into the relative
        // branch and be accepted — while a browser resolves `\` as `/` for special schemes and
        // navigates to https://evil.example/x. Refusing it outright removes the question rather
        // than relying on a platform's parser to keep answering it the same way.
        if (value.Any(char.IsControl) || value.Contains('\\', StringComparison.Ordinal))
        {
            return null;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
        {
            return absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps
                ? value
                : null;
        }

        // Site-relative: one leading slash and not two. A protocol-relative "//host/path" is
        // parsed as relative by Uri.TryCreate but navigates off-site, which is exactly the value
        // somebody would use to make an off-site link look like a local one.
        return value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal)
            ? value
            : null;
    }

    /// <summary>
    /// What a resolution of the notification settings produced: the settings themselves, and the
    /// configured recipient addresses that were refused.
    /// </summary>
    /// <remarks>
    /// The refusals travel back to the caller rather than being logged here so that the
    /// resolution stays a pure function of configuration — testable by calling it, with no
    /// logger to stand up and no captured output to read back. Reporting them is the startup
    /// handler's job, which is where a logger already exists.
    /// </remarks>
    internal readonly record struct NotificationResolution(
        BookingNotificationSettings Settings,
        IReadOnlyList<string> RejectedRecipients);

    /// <summary>
    /// The site's notification settings, and any recipient addresses refused while resolving them.
    /// </summary>
    /// <remarks>
    /// <b>Nothing here falls back to a value that enables anything.</b> Absent, blank and
    /// unusable all resolve to off, in the direction established by
    /// <see cref="ResolveRetentionDays"/> — a setting whose failure destroys data fails towards
    /// keeping it, and a setting whose failure writes to a site's customers fails towards
    /// silence. A typo must never start sending mail.
    /// </remarks>
    internal static NotificationResolution ResolveNotifications(IConfiguration configuration)
    {
        // bool.TryParse and not a truthiness test: "yes", "1" and "on" are not values this
        // accepts, and treating them as true would mean guessing at what a site meant while
        // starting to write to its customers on the strength of the guess.
        var sendBookerEmails =
            bool.TryParse(configuration[SendBookerEmailsSettingKey], out var send) && send;

        var kept = new List<string>();
        var rejected = new List<string>();

        foreach (var child in configuration.GetSection(InternalRecipientsSettingKey).GetChildren())
        {
            var configured = child.Value;

            // A blank entry is a hole in the list rather than a mistyped address — an array slot
            // whose environment variable resolved to nothing. There is nothing to report and
            // nobody to report it about, so it is skipped rather than counted as a refusal.
            if (string.IsNullOrWhiteSpace(configured))
            {
                continue;
            }

            var value = configured.Trim();

            if (MailAddress.TryCreate(value, out _))
            {
                kept.Add(value);
            }
            else
            {
                rejected.Add(value);
            }
        }

        return new NotificationResolution(
            new BookingNotificationSettings
            {
                SendBookerEmails = sendBookerEmails,
                InternalRecipients = kept,
            },
            rejected);
    }
}
