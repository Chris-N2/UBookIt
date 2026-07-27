using Microsoft.EntityFrameworkCore;
using UBookIt.Core.Availability;
using UBookIt.Core.Resources;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Entities;

namespace UBookIt.Persistence.Stores;

/// <summary>
/// SQL Server implementation of <see cref="IResourceStore"/>. Read-only in
/// this change; write surface arrives with the management API change.
/// </summary>
internal sealed class SqlResourceStore(UBookItDbContext db) : IResourceStore
{
    public async Task<Resource?> GetAsync(Guid resourceId, CancellationToken cancellationToken = default)
    {
        var row = await db.Resources
            .AsNoTracking()
            .Include(r => r.OpenHours)
            .Include(r => r.Exceptions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == resourceId, cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : ToDomain(row);
    }

    /// <summary>
    /// Assembles the domain resource through the Core factories so stored data
    /// re-passes domain validation; corrupted rows surface as exceptions, not
    /// silently-wrong domain objects.
    /// </summary>
    private static Resource ToDomain(ResourceRow row)
    {
        var openHours = WeeklyOpenHours.Create(
            row.OpenHours.Select(w =>
                ((DayOfWeek)w.DayOfWeek, DayWindow.Create(w.StartTime, w.EndTime).Value))).Value;

        var exceptions = row.Exceptions
            .GroupBy(e => e.Date)
            .Select(group => group.All(e => e.StartTime is null)
                ? DateException.Closure(group.Key)
                : DateException.Override(
                    group.Key,
                    group.Select(e => DayWindow.Create(e.StartTime!.Value, e.EndTime!.Value).Value)).Value);

        var constraints = BookingConstraints.Create(
            granularity: TimeSpan.FromMinutes(row.GranularityMinutes),
            minDuration: TimeSpan.FromMinutes(row.MinDurationMinutes),
            maxDuration: TimeSpan.FromMinutes(row.MaxDurationMinutes),
            leadTime: TimeSpan.FromMinutes(row.LeadTimeMinutes),
            horizonDays: row.HorizonDays).Value;

        var availability = AvailabilityConfiguration.Create(openHours, exceptions, constraints).Value;

        return Resource.Create(row.Type, row.DisplayName, row.Description, availability, row.Id).Value;
    }
}
