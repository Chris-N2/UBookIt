using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Resources;
using UBookIt.Core.Stores;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// What an operator's placement tells people (booking-emails spec, "Which events produce
/// messages, and for whom"): the booker, and nobody else.
/// </summary>
/// <remarks>
/// <para>
/// <b>The path has four links and no single test can see all of them</b> — Core to the
/// observation port, the port to a notification, the notification to a registered handler, the
/// handler to a sender. A guard over each side of a seam stays green through a regression that
/// breaks the seam, which this project has paid for before, so each link is asserted here or in
/// the file named against it:
/// </para>
/// <list type="bullet">
/// <item>Core → port: <see cref="An_operators_placement_is_reported_as_its_own_observation"/>.</item>
/// <item>port → notification: <c>UmbracoBookingObserverTests</c>, the mapping collection.</item>
/// <item>notification → handler: <c>NotificationRegistrationTests</c>.</item>
/// <item>handler → sender: <c>BookingEmailTests</c>, where the composer fixtures live.</item>
/// </list>
/// </remarks>
public class PlaceOnBehalfNotificationTests
{
    private static readonly DateOnly Date = TestData.BaseDate;

    private static Guid ResourceId => new("00000000-0000-0000-0000-00000000000a");

    private static Resource Room() => Resource.Create(
        ResourceTypes.Room,
        "Meeting Room A",
        directlyBookable: true,
        availability: TestData.Config(TestData.Weekly("08:00", "18:00", Date.DayOfWeek)),
        id: ResourceId).Value;

    private sealed class RecordingObserver : IBookingObserver
    {
        public List<string> Heard { get; } = [];

        public Task BookingPlacedAsync(Booking booking, CancellationToken cancellationToken = default)
        {
            Heard.Add("placed");
            return Task.CompletedTask;
        }

        public Task BookingPlacedOnBehalfAsync(
            Booking booking, CancellationToken cancellationToken = default)
        {
            Heard.Add("placed-on-behalf");
            return Task.CompletedTask;
        }

        public Task BookingConfirmedAsync(Booking booking, CancellationToken cancellationToken = default)
        {
            Heard.Add("confirmed");
            return Task.CompletedTask;
        }

        public Task BookingDeclinedAsync(Booking booking, CancellationToken cancellationToken = default)
        {
            Heard.Add("declined");
            return Task.CompletedTask;
        }

        public Task BookingCancelledAsync(Booking booking, CancellationToken cancellationToken = default)
        {
            Heard.Add("cancelled");
            return Task.CompletedTask;
        }

        public Task BookingMovedAsync(
            Booking booking, BookingInterval previousInterval, CancellationToken cancellationToken = default)
        {
            Heard.Add("moved");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// A host's implementation from before operator placement existed: it never heard of the
    /// event, so it does not override the interface's default.
    /// </summary>
    private sealed class ObserverFromBeforeThisChange : IBookingObserver
    {
        public List<string> Heard { get; } = [];

        public Task BookingPlacedAsync(Booking booking, CancellationToken cancellationToken = default)
        {
            Heard.Add("placed");
            return Task.CompletedTask;
        }

        public Task BookingConfirmedAsync(Booking booking, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task BookingDeclinedAsync(Booking booking, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task BookingCancelledAsync(Booking booking, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task BookingMovedAsync(
            Booking booking, BookingInterval previousInterval, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private static (BookingService Bookings, T Observer) Wire<T>(T observer)
        where T : IBookingObserver
    {
        var resources = new InMemoryResourceStore().Add(Room());
        var store = new InMemoryBookingStore();
        var settings = new SiteBookingSettings { TimeZoneId = TestData.LondonZoneId };

        return (
            new BookingService(
                resources, store, new FixedTimeProvider(TestData.Now), settings, observer),
            observer);
    }

    private static BookingRequest Request(string start = "10:00") => new()
    {
        ResourceId = ResourceId,
        Start = TestData.Utc(Date, start),
        Duration = TimeSpan.FromMinutes(60),
        Booker = TestData.Booker(),
    };

    // ---- Core → the observation port ---------------------------------------------------------

    [Fact]
    public async Task An_operators_placement_is_reported_as_its_own_observation()
    {
        var (bookings, observer) = Wire(new RecordingObserver());

        await bookings.PlaceOnBehalfAsync(Request());

        Assert.Equal(["placed-on-behalf"], observer.Heard);
    }

    [Fact]
    public async Task A_visitors_placement_is_still_reported_as_a_placement()
    {
        var (bookings, observer) = Wire(new RecordingObserver());

        await bookings.PlaceAsync(Request());

        Assert.Equal(["placed"], observer.Heard);
    }

    [Fact]
    public async Task Spec_scenario_the_observation_port_gains_nothing_a_host_must_implement()
    {
        // The default implementation, exercised rather than asserted: a host that implemented
        // this port before operator placement existed still compiles — this class is the proof,
        // since it does not declare the member — and still hears that a booking was placed.
        // Under-reporting a distinction, never inventing one, and never losing the placement.
        var (bookings, observer) = Wire(new ObserverFromBeforeThisChange());

        await bookings.PlaceOnBehalfAsync(Request());

        Assert.Equal(["placed"], observer.Heard);
    }

    [Fact]
    public async Task A_refused_operator_placement_reports_nothing()
    {
        var (bookings, observer) = Wire(new RecordingObserver());

        var refused = await bookings.PlaceOnBehalfAsync(Request(start: "07:00"));

        Assert.False(refused.Succeeded);
        Assert.Empty(observer.Heard);
    }
}
