using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Notifications;

namespace UBookIt.Persistence.Composing;

/// <summary>
/// Checks, once at boot, the two ways a site's notification configuration can be wrong while
/// looking right — both of which are otherwise discovered by a message that never arrives.
/// <list type="number">
/// <item>
/// <b>A recipient address that was refused.</b> One mistyped address does not silence the whole
/// list — see <see cref="UBookItPersistenceComposer.ResolveNotifications"/> for why — so without
/// this the site keeps working and one person quietly never hears anything.
/// </item>
/// <item>
/// <b>Sending asked for on a host that cannot send.</b> The two conditions are independent by
/// design, and the failure when they disagree is silence: Umbraco's own sender logs at Debug and
/// returns. Boot is the moment this is actually diagnosable, because it is the moment somebody is
/// looking at configuration.
/// </item>
/// </list>
/// <para>
/// <b>Neither check fails boot</b>, on the same reasoning the theme boot check records: a
/// misconfigured mail server is a site's own operational problem, and taking a production site
/// down for it is worse than a loud log line and a site that still takes bookings. Nothing here
/// changes what is sent — only whether anybody is told why it was not.
/// </para>
/// </summary>
public sealed class UBookItNotificationBootCheck(
    IConfiguration configuration,
    IEmailSender emailSender,
    ILogger<UBookItNotificationBootCheck> logger)
    : INotificationHandler<UmbracoApplicationStartedNotification>
{
    public void Handle(UmbracoApplicationStartedNotification notification) => Run();

    /// <summary>
    /// Runs both checks and logs. Separated from the notification so it can be exercised without
    /// booting Umbraco.
    /// </summary>
    /// <returns>The problems found, so a caller can assert on them.</returns>
    public IReadOnlyList<string> Run()
    {
        var problems = new List<string>();
        var resolution = UBookItPersistenceComposer.ResolveNotifications(configuration);

        // Logged where it is added rather than in a sweep at the end. The first version of this
        // returned early on one path and skipped the sweep, so the refused recipients were
        // collected and never reported — the exact silence this check exists to break.
        void Report(string problem)
        {
            problems.Add(problem);
            logger.LogWarning("uBookIt notification configuration: {Problem}", problem);
        }

        foreach (var rejected in resolution.RejectedRecipients)
        {
            // THE VALUE IS LOGGED, and that is a deliberate distinction rather than an oversight.
            // This is a staff address typed into configuration by whoever administers the site,
            // not a booker's personal data — and an operator cannot fix a typo they are not shown.
            // It does not license logging a BOOKER's address anywhere: see the sending path, where
            // the opposite rule applies and is guarded.
            Report(
                $"The configured uBookIt notification recipient '{rejected}' is not a usable email "
                + "address and will not be written to. The other configured recipients are unaffected.");
        }

        // Asked here and not trusted afterwards. This answers "was the site set up coherently",
        // which is a boot-time question; whether mail can be sent for a PARTICULAR message is
        // asked again at that moment, because a site's mail configuration can change while it runs.
        if (resolution.Settings.SendBookerEmails || resolution.Settings.HasInternalRecipients)
        {
            bool canSend;

            try
            {
                canSend = emailSender.CanSendRequiredEmail();
            }
            catch (Exception exception)
            {
                // NOT read as "cannot send". Umbraco's default sender throws from every member
                // including this one, and a host that cannot answer the question has told us
                // nothing about the answer. Reported as its own problem so it cannot be mistaken
                // for a configured decision — and caught at all only because this runs during
                // boot, where an escaping exception would take the site down over a log line.
                logger.LogError(
                    exception,
                    "uBookIt could not establish whether this site can send email. Booking "
                    + "notifications are configured, so messages may not be sent.");

                Report("The host could not be asked whether it can send email.");
                return problems;
            }

            if (!canSend)
            {
                Report(
                    "uBookIt booking notifications are configured, but this site has no usable "
                    + "mail configuration, so no message will be sent. Configure "
                    + "Umbraco:CMS:Global:Smtp (a Host, or a PickupDirectoryLocation) and a From address.");
            }
        }

        return problems;
    }
}
