using System.Reflection;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The visitor's cancellation terms (`self-service-cancellation`, "A visitor may not cancel a
/// booking that has started" and "A self-service cancellation is an ordinary cancellation").
/// </summary>
/// <remarks>
/// <b>These tests drive both entry points against the SAME booking in the SAME store</b>, because
/// the claim under test is that the two diverge — and a test exercising only one of them would
/// assert a property of that one rather than the difference between them. Testing each half
/// separately is how a seam stays untested while both sides are green.
/// </remarks>
public class VisitorCancellationTests
{
    private static readonly Resource Room = TestData.Room();

    /// <summary>A start the room is open for — 02:00 is not, and placement refuses it.</summary>
    private static readonly DateTimeOffset Start = TestData.Utc(TestData.BaseDate, "09:00");

    /// <summary>Comfortably before <see cref="Start"/>, and clear of any lead time.</summary>
    private static readonly DateTimeOffset BeforeIt = Start.AddDays(-1);

    private sealed class RecordingObserver : IBookingObserver
    {
        public List<Guid> Cancelled { get; } = [];

        public Task BookingCancelledAsync(Booking booking, CancellationToken cancellationToken = default)
        {
            Cancelled.Add(booking.Id);
            return Task.CompletedTask;
        }

        public Task BookingPlacedAsync(Booking booking, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task BookingConfirmedAsync(Booking booking, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task BookingDeclinedAsync(Booking booking, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task BookingMovedAsync(
            Booking booking, BookingInterval previousInterval, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    /// <summary>A service over the given store, with the clock wherever the test needs it.</summary>
    private static BookingService At(
        InMemoryBookingStore store, DateTimeOffset now, IBookingObserver? observer = null)
        => observer is null
            ? new BookingService(new InMemoryResourceStore().Add(Room), store, new FixedTimeProvider(now), TestData.Settings)
            : new BookingService(new InMemoryResourceStore().Add(Room), store, new FixedTimeProvider(now), TestData.Settings, observer);

    private static async Task<Booking> PlaceAsync(BookingService bookings, DateTimeOffset start)
    {
        var placed = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = Room.Id,
            Start = start,
            Duration = TimeSpan.FromMinutes(60),
            Booker = TestData.Booker(),
        });

        Assert.True(placed.Succeeded, string.Join("; ", placed.Failures.Select(failure => failure.Message)));
        return placed.Value!;
    }

    private static async Task<BookingStatus> StatusAsync(InMemoryBookingStore store, Guid id)
        => (await store.GetBookingAsync(id))!.Status;

    [Fact]
    public async Task A_visitor_cannot_cancel_a_booking_that_has_started_and_an_operator_can()
    {
        // THE SEAM: one booking, one store, one instant — both doors.
        var store = new InMemoryBookingStore();
        var booking = await PlaceAsync(At(store, BeforeIt), Start);

        var afterItBegan = At(store, Start.AddMinutes(1));

        var visitor = await afterItBegan.CancelAsVisitorAsync(booking.Id);
        Assert.False(visitor.Succeeded);
        Assert.Equal(FailureCodes.BookingAlreadyStarted, visitor.Failures[0].Code);
        Assert.Equal(BookingStatus.Confirmed, await StatusAsync(store, booking.Id));

        // The same booking, the same clock, the other entry point.
        var operatorResult = await afterItBegan.CancelAsync(booking.Id);
        Assert.True(operatorResult.Succeeded);
        Assert.Equal(BookingStatus.Cancelled, await StatusAsync(store, booking.Id));
    }

    [Fact]
    public async Task A_visitor_can_cancel_a_booking_that_has_not_started()
    {
        var store = new InMemoryBookingStore();
        var bookings = At(store, BeforeIt);
        var booking = await PlaceAsync(bookings, Start);

        Assert.True((await bookings.CancelAsVisitorAsync(booking.Id)).Succeeded);
        Assert.Equal(BookingStatus.Cancelled, await StatusAsync(store, booking.Id));
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public async Task The_refusal_begins_exactly_at_the_start(int ticksFromStart, bool expectedToSucceed)
    {
        // The boundary asserted rather than assumed: AT the start is already too late, because a
        // booking that has begun is one the visitor is asking to undo rather than to call off.
        var store = new InMemoryBookingStore();
        var booking = await PlaceAsync(At(store, BeforeIt), Start);

        var result = await At(store, Start.AddTicks(ticksFromStart)).CancelAsVisitorAsync(booking.Id);

        Assert.Equal(expectedToSucceed, result.Succeeded);
    }

    [Fact]
    public async Task A_visitor_cancellation_is_indistinguishable_from_any_other()
    {
        // "A self-service cancellation is an ordinary cancellation": same resulting state, same
        // observer call. Both driven through the production entry points.
        var visitorStore = new InMemoryBookingStore();
        var visitorObserver = new RecordingObserver();
        var visitorService = At(visitorStore, BeforeIt, visitorObserver);
        var visitorBooking = await PlaceAsync(visitorService, Start);
        await visitorService.CancelAsVisitorAsync(visitorBooking.Id);

        var operatorStore = new InMemoryBookingStore();
        var operatorObserver = new RecordingObserver();
        var operatorService = At(operatorStore, BeforeIt, operatorObserver);
        var operatorBooking = await PlaceAsync(operatorService, Start);
        await operatorService.CancelAsync(operatorBooking.Id);

        Assert.Equal(
            await StatusAsync(operatorStore, operatorBooking.Id),
            await StatusAsync(visitorStore, visitorBooking.Id));

        Assert.Equal([operatorBooking.Id], operatorObserver.Cancelled);
        Assert.Equal([visitorBooking.Id], visitorObserver.Cancelled);
    }

    [Fact]
    public async Task A_second_visitor_cancellation_is_refused()
    {
        var store = new InMemoryBookingStore();
        var bookings = At(store, BeforeIt);
        var booking = await PlaceAsync(bookings, Start);

        Assert.True((await bookings.CancelAsVisitorAsync(booking.Id)).Succeeded);

        var second = await bookings.CancelAsVisitorAsync(booking.Id);
        Assert.False(second.Succeeded);
        Assert.Equal(FailureCodes.InvalidStatusTransition, second.Failures[0].Code);
    }

    [Fact]
    public async Task An_unknown_booking_answers_the_same_as_the_operator_route()
    {
        // The codes must match, or a caller could tell "no such booking" from "a booking you may
        // not touch" by the difference — the oracle this capability exists to close.
        var bookings = At(new InMemoryBookingStore(), BeforeIt);
        var absent = Guid.NewGuid();

        var visitor = await bookings.CancelAsVisitorAsync(absent);
        var operatorResult = await bookings.CancelAsync(absent);

        Assert.Equal(operatorResult.Failures[0].Code, visitor.Failures[0].Code);
    }

    [Fact]
    public void The_waiver_is_structural_so_no_parameter_can_reach_the_other_terms()
    {
        // The claim D3 makes: terms are carried by WHICH ENTRY POINT is called, never by a value a
        // caller supplies. If either member grows a parameter beyond the booking and the token,
        // that claim has quietly stopped being true — and a flag is exactly how the operator's
        // waiver would become reachable from the visitor's door.
        foreach (var name in new[]
                 {
                     nameof(IBookingService.CancelAsync),
                     nameof(IBookingService.CancelAsVisitorAsync),
                 })
        {
            var method = typeof(IBookingService).GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(method);

            var parameters = method!.GetParameters();

            Assert.Equal(2, parameters.Length);
            Assert.Equal(typeof(Guid), parameters[0].ParameterType);
            Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        }
    }
}
