using Microsoft.AspNetCore.Mvc;
using UBookIt.Core;
using UBookIt.Core.Bookings;
using UBookIt.Core.Stores;
using UBookIt.Web.Rendering;

namespace UBookIt.Web.Controllers;

/// <summary>
/// Where a booker cancels their own booking, using the link sent to them.
/// </summary>
/// <remarks>
/// <para>
/// <b>GET IS SAFE AND POST ACTS, and the split is not ceremony.</b> Mail security products,
/// corporate scanners and client previewers issue an unattended GET on every URL in a message,
/// before a person has read it. A GET that cancelled would mean bookings cancelled by robots on a
/// schedule the booker did not choose and cannot appeal — so retrieving this page changes nothing,
/// and the secret is marked redeemed by the submission.
/// </para>
/// <para>
/// <b>Every unusable secret gets one answer.</b> Expired, already redeemed, belonging to a booking
/// that can no longer be cancelled, or never issued at all — the distinctions are exactly what an
/// attacker wants, and a page that told them apart would rebuild at the end of the flow the oracle
/// this design removed at the start.
/// </para>
/// <para>
/// A plain controller rather than a <c>SurfaceController</c>: this route has no page behind it. The
/// link has to work on every installation, including one whose visitor-facing front end is headless
/// and whose Umbraco site renders nothing else.
/// </para>
/// </remarks>
[Route(Constants.CancellationPath)]
public sealed class CancellationController(
    ICancellationSecretStore secrets,
    IBookingStore bookings,
    IResourceStore resources,
    IBookingService bookingService,
    SiteBookingSettings settings,
    TimeProvider clock) : Controller
{
    /// <summary>
    /// The confirmation page, or the one refusal every unusable secret shares.
    /// </summary>
    /// <remarks>
    /// <b>Reads, and only reads.</b> Nothing here marks the secret redeemed, and nothing here
    /// changes a booking.
    /// </remarks>
    [HttpGet("{secret}")]
    public async Task<IActionResult> Index(string secret, CancellationToken cancellationToken = default)
    {
        NoReferrer();

        var page = await BuildAsync(secret, cancellationToken).ConfigureAwait(false);

        return page is null ? View("Unusable") : View(page);
    }

    /// <summary>
    /// Redeems the secret and cancels the booking, or answers with the same one refusal.
    /// </summary>
    [HttpPost("{secret}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(string secret, CancellationToken cancellationToken = default)
    {
        NoReferrer();

        if (!CancellationSecret.TryParse(secret, out var parsed))
        {
            return View("Unusable");
        }

        // THE REDEMPTION IS THE COMPARE-AND-SWAP, and it happens before the cancellation it
        // authorises. Two submissions arriving together cannot both win, and a secret spent on an
        // attempt that then fails is spent — which is the safe direction for a credential.
        var bookingId = await secrets
            .TryRedeemAsync(parsed!.Hash, clock.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        if (bookingId is null)
        {
            return View("Unusable");
        }

        var cancelled = await bookingService
            .CancelAsVisitorAsync(bookingId.Value, cancellationToken)
            .ConfigureAwait(false);

        // A REFUSAL HERE JOINS THE SAME SENTENCE. "Already cancelled" and "already started" are
        // facts about the booking that a caller holding a valid secret could otherwise learn, and
        // the capability's answer to every unusable state is one response.
        return cancelled.Succeeded ? View("Cancelled") : View("Unusable");
    }

    /// <summary>
    /// The page's model, or <c>null</c> where the secret cannot be used for any reason.
    /// </summary>
    /// <remarks>
    /// <b>One null for four causes</b>, deliberately: unparseable, unknown, expired, redeemed. A
    /// caller distinguishes none of them, and neither does anything downstream of this method.
    /// </remarks>
    private async Task<CancellationPageModel?> BuildAsync(string secret, CancellationToken cancellationToken)
    {
        if (!CancellationSecret.TryParse(secret, out var parsed))
        {
            return null;
        }

        var record = await secrets.FindAsync(parsed!.Hash, cancellationToken).ConfigureAwait(false);

        if (record is null || record.Redeemed || record.ExpiresUtc <= clock.GetUtcNow())
        {
            return null;
        }

        // THE DOMAIN READ, not the management one. The management summary carries the booker's
        // name and address, and a page that must never show them has no business holding them:
        // what a view cannot be given, a view cannot leak.
        var booking = await bookings.GetBookingAsync(record.BookingId, cancellationToken).ConfigureAwait(false);

        if (booking is null || booking.Status is BookingStatus.Cancelled or BookingStatus.Declined)
        {
            return null;
        }

        // THE BOOKING'S OWN START, not only the stored expiry — and the two can disagree. The
        // expiry is frozen when the secret is issued, so an operator who moves a booking EARLIER
        // leaves a secret whose expiry is now later than the booking it belongs to. Without this,
        // the page would render a confirmation form, the submission would burn the secret, and the
        // visitor would be answered "this link can no longer be used" for a booking they were just
        // shown. Offering a button that cannot work is the failure this package has shipped before.
        if (booking.Interval.StartUtc <= clock.GetUtcNow())
        {
            return null;
        }

        var zone = ResolveZone(booking.Interval.TimeZoneId);

        // Names where they can be established, and nothing where they cannot — the same trade the
        // booker's message makes: what was booked is worth stating, and its absence is not worth
        // withholding the rest for.
        var names = new List<string>();

        foreach (var claim in booking.Claims)
        {
            var resource = await resources.GetAsync(claim.ResourceId, cancellationToken).ConfigureAwait(false);

            if (resource is not null)
            {
                names.Add(resource.DisplayName);
            }
        }

        return new CancellationPageModel
        {
            Reference = booking.Reference.Display,
            LocalStart = TimeZoneInfo.ConvertTime(booking.Interval.StartUtc, zone),
            LocalEnd = TimeZoneInfo.ConvertTime(booking.Interval.EndUtc, zone),
            TimeZoneId = booking.Interval.TimeZoneId,
            ServiceName = booking.Service?.DisplayName,
            ResourceNames = names,
        };
    }

    /// <summary>
    /// The booking's own zone, falling back to the site's and then to UTC.
    /// </summary>
    private TimeZoneInfo ResolveZone(string? zoneId)
    {
        foreach (var candidate in new[] { zoneId, settings.TimeZoneId })
        {
            if (!string.IsNullOrWhiteSpace(candidate)
                && TimeZoneInfo.TryFindSystemTimeZoneById(candidate, out var zone))
            {
                return zone;
            }
        }

        return TimeZoneInfo.Utc;
    }

    /// <summary>
    /// Keeps the secret out of the <c>Referer</c> header.
    /// </summary>
    /// <remarks>
    /// The shipped page loads nothing third-party, so nothing here leaks today — but a theme may
    /// add analytics, and a URL carrying a credential is exactly the one that must not travel in a
    /// header to somebody else's server. Cheap, and it does not depend on what a theme does.
    /// </remarks>
    private void NoReferrer()
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";

        // AND NOT STORED ANYWHERE. A 200 carrying a booking's details, at a URL that carries a
        // credential, must not be left to whatever a CDN, a proxy or a shared browser decides. The
        // change took this trouble for the referrer; this is the same pair's other half.
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
    }
}
