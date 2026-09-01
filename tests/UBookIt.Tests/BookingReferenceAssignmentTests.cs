using UBookIt.Core.Bookings;
using UBookIt.Core.Resources;
using UBookIt.Core.Common;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// What placement does with references: assigns one, keeps it, and copes when it draws one
/// that is already taken.
/// </summary>
public class BookingReferenceAssignmentTests
{
    private static readonly DateOnly Date = TestData.BaseDate;

    private static BookingRequest Request(Guid resourceId, string start) => new()
    {
        ResourceId = resourceId,
        Start = TestData.Utc(Date, start),
        Duration = TimeSpan.FromHours(1),
        Booker = TestData.Booker(),
    };

    private static (BookingService Bookings, InMemoryBookingStore Store, Resource Room) Rig(
        IBookingReferenceFactory? factory = null)
    {
        var room = TestData.Room();
        var resources = new InMemoryResourceStore().Add(room);
        var store = new InMemoryBookingStore();
        var time = new FixedTimeProvider(TestData.Now);

        return (new BookingService(resources, store, time, TestData.Settings, referenceFactory: factory), store, room);
    }

    [Fact]
    public async Task A_placed_booking_carries_a_reference()
    {
        var (bookings, _, room) = Rig();

        var placed = await bookings.PlaceAsync(Request(room.Id, "09:00"));

        Assert.True(placed.Succeeded);
        Assert.Equal(BookingReference.Length, placed.Value.Reference.Value.Length);
    }

    [Fact]
    public async Task Two_bookings_do_not_share_a_reference()
    {
        var (bookings, _, room) = Rig();

        var first = await bookings.PlaceAsync(Request(room.Id, "09:00"));
        var second = await bookings.PlaceAsync(Request(room.Id, "11:00"));

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.NotEqual(first.Value.Reference, second.Value.Reference);
    }

    [Fact]
    public async Task A_taken_reference_is_retried_rather_than_failing_the_booking()
    {
        // The reason the generation seam exists. A collision is a problem for the package to
        // solve, never for the booker to be told about: they asked for a room at nine, and
        // which arbitrary label it gets is not their concern.
        var taken = References.Of("7QX4M2NP");
        var factory = new ScriptedBookingReferenceFactory(taken, taken);
        var (bookings, _, room) = Rig(factory);

        var first = await bookings.PlaceAsync(Request(room.Id, "09:00"));
        var second = await bookings.PlaceAsync(Request(room.Id, "11:00"));

        Assert.True(first.Succeeded);
        Assert.Equal(taken, first.Value.Reference);

        // The second draw returned the same reference, the store refused it, and placement
        // asked for another rather than surfacing the collision.
        Assert.True(second.Succeeded);
        Assert.NotEqual(taken, second.Value.Reference);
        Assert.Equal(3, factory.Calls);
    }

    [Fact]
    public async Task A_generator_that_never_yields_a_free_reference_is_reported_as_a_fault()
    {
        // Bounded, so a broken generator is an error rather than a hang. Deliberately not a
        // DomainResult failure: nothing the caller did caused it and nothing they can do fixes
        // it, so it is not a validation outcome — and the delivery API's failure mapping is a
        // contract that should not gain a code for "our generator is broken".
        var factory = new FixedBookingReferenceFactory(References.Of("7QX4M2NP"));
        var (bookings, _, room) = Rig(factory);

        var first = await bookings.PlaceAsync(Request(room.Id, "09:00"));
        Assert.True(first.Succeeded);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => bookings.PlaceAsync(Request(room.Id, "11:00")));
    }

    [Fact]
    public async Task A_rejected_placement_is_not_retried()
    {
        // The retry loop exists for reference collisions and must not turn any other failure
        // into repeated attempts — a conflicting interval is still one attempt, and the
        // pipeline above it runs once.
        var factory = new ScriptedBookingReferenceFactory();
        var (bookings, _, room) = Rig(factory);

        var first = await bookings.PlaceAsync(Request(room.Id, "09:00"));
        Assert.True(first.Succeeded);

        var clash = await bookings.PlaceAsync(Request(room.Id, "09:00"));

        Assert.False(clash.Succeeded);
        Assert.Equal(FailureCodes.Conflict, Assert.Single(clash.Failures).Code);
        Assert.Equal(2, factory.Calls);
    }

    [Theory]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Declined)]
    public void A_reference_survives_every_status_transition(BookingStatus target)
    {
        // Asserted by transitioning, not by inspecting that no code assigns it. The customer
        // is holding the reference they were given; a system that changes it denies all
        // knowledge of the booking they are asking about.
        var reference = References.Of("7QX4M2NP");

        var booking = Booking.Create(
            Guid.NewGuid(),
            reference,
            BookingInterval.Create(
                TestData.Utc(Date, "09:00"), TestData.Utc(Date, "10:00"), TestData.LondonZoneId).Value,
            TestData.Booker(),
            [new ResourceClaim(Guid.NewGuid())],
            BookingStatus.Requested,
            TestData.Now,
            service: null);

        var result = target switch
        {
            BookingStatus.Confirmed => booking.Confirm(),
            BookingStatus.Declined => booking.Decline(),
            _ => booking.Cancel(),
        };

        Assert.True(result.Succeeded);
        Assert.Equal(target, booking.Status);
        Assert.Equal(reference, booking.Reference);
    }
}
