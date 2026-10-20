using Microsoft.Extensions.Logging;
using UBookIt.Core.Notifications;
using UBookIt.Core.Stores;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace UBookIt.Web.Emails;

/// <summary>
/// Reports at startup, for each email template set a service names, which messages that set
/// supplies content for.
/// </summary>
/// <remarks>
/// <para>
/// <b>A set that supplies nothing is a warning</b>, naming the folder its views were expected in.
/// A service naming such a set is almost always a misspelt name, or a folder whose case differs
/// from the name — and without a report it is indistinguishable from a set that works, because
/// every message quietly falls back to the views for every service.
/// </para>
/// <para>
/// <b>A separate check rather than more of <see cref="UBookItEmailTemplateBootCheck"/></b>, because
/// that type's interface, constructor and <c>Run()</c> are published, and reporting sets means
/// reading services. Its report is never touched by this one.
/// </para>
/// <para>
/// <b>It never fails boot.</b> Where services cannot be read — the database not yet available, or
/// any other fault — it says that sets could not be checked, and returns. It describes services as
/// they stand at startup: a set typed in the editor later is reported at the next start.
/// </para>
/// </remarks>
public sealed class UBookItEmailTemplateSetBootCheck(
    RazorBookingTemplateRenderer renderer,
    IServiceStore services,
    IRuntimeState runtimeState,
    ILogger<UBookItEmailTemplateSetBootCheck> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    /// <summary>How many services are read per page while collecting the sets they name.</summary>
    private const int PageSize = 200;

    public Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
        => RunAsync(cancellationToken);

    /// <summary>
    /// Collects the sets services name, looks for each message's view in each, and reports.
    /// Separated from the notification so it can be exercised without booting Umbraco.
    /// </summary>
    /// <returns>
    /// Each distinct set, in ordinal order, with every message and whether the set supplies it.
    /// Empty where no service names a set, where the site is not running, and where services could
    /// not be read.
    /// </returns>
    public async Task<IReadOnlyList<(string TemplateSet, IReadOnlyList<(BookingMessageKind Kind, bool Supplied)> Messages)>> RunAsync(
        CancellationToken cancellationToken = default)
    {
        // Before an install or an upgrade has finished, uBookIt's tables may not exist yet, and a
        // read would be a fault this check caused. There is nothing to report until the site runs.
        if (runtimeState.Level != RuntimeLevel.Run)
        {
            return [];
        }

        SortedSet<string> sets;

        try
        {
            sets = await NamedSetsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "uBookIt could not read services at startup, so email template sets could not be checked. "
                + "They are checked again at the next start.");

            return [];
        }

        if (sets.Count == 0)
        {
            // Debug, as the report for the views for every service says supplying nothing: it is
            // the ordinary state, and a site that has never heard of sets should not find a line
            // about them in every startup log.
            logger.LogDebug("uBookIt found no service naming an email template set.");

            return [];
        }

        var results = new List<(string, IReadOnlyList<(BookingMessageKind, bool)>)>();

        foreach (var set in sets)
        {
            var messages = Enum.GetValues<BookingMessageKind>()
                .Select(kind => (Kind: kind, Supplied: renderer.IsSuppliedInSet(set, kind)))
                .ToList();

            results.Add((set, messages));

            var supplied = messages.Where(m => m.Supplied).Select(m => m.Kind).ToList();
            var folder = $"{RazorBookingTemplateRenderer.TemplateFolder}{set}/";

            if (supplied.Count == 0)
            {
                logger.LogWarning(
                    "uBookIt found no email content for the template set {TemplateSet}, which a service names. "
                    + "Its views were expected in {Folder}. Set folders are lower case. Until the folder "
                    + "exists, every message for that service uses the views for every service.",
                    set,
                    folder);

                continue;
            }

            // Both halves, as the report for the views for every service gives them.
            logger.LogInformation(
                "uBookIt template set {TemplateSet} in {Folder} supplies: {Supplied}. Not supplied, so the views "
                + "for every service apply: {NotSupplied}.",
                set,
                folder,
                string.Join(", ", supplied),
                string.Join(", ", messages.Where(m => !m.Supplied).Select(m => m.Kind)));
        }

        return results;
    }

    /// <summary>
    /// Every distinct set any service names, read a page at a time so a large catalogue is never
    /// held whole.
    /// </summary>
    private async Task<SortedSet<string>> NamedSetsAsync(CancellationToken cancellationToken)
    {
        var sets = new SortedSet<string>(StringComparer.Ordinal);

        for (var skip = 0; ; skip += PageSize)
        {
            var page = await services.ListAsync(skip, PageSize, cancellationToken).ConfigureAwait(false);

            foreach (var service in page.Items)
            {
                if (service.TemplateSet is { } set)
                {
                    sets.Add(set);
                }
            }

            if (page.Items.Count == 0 || skip + page.Items.Count >= page.Total)
            {
                return sets;
            }
        }
    }
}
