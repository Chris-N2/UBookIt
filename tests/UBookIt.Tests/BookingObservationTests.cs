using Microsoft.Extensions.DependencyInjection;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Persistence.Composing;
using UBookIt.Persistence.Notifications;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// What the domain reports, when, and what an observer cannot do to it.
/// </summary>
/// <remarks>
/// <para>
/// The guarantee worth the most here is negative: <b>an observer cannot break a booking</b>.
/// By the time one runs, the booking is stored. If an exception escaped, a visitor who
/// successfully booked would be told it failed — and a visitor told that books again. A
/// double booking caused by somebody else's mail handler is worse than any notification is
/// worth, so this is absolute rather than best-effort.
/// </para>
/// <para>
/// The others are about <i>when</i> rather than <i>what</i>, which is the kind of assertion
/// that passes for the wrong reason: a test that merely counts reports is satisfied by a
/// report raised before the store agreed, or on a failure.
/// </para>
/// </remarks>
public class BookingObservationTests
{
    private static readonly DateOnly Date = TestData.BaseDate;


    /// <summary>Records what it was told, in order.</summary>
    private sealed class RecordingObserver : IBookingObserver
    {
        public List<(string What, Booking Booking)> Told { get; } = [];

        public Task BookingPlacedAsync(Booking booking, CancellationToken cancellationToken = default)
        {
            Told.Add(("placed", booking));
            return Task.CompletedTask;
        }

        public Task BookingCancelledAsync(Booking booking, CancellationToken cancellationToken = default)
        {
            Told.Add(("cancelled", booking));
            return Task.CompletedTask;
        }
    }

