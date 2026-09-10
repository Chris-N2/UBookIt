using Umbraco.Cms.Core.Hosting;

namespace UBookIt.Persistence.Notifications;

/// <summary>
/// Where a site's own recipients are sent to see a booking, since the message itself deliberately
/// carries no booker contact details.
/// </summary>
/// <remarks>
/// <b>The point of the link is the control at the other end of it.</b> Whoever follows it arrives
/// in the backoffice, where whether they may see the booker's name and address is decided by the
/// group the sensitive-data handling tests — the same decision that would have been bypassed by
/// putting those details in an email.
/// </remarks>
public static class BackofficeBookingLink
{
    /// <summary>
    /// The bookings screen's path within the backoffice.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Built from the section's <c>pathname</c> ("ubookit") and the bookings section view's
    /// ("bookings"), as declared in the backoffice client's section manifest.
    /// </para>
    /// <para>
    /// <b>There is no per-booking route to link to.</b> Bookings are a section VIEW, not a
    /// workspace with its own entity route, so the deepest available target is the list — which is
    /// why the reference is in the message body: it is what somebody searches the list for.
    /// </para>
    /// </remarks>
    public const string BookingsPath = "umbraco/section/ubookit/view/bookings";

    /// <summary>
    /// The absolute link to the bookings screen, or <c>null</c> when the site's own address is not
    /// established.
    /// </summary>
    /// <remarks>
    /// <b><see cref="IHostingEnvironment.ApplicationMainUrl"/> is declared non-nullable and is
    /// not.</b> It is backed by a null-forgiving field that stays null until Umbraco resolves the
    /// application URL — from configuration, or from an observed request, depending on the site's
    /// <c>ApplicationUrlDetection</c> setting. Umbraco surfaces the unresolved case as a first-class
    /// condition of its own elsewhere, so this is a real state and not a defensive flourish: a site
    /// that has not configured the URL and has not yet served a request has no address to give out.
    /// The compiler will not warn about reading it, which is precisely why it is checked here.
    /// </remarks>
    public static Uri? For(IHostingEnvironment hostingEnvironment)
    {
        var root = hostingEnvironment.ApplicationMainUrl;

        return root is null ? null : new Uri(root, BookingsPath);
    }
}
