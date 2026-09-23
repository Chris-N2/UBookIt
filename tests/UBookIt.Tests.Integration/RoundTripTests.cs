using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Persistence.Entities;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

[Collection(SqlServerCollection.Name)]
public class RoundTripTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Resource_availability_configuration_round_trips()
    {
        fixture.EnsureAvailable();

        var id = Guid.NewGuid();
        var closureDate = new DateOnly(2026, 12, 24);
        var overrideDate = new DateOnly(2026, 9, 21);
        var row = new ResourceRow
        {
            Id = id,
            Type = "room",
            DisplayName = "Round Trip Room",
            Description = "A room with the works",
            GranularityMinutes = 30,
            MinDurationMinutes = 60,
            MaxDurationMinutes = 240,
            LeadTimeMinutes = 120,
            HorizonDays = 30,
            OpenHours =
            [
                new OpenHoursRow { DayOfWeek = (int)DayOfWeek.Monday, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0) },
                new OpenHoursRow { DayOfWeek = (int)DayOfWeek.Monday, StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(14, 0) },
                new OpenHoursRow { DayOfWeek = (int)DayOfWeek.Tuesday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(17, 0) },
            ],
            Exceptions =
            [
                new ExceptionRow { Date = closureDate, StartTime = null, EndTime = null },
                new ExceptionRow { Date = overrideDate, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(22, 0) },
            ],
        };

        await using (var context = fixture.CreateContext())
        {
            context.Resources.Add(row);
            await context.SaveChangesAsync(Ct);
        }

        await using var readContext = fixture.CreateContext();
        var resource = await new SqlResourceStore(readContext, new SqlSiteClosureStore(readContext)).GetAsync(id, Ct);

        Assert.NotNull(resource);
        Assert.Equal("room", resource.Type);
        Assert.Equal("Round Trip Room", resource.DisplayName);
        Assert.Equal("A room with the works", resource.Description);

        var config = resource.Availability;
        Assert.Equal(
            [(new TimeOnly(8, 0), new TimeOnly(12, 0)), (new TimeOnly(12, 0), new TimeOnly(14, 0))],
            config.OpenHours.WindowsFor(DayOfWeek.Monday).Select(w => (w.Start, w.End)).ToArray());
        Assert.Single(config.OpenHours.WindowsFor(DayOfWeek.Tuesday));
        Assert.Empty(config.OpenHours.WindowsFor(DayOfWeek.Wednesday));

        Assert.True(config.ExceptionFor(closureDate)!.IsClosure);
        var overrideException = config.ExceptionFor(overrideDate)!;
        Assert.Equal(new TimeOnly(22, 0), Assert.Single(overrideException.Windows).End);

        Assert.Equal(TimeSpan.FromMinutes(30), config.Constraints.Granularity);
        Assert.Equal(TimeSpan.FromMinutes(60), config.Constraints.MinDuration);
        Assert.Equal(TimeSpan.FromMinutes(240), config.Constraints.MaxDuration);
        Assert.Equal(TimeSpan.FromMinutes(120), config.Constraints.LeadTime);
        Assert.Equal(30, config.Constraints.HorizonDays);
    }

    [Fact]
    public async Task Booking_round_trips_through_place_and_reload()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var start = new DateTimeOffset(2026, 9, 15, 9, 0, 0, TimeSpan.Zero);
        var booking = Seed.ConfirmedBooking(resourceId, start, TimeSpan.FromHours(1));

        await using (var placeContext = fixture.CreateContext())
        {
            var placed = await new SqlBookingStore(placeContext).PlaceAsync(booking, Ct);
            Assert.True(placed.Succeeded);
        }

        await using var readContext = fixture.CreateContext();
        var reloaded = await new SqlBookingStore(readContext).GetBookingAsync(booking.Id, Ct);

        Assert.NotNull(reloaded);
        Assert.Equal(booking.Id, reloaded.Id);

        // Through a real column and back, canonical and unchanged. Rehydration goes via
        // FromCanonical, which throws on anything non-canonical — so a store that lower-cased
        // or padded the value would fail here rather than producing a reference that no
        // lookup could ever match.
        Assert.Equal(booking.Reference, reloaded.Reference);
        Assert.Equal(start, reloaded.Interval.StartUtc);
        Assert.Equal(start.AddHours(1), reloaded.Interval.EndUtc);
        Assert.Equal("UTC", reloaded.Interval.TimeZoneId);
        Assert.Equal(BookingStatus.Confirmed, reloaded.Status);
        Assert.Equal(booking.CreatedUtc, reloaded.CreatedUtc);
        Assert.Null(reloaded.Booker.MemberKey);
        var contact = reloaded.Booker.Contact;
        Assert.NotNull(contact);
        Assert.Equal("Integration Tester", contact.Name);
        Assert.Equal("integration@example.com", contact.Email);
        Assert.Equal("01234 567890", contact.Phone);
        Assert.Null(reloaded.Booker.ErasedUtc);
        Assert.False(reloaded.Booker.IsErased);
        Assert.Equal(resourceId, Assert.Single(reloaded.Claims).ResourceId);
    }

    [Fact]
    public async Task Unknown_ids_return_null()
    {
        fixture.EnsureAvailable();

        await using var context = fixture.CreateContext();
        Assert.Null(await new SqlResourceStore(context, new SqlSiteClosureStore(context)).GetAsync(Guid.NewGuid(), Ct));
        Assert.Null(await new SqlBookingStore(context).GetBookingAsync(Guid.NewGuid(), Ct));
    }
}
