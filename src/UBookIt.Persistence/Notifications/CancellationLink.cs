using Umbraco.Cms.Core.Hosting;

namespace UBookIt.Persistence.Notifications;

/// <summary>
/// Where a booker is sent to cancel their own booking.
/// </summary>
/// <remarks>
/// <b>The secret is in the path, and the path is the whole credential.</b> Whoever follows this
/// link is, by holding it, the person the message was sent to — which is the entire authentication
/// story for self-service cancellation, and why the value must never be reconstructible from
/// anything a stranger can see.
/// <para>
/// Built on the same terms as <see cref="BackofficeBookingLink"/>, and for the same reason: a
/// message is composed without a request, so the site's own address is the only thing that can say
/// where the link points.
/// </para>
/// </remarks>
public static class CancellationLink
{
    /// <summary>The route the package serves for redeeming a cancellation secret.</summary>
    /// <remarks>
    /// A package route rather than a page a site must create: the link has to work on every
    /// installation, including one whose visitor-facing front end is headless and whose Umbraco
    /// site renders nothing else.
    /// </remarks>
    public const string PathBase = "umbraco/ubookit/cancel";

    /// <summary>
    /// The absolute link for a secret, or <c>null</c> when the site's own address is not
    /// established.
    /// </summary>
    /// <remarks>
    /// <b><see cref="IHostingEnvironment.ApplicationMainUrl"/> is declared non-nullable and is
    /// not</b> — it stays null until Umbraco resolves the application URL, from configuration or
    /// from an observed request. A site that has configured neither and has not yet served a
    /// request has no address to give out, and the honest answer is no link rather than a relative
    /// one nobody can follow from an inbox.
    /// </remarks>
    public static Uri? For(IHostingEnvironment hostingEnvironment, string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        var root = hostingEnvironment.ApplicationMainUrl;

        if (root is null)
        {
            return null;
        }

        // THE TRAILING SLASH MATTERS AND IS NOT COSMETIC. Uri resolution treats the last segment of
        // a base without one as a file and replaces it, so a site in a virtual directory would get
        // a link to a route that is not there — exactly the case nobody tests locally.
        var absolute = root.AbsoluteUri;
        var baseUri = absolute.EndsWith('/') ? root : new Uri(absolute + "/");

        // The secret's alphabet is URL-safe by construction, so nothing here needs escaping — but
        // it is escaped anyway, because "the caller always passes a safe value" is a property of
        // today's callers rather than of this method.
        return new Uri(baseUri, $"{PathBase}/{Uri.EscapeDataString(secret)}");
    }
}
