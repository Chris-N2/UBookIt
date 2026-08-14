using UBookIt.Core.Availability;
using UBookIt.Core.Resources;
using UBookIt.Persistence.Entities;

namespace UBookIt.Persistence.Stores;

/// <summary>
/// Maps between the domain <see cref="Resource"/> aggregate and its rows.
/// Domain assembly goes through the Core factories so stored data re-passes
/// domain validation; corrupted rows surface as exceptions, never as
/// silently-wrong domain objects.
/// </summary>
internal static class ResourceRowMapper
{
    internal static Resource ToDomain(ResourceRow row)
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

        return Resource.Create(
            row.Type,
            row.DisplayName,
            row.Description,
            row.Capabilities.Select(c => (string?)c.Key),
            availability,
            row.Id).Value;
    }

    internal static ResourceRow ToRow(Resource resource)
    {
        var row = new ResourceRow
        {
            Id = resource.Id,
            Type = resource.Type,
            DisplayName = resource.DisplayName,
            Description = resource.Description,
            OpenHours = ToOpenHoursRows(resource),
            Exceptions = ToExceptionRows(resource),
            Capabilities = ToCapabilityRows(resource),
        };
        ApplyScalars(resource, row);
        return row;
    }

    /// <summary>Copies type, display, and constraint columns (not child rows) onto an existing row.</summary>
    internal static void ApplyScalars(Resource resource, ResourceRow row)
    {
        row.Type = resource.Type;
        row.DisplayName = resource.DisplayName;
        row.Description = resource.Description;

        var constraints = resource.Availability.Constraints;
        row.GranularityMinutes = (int)constraints.Granularity.TotalMinutes;
        row.MinDurationMinutes = (int)constraints.MinDuration.TotalMinutes;
        row.MaxDurationMinutes = (int)constraints.MaxDuration.TotalMinutes;
        row.LeadTimeMinutes = (int)constraints.LeadTime.TotalMinutes;
        row.HorizonDays = constraints.HorizonDays;
    }

    internal static List<OpenHoursRow> ToOpenHoursRows(Resource resource)
        => Enum.GetValues<DayOfWeek>()
            .SelectMany(day => resource.Availability.OpenHours.WindowsFor(day)
                .Select(window => new OpenHoursRow
                {
                    ResourceId = resource.Id,
                    DayOfWeek = (int)day,
                    StartTime = window.Start,
                    EndTime = window.End,
                }))
            .ToList();

    internal static List<ResourceCapabilityRow> ToCapabilityRows(Resource resource)
        => resource.Capabilities.Keys
            .Select(key => new ResourceCapabilityRow { ResourceId = resource.Id, Key = key })
            .ToList();

    internal static List<ExceptionRow> ToExceptionRows(Resource resource)
        => resource.Availability.Exceptions
            .SelectMany(exception => exception.IsClosure
                ? [new ExceptionRow { ResourceId = resource.Id, Date = exception.Date }]
                : exception.Windows.Select(window => new ExceptionRow
                {
                    ResourceId = resource.Id,
                    Date = exception.Date,
                    StartTime = window.Start,
                    EndTime = window.End,
                }))
            .ToList();
}
