using Microsoft.Extensions.Logging;
using UBookIt.Core.Bookings;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace UBookIt.Persistence.Notifications;

/// <summary>
/// Raised after a booking has been placed and stored.
/// </summary>
/// <remarks>
/// <para>
/// Subscribe with <c>INotificationAsyncHandler&lt;BookingPlacedNotification&gt;</c> (or the
/// synchronous <c>INotificationHandler</c>) registered from a composer, exactly as for any
/// Umbraco notification.
/// </para>
/// <para>
/// <b>The package sends nothing unless a site has configured it to.</b> Out of the box it sends
/// no email and no message of any kind, to the booker or to anyone else — and configuring the
/// site's mail server does not change that on its own. See
/// <c>UBookIt:Notifications</c> and <see cref="BookingEmailHandler"/>. This notification exists
/// independently of all that, so a site can send whatever it wants to instead, or as well.
/// </para>
/// <para>
/// <b>A handler that throws is a notification nobody receives.</b> The booking is already
/// stored and will not be undone, and the package neither retries nor queues.
/// </para>
/// </remarks>
public sealed class BookingPlacedNotification(Booking booking) : INotification
{
    /// <summary>The booking as it was stored.</summary>
    public Booking Booking { get; } = booking;
}

/// <summary>
/// Raised after a booking has been confirmed and the change stored.
/// </summary>
/// <remarks>
/// <para>
/// <b>Being told at all means the booking has just become confirmed.</b> The status machine
/// permits confirmation only from <c>Requested</c>, so there is no before-and-after to carry.
/// A booking placed as confirmed under auto-confirm raises
/// <see cref="BookingPlacedNotification"/> and not this — auto-confirmation is not an event,
/// it is what placement produced.
/// </para>
/// <para>
/// The same two caveats apply as for placement: what the package tells the booker depends
/// entirely on configuration and is nothing by default, and a handler that throws is a
/// notification nobody receives.
/// </para>
/// </remarks>
public sealed class BookingConfirmedNotification(Booking booking) : INotification
{
    /// <summary>The booking as it now stands, confirmed.</summary>
    public Booking Booking { get; } = booking;
}

/// <summary>
/// Raised after a booking has been declined and the change stored.
/// </summary>
/// <remarks>
/// <para>
/// <b>Being told at all means the booking has just become declined.</b> Decline succeeds only
/// from <c>Requested</c>, on the same reasoning as
/// <see cref="BookingConfirmedNotification"/>. A declined booking remains stored but stops
/// holding its time.
/// </para>
/// <para>
/// The same two caveats apply as for placement: what the package tells the booker depends
/// entirely on configuration and is nothing by default, and a handler that throws is a
/// notification nobody receives.
/// </para>
/// </remarks>
public sealed class BookingDeclinedNotification(Booking booking) : INotification
{
    /// <summary>The booking as it now stands, declined.</summary>
    public Booking Booking { get; } = booking;
}

/// <summary>
/// Raised after a booking has been cancelled and the change stored.
/// </summary>
/// <remarks>
/// <para>
/// <b>Being told at all means the booking has just become cancelled.</b> The status machine
/// permits cancellation only from <c>Requested</c> or <c>Confirmed</c>, so there is no
/// before-and-after to carry: cancelling an already-cancelled booking fails and raises
/// nothing.
/// </para>
/// <para>
/// The same two caveats apply as for placement: what the package tells the booker depends
/// entirely on configuration and is nothing by default, and a handler that throws is a
/// notification nobody receives.
/// </para>
/// </remarks>
public sealed class BookingCancelledNotification(Booking booking) : INotification
{
    /// <summary>The booking as it now stands, cancelled.</summary>
    public Booking Booking { get; } = booking;
}

/// <summary>
/// Turns Core's observations into Umbraco notifications.
/// </summary>
/// <remarks>
/// <para>
/// This lives in <c>UBookIt.Persistence</c> for one reason, and it is a trade-off rather than
/// a natural fit: this is where the composer already lives and it is the one project every
/// consumer loads. Splitting an assembly to house a single adapter would cost more than the
/// misfiling does. Named here so the next reader knows it was chosen.
/// </para>
/// <para>
/// <b>This is where the logging lives.</b> Core catches an observer's exception so it cannot
/// reach the caller, but Core has no logging dependency — it has no dependencies at all — so
/// its defence is necessarily silent. Here there is an <c>ILogger</c>, so a handler that
/// throws leaves a trace rather than vanishing.
/// </para>
/// </remarks>
public sealed class UmbracoBookingObserver(
    IEventAggregator eventAggregator,
    ILogger<UmbracoBookingObserver> logger) : IBookingObserver
{
    public Task BookingPlacedAsync(Booking booking, CancellationToken cancellationToken = default)
        => PublishAsync(new BookingPlacedNotification(booking), booking, "placed");

    public Task BookingConfirmedAsync(Booking booking, CancellationToken cancellationToken = default)
        => PublishAsync(new BookingConfirmedNotification(booking), booking, "confirmed");

    public Task BookingDeclinedAsync(Booking booking, CancellationToken cancellationToken = default)
        => PublishAsync(new BookingDeclinedNotification(booking), booking, "declined");

    public Task BookingCancelledAsync(Booking booking, CancellationToken cancellationToken = default)
        => PublishAsync(new BookingCancelledNotification(booking), booking, "cancelled");

    /// <remarks>
    /// The caller's <c>CancellationToken</c> is deliberately not forwarded. It belongs to the
    /// request that placed the booking or changed its status, and that work is already committed — a
    /// visitor closing their browser must not stop a site being told what happened.
    /// </remarks>
    private async Task PublishAsync(INotification notification, Booking booking, string what)
    {
        try
        {
            await eventAggregator.PublishAsync(notification).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Swallowed here as well as in Core, and logged here because this is the layer
            // that can. The booking is committed; a subscriber's fault must not become the
            // booker's problem.
            //
            // THE BOOKING ID ONLY — a notification failure is not a reason to write a booker's
            // name or email into a log. That governs what THIS package writes, and it is the
            // whole of what it can govern.
            //
            // It does not govern the exception, and since 0.5.0 that distinction is reachable
            // rather than academic: the package can now hand a booker's address to a mail client,
            // and an SMTP rejection commonly echoes the recipient into its own message. Such an
            // exception arrives here and is logged with the error, as it must be — redacting text
            // a third party wrote would as likely destroy the diagnostic that makes a failed send
            // findable. `docs/notifications.md` says so rather than implying a guarantee this
            // cannot keep.
            logger.LogError(
                exception,
                "A handler for the booking-{What} notification threw. Booking {BookingId} is "
                + "unaffected and the notification was not retried.",
                what,
                booking.Id);
        }
    }
}
