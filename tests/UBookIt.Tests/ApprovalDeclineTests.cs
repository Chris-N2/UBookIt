using UBookIt.Core;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Services;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The routes into the status machine's <c>Requested</c> arm: placement under
/// <c>AutoConfirm</c> off, and the confirm/decline operations that resolve it.
/// </summary>
/// <remarks>
/// <para>
/// The transitions themselves were specified and tested from v1 (<c>StatusMachineTests</c>);
/// what was missing until this change was every route in. These tests therefore reach
/// <c>Requested</c> the way production does — placement with the setting off — rather than by
/// rehydrating a booking into the state, so a regression in the route fails here and cannot
/// hide behind a fixture.
/// </para>
/// <para>
/// The observation assertions record <b>the store's write count at the moment the observer was
/// told</b>, not just that it was told: the in-memory store hands back the instance the domain
/// mutated, so a status read after the fact is <c>Confirmed</c> whether or not the update was
/// ever persisted. That lesson is <c>BookingObservationTests</c>'s, inherited rather than
/// relearned.
/// </para>
/// </remarks>
public class ApprovalDeclineTests
{
    private static readonly DateOnly Date = TestData.BaseDate;

    private static readonly UBookIt.Core.Resources.Resource Room = TestData.Room();

    /// <summary>Records what it was told and how many updates the store had taken.</summary>
    private sealed class RecordingObserver(InMemoryBookingStore store) : IBookingObserver
    {
        public List<(string What, BookingStatus StatusWhenTold, int UpdatesWhenTold)> Told { get; } = [];

        public Task BookingPlacedAsync(Booking booking, CancellationToken cancellationToken = default)
            => Record("placed", booking);

        public Task BookingConfirmedAsync(Booking booking, CancellationToken cancellationToken = default)
            => Record("confirmed", booking);

        public Task BookingDeclinedAsync(Booking booking, CancellationToken cancellationToken = default)
            => Record("declined", booking);

        public Task BookingCancelledAsync(Booking booking, CancellationToken cancellationToken = default)
            => Record("cancelled", booking);