    /// <summary>The badly-behaved subscriber this design exists to survive.</summary>
    private sealed class ThrowingObserver : IBookingObserver
    {
        public Task BookingPlacedAsync(Booking booking, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The site's handler is broken.");

        public Task BookingCancelledAsync(Booking booking, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The site's handler is broken.");
    }

    /// <summary>
    /// Records what the store held <b>at the moment it was told</b>, so "after the commit" is
    /// assertable rather than assumed.
    /// </summary>
    /// <remarks>
    /// It records the status as a <b>value</b>, not the booking. The in-memory store hands
    /// back the same instance it holds, so keeping the object would mean both recordings
    /// pointed at one booking that cancellation had since mutated — the placement entry would
    /// read <c>Cancelled</c> and the test would fail while the code was right. The first draft
    /// did exactly that, which is the second time this store's instance-sharing has caught me.
    /// </remarks>
    private sealed class CountingObserver(InMemoryBookingStore store) : IBookingObserver
    {
        public List<BookingStatus?> StatusWhenTold { get; } = [];

        /// <summary>
        /// How many writes the store had taken when each report arrived.
        /// </summary>
        /// <remarks>
        /// The status alone cannot show that cancellation was <i>persisted</i>: this store
        /// keeps the instance the domain mutated, so it reads <c>Cancelled</c> whether or not
        /// <c>UpdateAsync</c> was ever called. Deleting that call left every test here green
        /// until this was recorded too.
        /// </remarks>
        public List<int> UpdatesWhenTold { get; } = [];

        public Task BookingPlacedAsync(Booking booking, CancellationToken cancellationToken = default)
            => RecordAsync(booking.Id, cancellationToken);

        public Task BookingCancelledAsync(Booking booking, CancellationToken cancellationToken = default)
            => RecordAsync(booking.Id, cancellationToken);

        private async Task RecordAsync(Guid bookingId, CancellationToken cancellationToken)
        {
            var stored = await store.GetBookingAsync(bookingId, cancellationToken);
            StatusWhenTold.Add(stored?.Status);
            UpdatesWhenTold.Add(store.UpdateCount);
        }
    }

    private static readonly Resource Room = TestData.Room();

    /// <summary>
    /// The observer, the service and the store the service actually writes to — returned
    /// together because a test that let the observer read a DIFFERENT store would assert
    /// nothing while looking correct.
    /// </summary>
    private static (BookingService Service, InMemoryBookingStore Store) Wire(
        Func<InMemoryBookingStore, IBookingObserver> observer)
    {
        var resources = new InMemoryResourceStore().Add(Room);
        var store = new InMemoryBookingStore();

        return (
            new BookingService(
                resources, store, new FixedTimeProvider(TestData.Now), TestData.Settings, observer(store)),
            store);
    }

    private static BookingRequest Request(string start = "09:00") => new()
    {
        ResourceId = Room.Id,
        Start = TestData.Utc(Date, start),
        Duration = TimeSpan.FromMinutes(60),
        Booker = TestData.Booker(),
    };

    [Fact]
    public async Task A_placed_booking_is_reported_once()
    {
        var observer = new RecordingObserver();
        var (service, _) = Wire(_ => observer);

        var placed = await service.PlaceAsync(Request());

        Assert.True(placed.Succeeded);
        var told = Assert.Single(observer.Told);
        Assert.Equal("placed", told.What);
        Assert.Equal(placed.Value.Id, told.Booking.Id);
    }

    [Fact]
    public async Task A_cancelled_booking_is_reported_once()
    {
        var observer = new RecordingObserver();
        var (service, _) = Wire(_ => observer);

        var placed = await service.PlaceAsync(Request());
        var cancelled = await service.CancelAsync(placed.Value.Id);

        Assert.True(cancelled.Succeeded);
        Assert.Equal(["placed", "cancelled"], observer.Told.Select(t => t.What));
        Assert.Equal(BookingStatus.Cancelled, observer.Told[1].Booking.Status);
    }

    [Fact]
    public async Task A_placement_refused_by_the_rules_reports_nothing()
    {
        // Outside open hours. Worth having, and worth knowing what it does NOT prove: this
        // failure returns from the pipeline long before the report site, so it cannot catch a
        // report raised on failure. That is the test below.
        var observer = new RecordingObserver();
        var (service, _) = Wire(_ => observer);

        var placed = await service.PlaceAsync(Request(start: "05:00"));

        Assert.False(placed.Succeeded);
        Assert.Empty(observer.Told);
    }

    [Fact]
    public async Task A_placement_the_STORE_refuses_reports_nothing()
    {
        // The one that actually guards the rule. A conflict is the only failure that happens
        // AFTER the booking is constructed and the report site is reached — every other
        // failure returns earlier, so a report moved outside its success check would still
        // never fire for them.
        //
        // Found by mutation: removing the success check left the whole suite green, because
        // the only failing-placement test used an out-of-hours request that exits the
        // pipeline first.
        //
        // An event raised on failure is a lie a subscriber cannot detect — it would send a
        // confirmation for a booking that does not exist.
        var observer = new RecordingObserver();
        var (service, _) = Wire(_ => observer);

        var first = await service.PlaceAsync(Request());
        Assert.True(first.Succeeded);
        observer.Told.Clear();

        var second = await service.PlaceAsync(Request());

        Assert.False(second.Succeeded);
        Assert.Equal(FailureCodes.Conflict, Assert.Single(second.Failures).Code);
        Assert.Empty(observer.Told);
    }

    [Fact]
    public async Task Cancelling_an_already_cancelled_booking_reports_nothing()
    {
        // And this is what makes the cancellation report unambiguous: it is only ever raised
        // for a transition that actually happened, so it needs no before-and-after to say
        // which way the booking moved.
        var observer = new RecordingObserver();
        var (service, _) = Wire(_ => observer);

        var placed = await service.PlaceAsync(Request());
        await service.CancelAsync(placed.Value.Id);
        observer.Told.Clear();

        var again = await service.CancelAsync(placed.Value.Id);

        Assert.False(again.Succeeded);
        Assert.Equal(FailureCodes.InvalidStatusTransition, Assert.Single(again.Failures).Code);
        Assert.Empty(observer.Told);
    }

    [Fact]
    public async Task An_unknown_booking_cannot_be_cancelled_and_reports_nothing()
    {
        var observer = new RecordingObserver();
        var (service, _) = Wire(_ => observer);

        var result = await service.CancelAsync(Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.BookingNotFound, Assert.Single(result.Failures).Code);
        Assert.Empty(observer.Told);
    }

    [Fact]
    public async Task A_throwing_observer_does_not_break_the_placement()
    {
        // The sharpest risk in the whole change. The booking is committed before the observer
        // runs, so an escaping exception reports failure for a booking that exists, and the
        // visitor books again.
        var (service, store) = Wire(_ => new ThrowingObserver());

        var placed = await service.PlaceAsync(Request());

        Assert.True(placed.Succeeded);

        // And the booking is really there, unchanged — not merely reported as successful.
        var stored = await store.GetBookingAsync(placed.Value.Id);
        Assert.NotNull(stored);
        Assert.Equal(BookingStatus.Confirmed, stored!.Status);
    }

    [Fact]
    public async Task A_throwing_observer_does_not_break_the_cancellation()
    {
        var (service, store) = Wire(_ => new ThrowingObserver());

        var placed = await service.PlaceAsync(Request());
        var cancelled = await service.CancelAsync(placed.Value.Id);

        Assert.True(cancelled.Succeeded);

        var stored = await store.GetBookingAsync(placed.Value.Id);
        Assert.Equal(BookingStatus.Cancelled, stored?.Status);
    }

    [Fact]
    public async Task The_booking_is_already_stored_when_the_observer_is_told()
    {
        // "After the commit" is the guarantee, and counting reports would not notice one
        // raised before the store agreed — which would announce a booking that might never
        // exist. So the observer reads the very store the service writes to, and records what
        // it found at the moment it was told. An observer reading a DIFFERENT store would
        // assert nothing while looking entirely correct, which is why `Wire` hands it over.
        CountingObserver? observer = null;
        var (service, _) = Wire(store => observer = new CountingObserver(store));

        var placed = await service.PlaceAsync(Request());
        await service.CancelAsync(placed.Value.Id);

        // Each entry is the status the store held when that report was raised. Placement's
        // entry being Confirmed proves the booking was committed before the observer ran; a
        // report raised first would have found nothing stored and recorded null.
        Assert.NotNull(observer);
        Assert.Equal(
            [BookingStatus.Confirmed, BookingStatus.Cancelled],
            observer!.StatusWhenTold);

        // Placement's entry reports zero updates (it was an insert); cancellation's reports
        // one, which is the part the status alone cannot show. Without this, deleting the
        // UpdateAsync call from cancellation leaves every test here green — the store keeps
        // the instance the domain mutated, so it reads Cancelled either way.
        Assert.Equal([0, 1], observer.UpdatesWhenTold);
    }

    [Fact]
    public void The_package_registers_a_real_observer()
    {
        // The residual risk of defaulting the constructor parameter, and the reason that
        // default is defensible: a host that never registered an observer would get silence,
        // and silence looks exactly like a site with no subscribers. So the registration is
        // asserted rather than trusted.
        //
        // Asserted against the composer's own registration list — not by resolving, which
        // would need a container Umbraco has already configured.
        var registrations = new ServiceCollection();
        new UBookItPersistenceComposer().Compose(new ServicesOnlyUmbracoBuilder(registrations));

        var observer = Assert.Single(
            registrations, d => d.ServiceType == typeof(IBookingObserver));

        Assert.Equal(typeof(UmbracoBookingObserver), observer.ImplementationType);

        // And not the no-op, which would be a registration that satisfies a presence check
        // while telling nobody anything.
        Assert.NotEqual(typeof(NullBookingObserver), observer.ImplementationType);
    }

    [Fact]
    public async Task Omitting_the_observer_is_silence_rather_than_a_null_reference()
    {
        // The default exists so the several dozen placement tests that predate observation
        // need not care about it. It must be a null OBJECT rather than a null check
        // scattered through the service — and it must actually place a booking, not merely
        // construct without throwing.
        var service = new BookingService(
            new InMemoryResourceStore().Add(Room),
            new InMemoryBookingStore(),
            new FixedTimeProvider(TestData.Now),
            TestData.Settings);

        var placed = await service.PlaceAsync(Request());

        Assert.True(placed.Succeeded);
    }
}
