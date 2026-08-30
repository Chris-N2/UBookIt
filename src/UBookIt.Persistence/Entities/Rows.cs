using UBookIt.Core.Services;

namespace UBookIt.Persistence.Entities;

/// <summary>Table: uBookItResource. Constraint values stored as integer minutes/days.</summary>
internal sealed class ResourceRow
{
    public Guid Id { get; set; }

    public required string Type { get; set; }

    public required string DisplayName { get; set; }

    public string? Description { get; set; }

    public int GranularityMinutes { get; set; }

    public int MinDurationMinutes { get; set; }

    public int MaxDurationMinutes { get; set; }

    public int LeadTimeMinutes { get; set; }

    public int HorizonDays { get; set; }

    /// <summary>
    /// Whether the resource may be booked on its own. Non-nullable with a default
    /// of false, so every existing row takes the withheld answer — which is the
    /// behaviour change this change exists to make, applied by the migration
    /// rather than by a backfill.
    /// </summary>
    public bool DirectlyBookable { get; set; }

    public List<OpenHoursRow> OpenHours { get; set; } = [];

    public List<ExceptionRow> Exceptions { get; set; } = [];

    public List<ResourceCapabilityRow> Capabilities { get; set; } = [];
}

/// <summary>
/// Table: uBookItResourceCapability. One row per capability a resource carries.
/// Keyed by (resource, key) so a duplicate is impossible in storage and not only
/// in <c>CapabilitySet</c>.
/// </summary>
internal sealed class ResourceCapabilityRow
{
    public Guid ResourceId { get; set; }

    public required string Key { get; set; }
}

/// <summary>Table: uBookItResourceOpenHours. One row per weekly window.</summary>
internal sealed class OpenHoursRow
{
    public long Id { get; set; }

    public Guid ResourceId { get; set; }

    public int DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }
}

/// <summary>
/// Table: uBookItResourceException. One row per exception window; a closure is
/// a single row for the date with NULL start and end times.
/// </summary>
internal sealed class ExceptionRow
{
    public long Id { get; set; }

    public Guid ResourceId { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }
}

/// <summary>Table: uBookItBooking.</summary>
internal sealed class BookingRow
{
    public Guid Id { get; set; }

    public DateTimeOffset StartUtc { get; set; }

    public DateTimeOffset EndUtc { get; set; }

    public required string TimeZoneId { get; set; }

    public int Status { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public Guid? MemberKey { get; set; }

    public required string BookerName { get; set; }

    public required string BookerEmail { get; set; }

    public string? BookerPhone { get; set; }

    /// <summary>
    /// The service this booking was placed for, or NULL for one placed directly.
    /// </summary>
    /// <remarks>
    /// <b>No foreign key, deliberately.</b> A booking is a historical fact and must survive
    /// its service being renamed, retired or deleted. A nullable FK with
    /// <c>ON DELETE SET NULL</c> would convert "placed for a service that no longer exists"
    /// into "placed directly", and that is the one meaning NULL has to keep.
    /// </remarks>
    public Guid? ServiceId { get; set; }

    /// <summary>
    /// The service's name as it stood at placement — a snapshot, not a reference.
    /// </summary>
    /// <remarks>
    /// Stored rather than joined so a row stays answerable after the service is renamed or
    /// deleted. A join would report the name the service has now, or none at all.
    /// NULL exactly when <see cref="ServiceId"/> is NULL.
    /// </remarks>
    public string? ServiceName { get; set; }

    public List<ClaimRow> Claims { get; set; } = [];
}

/// <summary>Table: uBookItResourceClaim. Unique per (BookingId, ResourceId).</summary>
internal sealed class ClaimRow
{
    public long Id { get; set; }

    public Guid BookingId { get; set; }

    public Guid ResourceId { get; set; }
}

/// <summary>
/// Table: uBookItService. The duration kind is stored as its name; bounds are
/// integer minutes. A fixed duration stores the same value in both bounds; a
/// variable one may leave either NULL, meaning the resource's own bound applies.
/// </summary>
internal sealed class ServiceRow
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public ServiceDurationKind DurationKind { get; set; }

    public int? MinDurationMinutes { get; set; }

    public int? MaxDurationMinutes { get; set; }

    public List<ServiceRoleRow> Roles { get; set; } = [];
}

/// <summary>Table: uBookItServiceRole. One row per required role (v1: exactly one per service).</summary>
internal sealed class ServiceRoleRow
{
    public long Id { get; set; }

    public Guid ServiceId { get; set; }

    public required string ResourceType { get; set; }

    public int Count { get; set; }

    /// <summary>
    /// Whether a visitor may choose which resource fills this role. A
    /// non-nullable <c>bit</c> defaulting to <c>false</c>, so every role stored
    /// before the column existed loads as not selectable and no service changes
    /// behaviour — the shape <c>AddDirectBookability</c> established.
    /// </summary>
    public bool VisitorSelectable { get; set; }

    public List<ServiceRoleCapabilityRow> Capabilities { get; set; } = [];
}

/// <summary>
/// Table: uBookItServiceRoleCapability. One row per capability a service role
/// requires. Keyed by (role, key), as for resource capabilities.
/// </summary>
internal sealed class ServiceRoleCapabilityRow
{
    public long ServiceRoleId { get; set; }

    public required string Key { get; set; }
}
