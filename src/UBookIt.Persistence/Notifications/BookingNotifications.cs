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

    public Task BookingCancelledAsync(Booking booking, CancellationToken cancellationToken = default)
        => PublishAsync(new BookingCancelledNotification(booking), booking, "cancelled");

    /// <remarks>
    /// The caller's <c>CancellationToken</c> is deliberately not forwarded. It belongs to the
    /// request that placed or cancelled the booking, and that work is already committed — a
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
            // The booking id only — a notification failure is not a reason to write a
            // booker's name or email into a log.
            logger.LogError(
                exception,
                "A handler for the booking-{What} notification threw. Booking {BookingId} is "
                + "unaffected and the notification was not retried.",
                what,
                booking.Id);
        }
    }
}
