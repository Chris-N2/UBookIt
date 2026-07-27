using UBookIt.Core.Bookings;
using UBookIt.Persistence.Entities;

namespace UBookIt.Tests.Integration.Support;

internal static class Seed
{
    /// <summary>A room open every day 08:00–18:00 (UTC site zone), default-ish constraints.</summary>
    public static async Task<Guid> EveryDayRoomAsync(SqlServerFixture fixture, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var row = new ResourceRow
        {
            Id = id,
            Type = "room",
            DisplayName = $"Room {id:N}",
            GranularityMinutes = 15,
            MinDurationMinutes = 30,
            MaxDurationMinutes = 480,
            LeadTimeMinutes = 0,
            HorizonDays = 90,
            OpenHours = Enum.GetValues<DayOfWeek>()
                .Select(day => new OpenHoursRow
                {
                    DayOfWeek = (int)day,
                    StartTime = new TimeOnly(8, 0),
                    EndTime = new TimeOnly(18, 0),
                })
                .ToList(),
        };

        await using var context = fixture.CreateContext();
        context.Resources.Add(row);
        await context.SaveChangesAsync(cancellationToken);
        return id;
    }

    /// <summary>A confirmed single-claim booking aggregate built via the public rehydration surface.</summary>
    public static Booking ConfirmedBooking(
        Guid resourceId, DateTimeOffset startUtc, TimeSpan duration, BookingStatus status = BookingStatus.Confirmed)
        => Booking.Rehydrate(
            Guid.NewGuid(),
            BookingInterval.Create(startUtc, startUtc + duration, "UTC").Value,
            Booker.Create(null, "Integration Tester", "integration@example.com", "01234 567890").Value,
            [new ResourceClaim(resourceId)],
            status,
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)).Value;
}
