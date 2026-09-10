using Microsoft.Extensions.Logging;
using UBookIt.Persistence.Notifications;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Extensions;

namespace UBookIt.TestSite;

/// <summary>
/// A subscriber, in the dev site rather than the package.
/// </summary>
/// <remarks>
/// <para>
/// This exists to prove the notification hook works from the outside — as a consuming site
/// does it, through a composer and <c>INotificationAsyncHandler</c>, with no privileged
/// access. A unit test can show that Core reports; only this can show that a site can hear.
/// </para>
/// <para>
/// It logs and nothing more, and the dev site configures no notification settings, so nothing
/// else is sent either. The package's own emails are a separate, optional path — this stays the
/// worked example of the seam, which is what a site uses when it wants its own message rather
/// than the one the package composes.
/// </para>
/// <para>
/// It is also the worked example in <c>docs/notifications.md</c>, kept as compiling code so
/// the documented shape cannot drift from one that actually builds.
/// </para>
/// </remarks>
public sealed class BookingNotificationComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddNotificationAsyncHandler<BookingPlacedNotification, LogBookingPlaced>();
        builder.AddNotificationAsyncHandler<BookingCancelledNotification, LogBookingCancelled>();
    }
}

public sealed class LogBookingPlaced(ILogger<LogBookingPlaced> logger)
    : INotificationAsyncHandler<BookingPlacedNotification>
{
    public Task HandleAsync(BookingPlacedNotification notification, CancellationToken cancellationToken)
    {
        var booking = notification.Booking;

        logger.LogInformation(
            "UBOOKIT-DEV-NOTIFICATION placed booking {BookingId} starting {StartUtc} for service {Service}",
            booking.Id,
            booking.Interval.StartUtc,
            booking.Service?.DisplayName ?? "(booked directly)");

        return Task.CompletedTask;
    }
}

public sealed class LogBookingCancelled(ILogger<LogBookingCancelled> logger)
    : INotificationAsyncHandler<BookingCancelledNotification>
{
    public Task HandleAsync(BookingCancelledNotification notification, CancellationToken cancellationToken)
    {
        var booking = notification.Booking;

        logger.LogInformation(
            "UBOOKIT-DEV-NOTIFICATION cancelled booking {BookingId} starting {StartUtc}",
            booking.Id,
            booking.Interval.StartUtc);

        return Task.CompletedTask;
    }
}
