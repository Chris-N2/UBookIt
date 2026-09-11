using Microsoft.Extensions.Logging;
using UBookIt.Core.Notifications;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace UBookIt.Web.Emails;

/// <summary>
/// Reports at startup which messages a site has supplied its own content for.
/// </summary>
/// <remarks>
/// <para>
/// <b>This changes the silence, not the fallback.</b> Falling back per message is what this
/// feature requires anyway — supplying one template must not oblige a site to supply six. But
/// without a report, a template saved under a misspelled name is indistinguishable from a
/// deliberate decision to leave that message alone: the package's own wording goes out, nothing
/// is wrong as far as any code can tell, and the author's work simply never appears. Boot is
/// where that mistake is meant to be caught, because boot is when somebody is looking at
/// configuration.
/// </para>
/// <para>
/// <b>It reports the OUTCOME, established the way sending establishes it</b> — by asking the view
/// engine for each message's view, exactly as the renderer will. A check that instead restated
/// what had been registered would confirm its own wiring and notice nothing about the files. That
/// distinction is the one the theme boot check records: the mechanism is not trusted, so the
/// result is measured.
/// </para>
/// <para>
/// <b>It never fails boot.</b> A misnamed email template is a site's own operational problem, and
/// taking a production site down for one would be worse than a log line and a site that still
/// takes bookings — the same reasoning the theme and notification boot checks already record.
/// </para>
/// </remarks>
public sealed class UBookItEmailTemplateBootCheck(
    RazorBookingTemplateRenderer renderer,
    ILogger<UBookItEmailTemplateBootCheck> logger)
    : INotificationHandler<UmbracoApplicationStartedNotification>
{
    public void Handle(UmbracoApplicationStartedNotification notification) => Run();

    /// <summary>
    /// Looks for each message's content and reports. Separated from the notification so it can be
    /// exercised without booting Umbraco.
    /// </summary>
    /// <returns>Every message, and whether a site has supplied content for it.</returns>
    public IReadOnlyList<(BookingMessageKind Kind, bool Supplied)> Run()
    {
        var results = Enum.GetValues<BookingMessageKind>()
            .Select(kind => (Kind: kind, Supplied: renderer.IsSupplied(kind)))
            .ToList();

        var supplied = results.Where(r => r.Supplied).Select(r => r.Kind).ToList();

        if (supplied.Count == 0)
        {
            // Said at Debug, not Information: supplying nothing is the ordinary state and the
            // overwhelmingly common one. A site that has never heard of this feature should not
            // find a line about it in every startup log.
            logger.LogDebug(
                "uBookIt is using its own wording for every message; no email content was found in {Folder}.",
                RazorBookingTemplateRenderer.TemplateFolder);

            return results;
        }

        // Both halves, in one line. Naming only what was found would leave an author who
        // misspelled one file reading a list that looks right until they count it.
        logger.LogInformation(
            "uBookIt is using site-supplied email content for: {Supplied}. Using its own wording for: {NotSupplied}.",
            string.Join(", ", supplied),
            string.Join(", ", results.Where(r => !r.Supplied).Select(r => r.Kind)));

        return results;
    }
}
