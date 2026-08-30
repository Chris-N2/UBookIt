using UBookIt.Core.Bookings;
using UBookIt.Persistence.Entities;

namespace UBookIt.Tests.Integration.Support;

internal static class Seed
{
    /// <summary>A room open every day 08:00–18:00 (UTC site zone), default-ish constraints.</summary>
    public static async Task<Guid> EveryDayRoomAsync(
        SqlServerFixture fixture,
        CancellationToken cancellationToken = default,
        string type = "room",
        int granularityMinutes = 15,
        int minDurationMinutes = 30,
        int maxDurationMinutes = 480,
        Guid? id = null,
        string? displayName = null)
    {
        id ??= Guid.NewGuid();
        var row = new ResourceRow
        {
            Id = id.Value,
            Type = type,

            // The default name is derived from the id, which is fine for suites that only
            // need distinct names. A test asserting that a NAME was joined from the
            // resource row must pass its own: a name derivable from the id cannot tell a
            // real join from a store fabricating the name out of the id it already has.
            DisplayName = displayName ?? $"Room {id:N}",
            GranularityMinutes = granularityMinutes,
            MinDurationMinutes = minDurationMinutes,
            MaxDurationMinutes = maxDurationMinutes,
            LeadTimeMinutes = 0,
            HorizonDays = 90,

            // Offered for direct booking. The column default is the opposite, and
            // these fixtures stand for ordinary bookable resources — the suites
            // that use them place direct bookings to exercise storage behaviour,
            // not to exercise the permission. `ResourceManagementStoreTests`
            // covers the permission itself, both answers, explicitly.
            DirectlyBookable = true,
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
        return id.Value;
    }

    /// <summary>A confirmed single-claim booking aggregate built via the public rehydration surface.</summary>
    public static Booking ConfirmedBooking(
        Guid resourceId,
        DateTimeOffset startUtc,
        TimeSpan duration,
        BookingStatus status = BookingStatus.Confirmed,
        ServiceAttribution? service = null)
        => Booking.Rehydrate(
            Guid.NewGuid(),
            BookingInterval.Create(startUtc, startUtc + duration, "UTC").Value,
            Booker.Create(null, "Integration Tester", "integration@example.com", "01234 567890").Value,
            [new ResourceClaim(resourceId)],
            status,
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            service).Value;
}
