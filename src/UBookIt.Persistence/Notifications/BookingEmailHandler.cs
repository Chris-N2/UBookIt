using Microsoft.Extensions.Logging;
using UBookIt.Core;
using UBookIt.Core.Bookings;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;

namespace UBookIt.Persistence.Notifications;

/// <summary>
/// Sends what a site has asked to be sent when a booking is placed or cancelled.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two independent conditions, and neither implies the other.</b> A site must have asked for
/// that direction of sending, AND the host must be able to send mail. An Umbraco site configures
/// SMTP for password resets and backoffice invites long before it has any opinion about booking
/// confirmations, so treating a configured mail server as permission to write to that site's
/// customers would mean upgrading the package silently begins contacting people who booked while
/// it sent nothing.
/// </para>
/// <para>
/// <b>Nothing here can harm a booking.</b> This runs after the booking is stored, from
/// <see cref="UmbracoBookingObserver"/>, which swallows and logs whatever escapes — so a mail
/// server's fault never becomes the booker's problem, and the caller is never told about it. By
/// the same token nothing is retried or queued: a message is not a booking.
/// </para>
/// <para>
/// <b>No booker name, address or telephone number is ever logged</b>, including on failure paths.
/// A failure to send is not a reason to write into a log the very details the rest of the package
/// takes care to govern. The booking id identifies the message uniquely and identifies nobody.
/// </para>
/// </remarks>
public sealed class BookingEmailHandler(
    SiteBookingSettings settings,
    IEmailSender emailSender,
    BookingMessageComposer composer,
    IHostingEnvironment hostingEnvironment,
    ILogger<BookingEmailHandler> logger)
    : INotificationAsyncHandler<BookingPlacedNotification>,
      INotificationAsyncHandler<BookingCancelledNotification>
{
    /// <summary>
    /// Identifies the package's booking mail on Umbraco's outgoing-mail notification, so a site
    /// handling that seam can act on this mail specifically rather than on everything the site
    /// sends. <c>Constants.Web.EmailTypes</c> has no booking member; the parameter is a free string.
    /// </summary>
    public const string EmailType = "UBookItBooking";

    public Task HandleAsync(BookingPlacedNotification notification, CancellationToken cancellationToken)
        => SendAsync(notification.Booking, BookingEvent.Placed, cancellationToken);

    public Task HandleAsync(BookingCancelledNotification notification, CancellationToken cancellationToken)
        => SendAsync(notification.Booking, BookingEvent.Cancelled, cancellationToken);

    private async Task SendAsync(
        Booking booking, BookingEvent bookingEvent, CancellationToken cancellationToken)
    {
        var notifications = settings.Notifications;
        var toBooker = notifications.SendBookerEmails;
        var toSite = notifications.HasInternalRecipients;

        // ASKED BEFORE THE HOST IS, and that order is load-bearing. A site that has asked for
        // nothing must not be affected by its mail configuration at all — including by a host
        // whose CanSendRequiredEmail() throws, which Umbraco's own default sender does.
        if (!toBooker && !toSite)
        {
            return;
        }

        // NOT WRAPPED IN A TRY. Umbraco's default sender throws from every member including this
        // one; in a web application Infrastructure replaces it, so this is not expected to throw.
        // If it does, the host cannot tell us whether it can send, and reading that as "no" would
        // convert a misconfiguration into permanent silence with nothing anywhere to find. The
        // observer above catches it, keeps it away from the booker, and logs it loudly.
        //
        // Asked per message rather than cached: EmailSender watches IOptionsMonitor<GlobalSettings>
        // and re-reads on change, so mail configuration can appear or vanish while the site runs.
        if (!emailSender.CanSendRequiredEmail())
        {
            return;
        }

        // THE SITE IS TOLD FIRST, and this is the ordering doing a job rather than an accident.
        // Neither send is wrapped, so whichever runs second is lost if the first throws. The
        // site's own notification is the one a business depends on to know a booking happened at
        // all, and the booker's address is the far likelier of the two to bounce — it was typed
        // by a member of the public minutes ago, while the internal list was typed by whoever
        // administers the site. Ordering costs nothing and removes the worse loss.
        //
        // It does not remove the other one: a failing internal send still costs the booker their
        // confirmation. Making the two genuinely independent needs a catch around each, which is
        // an escape hatch this change declined to add on its own authority.
        if (toSite)
        {
            var message = await composer
                .ForSiteAsync(booking, bookingEvent, BackofficeBookingLink.For(hostingEnvironment), cancellationToken)
                .ConfigureAwait(false);

            await SendAsync(message, [.. notifications.InternalRecipients], booking).ConfigureAwait(false);
        }

        // `Contact is { } contact` establishes the booker is not erased, and it is the only way to
        // reach an address: Booker.Contact is null if and only if the details were erased, so the
        // compiler enumerates every site that would have to be checked rather than leaving it to a
        // reviewer. Reachable rather than theoretical — the retention sweep erases a booker some
        // period after their booking ends, and it can still be cancelled afterwards.
        if (toBooker && booking.Booker.Contact is { } contact)
        {
            var message = await composer
                .ForBookerAsync(booking, bookingEvent, cancellationToken)
                .ConfigureAwait(false);

            await SendAsync(message, [contact.Email], booking).ConfigureAwait(false);
        }
    }

    private async Task SendAsync(BookingMessage message, string[] to, Booking booking)
    {
        // `from: null` on purpose — Umbraco falls back to the site's configured Smtp:From. A
        // sender address of our own would be a second source of truth for a fact the site has
        // already stated once, able to disagree with it and able to name an address the configured
        // mail server will not relay for.
        //
        // The multi-recipient constructor explicitly. The single-recipient overload takes a
        // `string?` and would bind first for a one-element list only by accident of conversion —
        // this way one recipient and five take the same path.
        var email = new EmailMessage(
            from: null,
            to: to,
            cc: null,
            bcc: null,
            replyTo: null,
            subject: message.Subject,
            body: message.Body,
            isBodyHtml: false,
            attachments: null);

        logger.LogDebug(
            "Sending a uBookIt booking message for booking {BookingId} to {RecipientCount} recipient(s).",
            booking.Id,
            to.Length);

        // enableNotification: true raises Umbraco's SendEmailNotification first, so a site that
        // handles it and marks it handled replaces this message entirely. That is the templating
        // seam, already built by the host, for the cost of one argument — and it means a site can
        // change the wording today without waiting for the package to make it configurable.
        // `expires` passed explicitly to bind the CURRENT overload: the three-argument one is
        // [Obsolete] and scheduled for removal in Umbraco 18, and this project builds warnings as
        // errors. Null means the host applies its own expiry, which is the behaviour the obsolete
        // overload had.
        await emailSender
            .SendAsync(email, EmailType, enableNotification: true, expires: null)
            .ConfigureAwait(false);
    }
}
