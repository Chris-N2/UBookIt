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
    /// <summary>
    /// Builds the domain aggregate, applying the site closures this resource has not
    /// opted out of.
    /// </summary>
    /// <param name="row">The resource row, with its child collections loaded.</param>
    /// <param name="siteClosures">
    /// Every closure the site has. Passed in rather than read here so that a listing
    /// costs one closure query for the whole batch instead of one per resource — the
    /// N+1 a candidate pool would otherwise make of a single availability question.
    /// </param>
    /// <remarks>
    /// <b>Closures are applied to the availability configuration, never to the
    /// exception rows.</b> <see cref="ToExceptionRows"/> is what the write path
    /// persists; a closure that arrived in that collection would be written back as an
    /// exception this resource owns, outliving the closure itself.
    /// </remarks>
    internal static Resource ToDomain(ResourceRow row, IReadOnlyList<SiteClosure> siteClosures)
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

        var optOuts = row.ClosureOptOuts.Select(o => o.ClosureId).ToHashSet();

        // Filtered here, so the configuration carries only closures that actually close
        // this resource's dates. An opted-out resource is then indistinguishable from one
        // on a site that never had the closure — which is exactly what opting out means.
        var applicableClosures = siteClosures.Where(c => !optOuts.Contains(c.Id));

        var availability = AvailabilityConfiguration
            .Create(openHours, exceptions, constraints, applicableClosures).Value;

        // Named rather than positional, deliberately. This call passed `row.Id`
        // as the sixth argument; adding one before it shifted the meaning of
        // every argument after the third. The compiler caught it because the
        // types differed, which is luck rather than design — naming them means
        // the next parameter cannot silently land somewhere else.
        return Resource.Create(
            type: row.Type,
            displayName: row.DisplayName,
            description: row.Description,
            capabilities: row.Capabilities.Select(c => (string?)c.Key),
            availability: availability,
            directlyBookable: row.DirectlyBookable,
            id: row.Id,
            closureOptOuts: optOuts).Value;
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
            ClosureOptOuts = ToClosureOptOutRows(resource),
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

        // Copied on every write, so a full update can withdraw the permission as
        // well as grant it — the same full-replacement semantics the capability
        // set has, rather than a value that can only ever be turned on.
        row.DirectlyBookable = resource.DirectlyBookable;
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

    /// <summary>
    /// The resource's closure exemptions. Written with its other availability rows; the
    /// closures themselves are the site's and are never written from here.
    /// </summary>
    internal static List<ResourceClosureOptOutRow> ToClosureOptOutRows(Resource resource)
        => resource.ClosureOptOuts
            .Select(closureId => new ResourceClosureOptOutRow
            {
                ResourceId = resource.Id,
                ClosureId = closureId,
            })
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
