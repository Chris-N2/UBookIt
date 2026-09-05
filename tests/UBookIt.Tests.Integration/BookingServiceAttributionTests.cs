using Microsoft.EntityFrameworkCore;
using UBookIt.Core;
using UBookIt.Core.Bookings;
using UBookIt.Core.Services;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// The service a booking was placed for, against real SQL Server.
///
/// <para>
/// These are the guards for the failures that are quiet. A column that exists and is never
/// written reads as "every booking was placed directly"; a foreign key that looks obviously
/// correct turns "placed for a service since deleted" into the same thing. Neither shows up
/// as an error — both show up much later as a number nobody can reconcile.
/// </para>
/// <para>
/// Windows are far-future and disjoint from the management-list suite's, for the reason
/// stated there: the database is a shared fixture, so an absolute assertion needs a window
/// nothing else reaches into.
/// </para>
/// </summary>
[Collection(SqlServerCollection.Name)]
public class BookingServiceAttributionTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateTimeOffset Start(int dayOffset)
        => new DateTimeOffset(2031, 3, 1, 9, 0, 0, TimeSpan.Zero).AddDays(dayOffset);

    private async Task<Guid> PlaceAsync(Booking booking)
    {
        await using var context = fixture.CreateContext();
        var placed = await new SqlBookingStore(context).PlaceAsync(booking, Ct);

        Assert.True(placed.Succeeded);
        return placed.Value.Id;
    }

    private async Task<Service> SeedServiceAsync(string name)
    {
        var service = Service.Create(
            name,
            ServiceDuration.Fixed(TimeSpan.FromMinutes(60)).Value,
            [new ServiceRole("room", 1)]).Value;

        await using var context = fixture.CreateContext();
        Assert.True((await new SqlServiceManagementStore(context).CreateAsync(service, Ct)).Succeeded);

        return service;
    }

    [Fact]
    public async Task A_service_attribution_round_trips()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var service = await SeedServiceAsync("Initial Consultation");
        var attribution = new ServiceAttribution(service.Id, service.Name);

        var bookingId = await PlaceAsync(
            Seed.ConfirmedBooking(resourceId, Start(0), TimeSpan.FromHours(1), service: attribution));

        await using var context = fixture.CreateContext();
        var reloaded = await new SqlBookingStore(context).GetBookingAsync(bookingId, Ct);

        Assert.NotNull(reloaded);
        Assert.Equal(attribution, reloaded!.Service);
    }

    [Fact]
    public async Task A_directly_placed_booking_stores_no_service()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);

        var bookingId = await PlaceAsync(
            Seed.ConfirmedBooking(resourceId, Start(1), TimeSpan.FromHours(1)));

        await using var context = fixture.CreateContext();

        // Absent in the aggregate...
        var reloaded = await new SqlBookingStore(context).GetBookingAsync(bookingId, Ct);
        Assert.NotNull(reloaded);
        Assert.Null(reloaded!.Service);

        // ...and NULL in both columns, rather than an empty string standing in for one.
        var row = await context.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId, Ct);
        Assert.Null(row.ServiceId);
        Assert.Null(row.ServiceName);
    }

    [Fact]
    public async Task A_booking_outlives_the_service_it_names()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var service = await SeedServiceAsync("Sports Massage");

        var bookingId = await PlaceAsync(Seed.ConfirmedBooking(
            resourceId, Start(2), TimeSpan.FromHours(1),
            service: new ServiceAttribution(service.Id, service.Name)));

        await using (var deleting = fixture.CreateContext())
        {
            Assert.True((await new SqlServiceManagementStore(deleting).DeleteAsync(service.Id, Ct)).Succeeded);
        }

        await using var context = fixture.CreateContext();

        // The row survives with both values intact. A foreign key with ON DELETE SET NULL
        // would leave the booking here and its attribution gone — reporting a booking that
        // was sold as a service as though someone had walked in and booked the room.
        // A cascade would delete the booking outright.
        var row = await context.Bookings.AsNoTracking().SingleOrDefaultAsync(b => b.Id == bookingId, Ct);

        Assert.NotNull(row);
        Assert.Equal(service.Id, row!.ServiceId);
        Assert.Equal("Sports Massage", row.ServiceName);

        // And it still rehydrates: a booking is not made unreadable by having succeeded.
        var reloaded = await new SqlBookingStore(context).GetBookingAsync(bookingId, Ct);
        Assert.NotNull(reloaded);
        Assert.Equal(service.Id, reloaded!.Service?.ServiceId);
    }

    [Fact]
    public async Task The_recorded_name_does_not_follow_a_rename()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var service = await SeedServiceAsync("Consultation");

        var bookingId = await PlaceAsync(Seed.ConfirmedBooking(
            resourceId, Start(3), TimeSpan.FromHours(1),
            service: new ServiceAttribution(service.Id, service.Name)));

        await using (var renaming = fixture.CreateContext())
        {
            var renamed = Service.Create(
                "Consultation (Extended)", service.Duration, service.Roles, service.Id).Value;

            Assert.True((await new SqlServiceManagementStore(renaming).UpdateAsync(renamed, Ct)).Succeeded);
        }

        await using var context = fixture.CreateContext();
        var reloaded = await new SqlBookingStore(context).GetBookingAsync(bookingId, Ct);

        // What was sold, not what it is called now. A read-time join would say
        // "Consultation (Extended)" here, retitling a booking after the fact.
        Assert.Equal("Consultation", reloaded?.Service?.DisplayName);
    }

    [Fact]
    public async Task Cancelling_keeps_the_service_the_booking_was_placed_for()
    {
        // The narrowed "indistinguishable in shape" clause names cancellation, and this is
        // where it can actually be tested: the unit-level equivalent re-reads the same
        // object from an in-memory store, so it would agree with itself no matter what the
        // persistence layer did. Here the row goes back to SQL Server and comes back out.
        //
        // The failure this catches is a status update that rewrites the whole row: the
        // booking would come back cancelled and *directly placed*, turning a cancelled
        // service booking into a walk-in in exactly the reporting this change exists for.
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var service = await SeedServiceAsync("Follow-up");

        var bookingId = await PlaceAsync(Seed.ConfirmedBooking(
            resourceId, Start(7), TimeSpan.FromHours(1),
            service: new ServiceAttribution(service.Id, service.Name)));

        await using (var cancelling = fixture.CreateContext())
        {
            var store = new SqlBookingStore(cancelling);
            var booking = await store.GetBookingAsync(bookingId, Ct);

            Assert.NotNull(booking);
            Assert.True(booking!.Cancel().Succeeded);

            await store.UpdateAsync(booking, Ct);
        }

        await using var context = fixture.CreateContext();
        var reloaded = await new SqlBookingStore(context).GetBookingAsync(bookingId, Ct);

        Assert.Equal(BookingStatus.Cancelled, reloaded?.Status);
        Assert.Equal(service.Id, reloaded?.Service?.ServiceId);
        Assert.Equal("Follow-up", reloaded?.Service?.DisplayName);
    }

    [Fact]
    public async Task Cancelling_is_not_deleting()
    {
        // The distinction the endpoint's verb rests on: POST .../cancel rather than DELETE.
        // A cancelled booking keeps its row, its interval, its booker and the service it was
        // placed for, and the list still returns it when cancelled bookings are asked for —
        // it merely stops holding its time.
        //
        // Asserted against real SQL Server because that is where "still there" means
        // anything: the domain returning a cancelled aggregate proves only that the object
        // in hand was mutated.
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var service = await SeedServiceAsync("Consultation");

        var from = Start(9);
        var to = from.AddDays(1);

        var bookingId = await PlaceAsync(Seed.ConfirmedBooking(
            resourceId, from.AddHours(1), TimeSpan.FromHours(1),
            service: new ServiceAttribution(service.Id, service.Name)));

        await using (var cancelling = fixture.CreateContext())
        {
            var store = new SqlBookingStore(cancelling);
            var booking = await store.GetBookingAsync(bookingId, Ct);

            Assert.True(booking!.Cancel().Succeeded);
            await store.UpdateAsync(booking, Ct);
        }

        await using var context = fixture.CreateContext();
        var query = BookingQuery.Create(
            from,
            to,
            new SiteBookingSettings { TimeZoneId = "UTC" },
            statuses: [BookingStatus.Cancelled]).Value;

        var listed = Assert.Single((await new SqlBookingManagementStore(context).ListAsync(query, Ct)).Items);

        Assert.Equal(bookingId, listed.BookingId);
        Assert.Equal(BookingStatus.Cancelled, listed.Status);
        Assert.Equal("Consultation", listed.Service?.DisplayName);
        var listedContact = listed.Booker.Contact;
        Assert.NotNull(listedContact);
        Assert.Equal("Integration Tester", listedContact.Name);
        Assert.Equal(from.AddHours(1), listed.Interval.StartUtc);
    }

    [Fact]
    public async Task A_cancelled_booking_stops_holding_its_time()
    {
        // The other half, and the one that makes cancelling worth having: the slot is free
        // again. `IsBlocking` is domain-level, but whether the CLAIM stops blocking is a
        // property of the stored data the availability reads run over.
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var start = Start(11);

        var bookingId = await PlaceAsync(
            Seed.ConfirmedBooking(resourceId, start.AddHours(1), TimeSpan.FromHours(1)));

        await using (var cancelling = fixture.CreateContext())
        {
            var store = new SqlBookingStore(cancelling);
            var booking = await store.GetBookingAsync(bookingId, Ct);
            Assert.True(booking!.Cancel().Succeeded);
            await store.UpdateAsync(booking, Ct);
        }

        await using var context = fixture.CreateContext();
        var claims = await new SqlBookingStore(context).GetClaimsAsync(
            resourceId, start, start.AddDays(1), Ct);

        // The claim row survives — a booking is a record — but nothing it holds blocks.
        Assert.All(claims, claim => Assert.False(
            claim.Status is BookingStatus.Requested or BookingStatus.Confirmed));
    }

    [Fact]
    public async Task The_management_list_carries_the_service_without_joining_to_it()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var service = await SeedServiceAsync("Deep Tissue");

        var from = Start(5);
        var to = from.AddDays(1);

        await PlaceAsync(Seed.ConfirmedBooking(
            resourceId, from.AddHours(1), TimeSpan.FromHours(1),
            service: new ServiceAttribution(service.Id, service.Name)));

        await PlaceAsync(Seed.ConfirmedBooking(resourceId, from.AddHours(4), TimeSpan.FromHours(1)));

        // Deleted before the list runs. The projection must still answer, which a join to
        // the service table could not do — this is the assertion that fails if anyone
        // "simplifies" the stored name away.
        await using (var deleting = fixture.CreateContext())
        {
            Assert.True((await new SqlServiceManagementStore(deleting).DeleteAsync(service.Id, Ct)).Succeeded);
        }

        await using var context = fixture.CreateContext();
        var query = BookingQuery.Create(
            from, to, new SiteBookingSettings { TimeZoneId = "UTC" }).Value;

        var page = await new SqlBookingManagementStore(context).ListAsync(query, Ct);

        Assert.Equal(2, page.Total);

        var attributed = Assert.Single(page.Items, item => item.Service is not null);
        Assert.Equal(service.Id, attributed.Service!.ServiceId);
        Assert.Equal("Deep Tissue", attributed.Service.DisplayName);

        Assert.Single(page.Items, item => item.Service is null);
    }
}
