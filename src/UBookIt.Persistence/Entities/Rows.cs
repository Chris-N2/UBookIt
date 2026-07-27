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

    public List<OpenHoursRow> OpenHours { get; set; } = [];

    public List<ExceptionRow> Exceptions { get; set; } = [];
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

    public List<ClaimRow> Claims { get; set; } = [];
}

/// <summary>Table: uBookItResourceClaim. Unique per (BookingId, ResourceId).</summary>
internal sealed class ClaimRow
{
    public long Id { get; set; }

    public Guid BookingId { get; set; }

    public Guid ResourceId { get; set; }
}