        private Task Record(string what, Booking booking)
        {
            Told.Add((what, booking.Status, store.UpdateCount));
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingObserver : IBookingObserver
    {
        public Task BookingPlacedAsync(Booking booking, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The site's handler is broken.");

        public Task BookingConfirmedAsync(Booking booking, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The site's handler is broken.");

        public Task BookingDeclinedAsync(Booking booking, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The site's handler is broken.");

        public Task BookingCancelledAsync(Booking booking, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The site's handler is broken.");
    }

    private static (BookingService Bookings, InMemoryBookingStore Store, RecordingObserver Observer) Wire(
        bool autoConfirm)
    {
        var resources = new InMemoryResourceStore().Add(Room);
        var store = new InMemoryBookingStore();
        var observer = new RecordingObserver(store);
        var settings = TestData.Settings with { AutoConfirm = autoConfirm };

        return (
            new BookingService(resources, store, new FixedTimeProvider(TestData.Now), settings, observer),
            store,
            observer);
    }

    private static BookingRequest Request(string start = "09:00") => new()
    {
        ResourceId = Room.Id,
        Start = TestData.Utc(Date, start),
        Duration = TimeSpan.FromMinutes(60),
        Booker = TestData.Booker(),
    };

    private static async Task<Booking> PlaceRequestedAsync(BookingService bookings, string start = "09:00")
    {
        var placed = await bookings.PlaceAsync(Request(start));
        Assert.True(placed.Succeeded);
        Assert.Equal(BookingStatus.Requested, placed.Value.Status);
        return placed.Value;
    }

    // ------------------------------------------------------------------
    // What placement yields, per the setting.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Placement_under_auto_confirm_yields_a_confirmed_booking()
    {
        var (bookings, _, _) = Wire(autoConfirm: true);

        var placed = await bookings.PlaceAsync(Request());

        Assert.True(placed.Succeeded);
        Assert.Equal(BookingStatus.Confirmed, placed.Value.Status);
    }

    [Fact]
    public async Task Placement_under_approval_yields_a_requested_booking()
    {
        var (bookings, store, _) = Wire(autoConfirm: false);

        var placed = await bookings.PlaceAsync(Request());

        Assert.True(placed.Succeeded);
        Assert.Equal(BookingStatus.Requested, placed.Value.Status);

        // What STORAGE holds, not just what the result says.
        var stored = await store.GetBookingAsync(placed.Value.Id);
        Assert.Equal(BookingStatus.Requested, stored!.Status);
    }

    [Fact]
    public void The_default_settings_auto_confirm()
    {
        // The unconfigured default is today's behaviour, asserted on the type itself so a
        // changed default cannot hide behind every test naming the value explicitly.
        Assert.True(new SiteBookingSettings { TimeZoneId = "UTC" }.AutoConfirm);
    }

    [Fact]
    public async Task Service_placement_agrees_with_direct_placement_under_approval()
    {
        // Both paths funnel through one placement site; this pins that a service booking
        // cannot come out confirmed while a direct one comes out requested.
        var resources = new InMemoryResourceStore().Add(Room);
        var serviceStore = new InMemoryServiceStore();
        var bookingStore = new InMemoryBookingStore();
        var time = new FixedTimeProvider(TestData.Now);
        var settings = TestData.Settings with { AutoConfirm = false };

        var service = Service.Create("Massage", null, [new ServiceRole(Room.Type, 1)]).Value;
        serviceStore.Add(service);

        var bookings = new BookingService(resources, bookingStore, time, settings);
        var services = new ServiceBookingService(
            serviceStore,
            resources,
            bookingStore,
            new UBookIt.Core.Availability.AvailabilityService(resources, bookingStore, time, settings),
            bookings,
            settings);

        var placed = await services.PlaceAsync(new ServiceBookingRequest
        {
            ServiceId = service.Id,
            Start = TestData.Utc(Date, "09:00"),
            Duration = TimeSpan.FromMinutes(60),
            Booker = TestData.Booker(),
        });

        Assert.True(placed.Succeeded);
        Assert.Equal(BookingStatus.Requested, placed.Value.Status);
    }

    [Fact]
    public async Task A_requested_booking_holds_its_slot()
    {
        var (bookings, _, _) = Wire(autoConfirm: false);
        await PlaceRequestedAsync(bookings);

        var second = await bookings.PlaceAsync(Request() with { Booker = TestData.Booker() });

        Assert.False(second.Succeeded);
        Assert.Contains(second.Failures, f => f.Code == FailureCodes.Conflict);
    }

    // ------------------------------------------------------------------
    // Confirm and decline.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Confirming_a_requested_booking_stores_and_reports_once()
    {
        var (bookings, store, observer) = Wire(autoConfirm: false);
        var booking = await PlaceRequestedAsync(bookings);

        var confirmed = await bookings.ConfirmAsync(booking.Id);

        Assert.True(confirmed.Succeeded);
        Assert.Equal(BookingStatus.Confirmed, (await store.GetBookingAsync(booking.Id))!.Status);

        var report = Assert.Single(observer.Told, t => t.What == "confirmed");
        Assert.Equal(BookingStatus.Confirmed, report.StatusWhenTold);
        // Told AFTER the store took the update — one write had landed when the report arrived.
        Assert.Equal(1, report.UpdatesWhenTold);
    }

    [Fact]
    public async Task Declining_a_requested_booking_stores_and_reports_once()
    {
        var (bookings, store, observer) = Wire(autoConfirm: false);
        var booking = await PlaceRequestedAsync(bookings);

        var declined = await bookings.DeclineAsync(booking.Id);

        Assert.True(declined.Succeeded);
        Assert.Equal(BookingStatus.Declined, (await store.GetBookingAsync(booking.Id))!.Status);

        var report = Assert.Single(observer.Told, t => t.What == "declined");
        Assert.Equal(BookingStatus.Declined, report.StatusWhenTold);
        Assert.Equal(1, report.UpdatesWhenTold);
    }

    [Theory]
    [InlineData("confirm")]
    [InlineData("decline")]
    public async Task A_transition_from_a_non_requested_status_is_refused_and_unreported(string operation)
    {
        // Confirmed, Declined and Cancelled in turn — the full class of wrong starting
        // statuses, not a sample of it.
        var (bookings, store, observer) = Wire(autoConfirm: false);

        var confirmedBooking = await PlaceRequestedAsync(bookings, "09:00");
        Assert.True((await bookings.ConfirmAsync(confirmedBooking.Id)).Succeeded);

        var declinedBooking = await PlaceRequestedAsync(bookings, "11:00");
        Assert.True((await bookings.DeclineAsync(declinedBooking.Id)).Succeeded);

        var cancelledBooking = await PlaceRequestedAsync(bookings, "13:00");
        Assert.True((await bookings.CancelAsync(cancelledBooking.Id)).Succeeded);

        var updatesBefore = store.UpdateCount;
        var reportsBefore = observer.Told.Count;

        foreach (var booking in new[] { confirmedBooking, declinedBooking, cancelledBooking })
        {
            var result = operation == "confirm"
                ? await bookings.ConfirmAsync(booking.Id)
                : await bookings.DeclineAsync(booking.Id);

            Assert.False(result.Succeeded);
            Assert.Contains(result.Failures, f => f.Code == FailureCodes.InvalidStatusTransition);
        }

        // The store was not touched and nothing was reported — for any of the three.
        Assert.Equal(updatesBefore, store.UpdateCount);
        Assert.Equal(reportsBefore, observer.Told.Count);
    }

    [Theory]
    [InlineData("confirm")]
    [InlineData("decline")]
    public async Task An_unknown_booking_is_not_found_and_unreported(string operation)
    {
        var (bookings, store, observer) = Wire(autoConfirm: false);

        var result = operation == "confirm"
            ? await bookings.ConfirmAsync(Guid.NewGuid())
            : await bookings.DeclineAsync(Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Code == FailureCodes.BookingNotFound);
        Assert.Equal(0, store.UpdateCount);
        Assert.Empty(observer.Told);
    }

    [Fact]
    public async Task A_throwing_observer_does_not_break_a_confirmation_or_a_decline()
    {
        var resources = new InMemoryResourceStore().Add(Room);
        var store = new InMemoryBookingStore();
        var settings = TestData.Settings with { AutoConfirm = false };
        var bookings = new BookingService(
            resources, store, new FixedTimeProvider(TestData.Now), settings, new ThrowingObserver());

        var first = await bookings.PlaceAsync(Request("09:00"));
        var second = await bookings.PlaceAsync(Request("11:00") with { Booker = TestData.Booker() });

        var confirmed = await bookings.ConfirmAsync(first.Value.Id);
        var declined = await bookings.DeclineAsync(second.Value.Id);

        Assert.True(confirmed.Succeeded);
        Assert.True(declined.Succeeded);
        Assert.Equal(BookingStatus.Confirmed, (await store.GetBookingAsync(first.Value.Id))!.Status);
        Assert.Equal(BookingStatus.Declined, (await store.GetBookingAsync(second.Value.Id))!.Status);
    }

    [Fact]
    public async Task A_decline_releases_the_slot()
    {
        var (bookings, _, _) = Wire(autoConfirm: false);
        var booking = await PlaceRequestedAsync(bookings);

        Assert.True((await bookings.DeclineAsync(booking.Id)).Succeeded);

        // The same request that would have conflicted a moment ago now succeeds.
        var replacement = await bookings.PlaceAsync(Request() with { Booker = TestData.Booker() });
        Assert.True(replacement.Succeeded);
    }

    [Fact]
    public async Task A_requested_booking_can_be_cancelled_without_being_confirmed_first()
    {
        // The booker-withdrew path: the status machine has always permitted it, and this pins
        // that the new Requested route did not disturb it.
        var (bookings, store, _) = Wire(autoConfirm: false);
        var booking = await PlaceRequestedAsync(bookings);

        var cancelled = await bookings.CancelAsync(booking.Id);

        Assert.True(cancelled.Succeeded);
        Assert.Equal(BookingStatus.Cancelled, (await store.GetBookingAsync(booking.Id))!.Status);
    }
}
