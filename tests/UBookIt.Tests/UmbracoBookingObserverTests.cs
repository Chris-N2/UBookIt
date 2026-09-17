using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using UBookIt.Core.Bookings;
using UBookIt.Persistence.Notifications;
using UBookIt.Tests.Support;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace UBookIt.Tests;

/// <summary>
/// The adapter that turns Core's observations into Umbraco notifications: each observation
/// publishes exactly its own notification type, and a subscriber's fault is swallowed and
/// logged with the booking id and nothing else about the booker.
/// </summary>
/// <remarks>
/// Whether the observations themselves fire once, only on success and only after storage is
/// <c>ApprovalDeclineTests</c>' and <c>BookingObservationTests</c>' ground; this file owns the
/// mapping, which they cannot see — a swapped notification type would leave both green while
/// every subscriber heard the wrong event.
/// </remarks>
public class UmbracoBookingObserverTests
{
    private sealed class RecordingAggregator : IEventAggregator
    {
        public List<INotification> Published { get; } = [];

        public bool Throw { get; init; }

        public Task PublishAsync<TNotification>(
            TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            if (Throw)
            {
                throw new InvalidOperationException("A subscriber failed.");
            }

            Published.Add(notification);
            return Task.CompletedTask;
        }

        public void Publish<TNotification>(TNotification notification)
            where TNotification : INotification => throw new NotSupportedException();

        public void Publish<TNotification, TNotificationHandler>(IEnumerable<TNotification> notifications)
            where TNotification : INotification
            where TNotificationHandler : INotificationHandler => throw new NotSupportedException();

        public Task PublishAsync<TNotification, TNotificationHandler>(
            IEnumerable<TNotification> notifications, CancellationToken cancellationToken = default)
            where TNotification : INotification
            where TNotificationHandler : INotificationHandler => throw new NotSupportedException();

        public bool PublishCancelable<TCancelableNotification>(TCancelableNotification notification)
            where TCancelableNotification : ICancelableNotification => throw new NotSupportedException();

        public Task<bool> PublishCancelableAsync<TCancelableNotification>(TCancelableNotification notification)
            where TCancelableNotification : ICancelableNotification => throw new NotSupportedException();
    }

    private sealed class CapturingLogger : ILogger<UmbracoBookingObserver>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }

    private static Booking Booking()
        => Core.Bookings.Booking.Rehydrate(
            Guid.NewGuid(),
            References.Any(),
            BookingInterval.Create(
                TestData.Utc(TestData.BaseDate, "09:00"),
                TestData.Utc(TestData.BaseDate, "10:00"),
                TestData.LondonZoneId).Value,
            Booker.Create(null, "Ada Lovelace", "ada@example.com", "07700 900123").Value,
            [new ResourceClaim(Guid.NewGuid())],
            BookingStatus.Requested,
            TestData.Now,
            service: null).Value;

    [Fact]
    public async Task Each_observation_publishes_its_own_notification_type()
    {
        var aggregator = new RecordingAggregator();
        var observer = new UmbracoBookingObserver(aggregator, new CapturingLogger());
        var booking = Booking();

        var previous = BookingInterval.Create(
            TestData.Utc(TestData.BaseDate, "14:00"), TestData.Utc(TestData.BaseDate, "15:00"), TestData.LondonZoneId).Value;

        await observer.BookingPlacedAsync(booking);
        await observer.BookingConfirmedAsync(booking);
        await observer.BookingDeclinedAsync(booking);
        await observer.BookingCancelledAsync(booking);
        await observer.BookingMovedAsync(booking, previous);
        await observer.BookingPlacedOnBehalfAsync(booking);

        // Type AND order AND count: five calls, five publications, none swapped and none
        // doubled. A mapping test that only checked presence would pass with confirm and
        // decline crossed over.
        Assert.Collection(
            aggregator.Published,
            n => Assert.Same(booking, Assert.IsType<BookingPlacedNotification>(n).Booking),
            n => Assert.Same(booking, Assert.IsType<BookingConfirmedNotification>(n).Booking),
            n => Assert.Same(booking, Assert.IsType<BookingDeclinedNotification>(n).Booking),
            n => Assert.Same(booking, Assert.IsType<BookingCancelledNotification>(n).Booking),
            n =>
            {
                var moved = Assert.IsType<BookingMovedNotification>(n);
                Assert.Same(booking, moved.Booking);
                Assert.Same(previous, moved.PreviousInterval);
            },

            // An operator's placement publishes its OWN type, not the ordinary placement one.
            // IsType is exact, so this fails if the adapter falls through to the interface's
            // default implementation — which would publish BookingPlacedNotification and tell
            // the site's internal recipients about a booking their colleague just took.
            n => Assert.Same(
                booking, Assert.IsType<BookingPlacedOnBehalfNotification>(n).Booking));
    }

    [Fact]
    public async Task A_failing_subscriber_is_swallowed_and_logged_with_the_id_only()
    {
        var aggregator = new RecordingAggregator { Throw = true };
        var logger = new CapturingLogger();
        var observer = new UmbracoBookingObserver(aggregator, logger);
        var booking = Booking();

        // None of the five may let the fault escape — the caller behind these is a placement,
        // a transition or a move that has already committed.
        await observer.BookingPlacedAsync(booking);
        await observer.BookingConfirmedAsync(booking);
        await observer.BookingDeclinedAsync(booking);
        await observer.BookingCancelledAsync(booking);
        await observer.BookingMovedAsync(booking, booking.Interval);

        Assert.Equal(5, logger.Entries.Count);

        foreach (var entry in logger.Entries)
        {
            Assert.Equal(LogLevel.Error, entry.Level);

            // The id is the anti-vacuity anchor — and it is a GUID, so it comes OUT of the
            // haystack before names are looked for in it ("Ada" is three hex digits).
            Assert.Contains(booking.Id.ToString(), entry.Message, StringComparison.Ordinal);

            var haystack = GuidRedaction.WithoutGuids(entry.Message);
            Assert.DoesNotContain("Ada", haystack, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Lovelace", haystack, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ada@example.com", haystack, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("07700", haystack, StringComparison.Ordinal);
        }
    }
}
