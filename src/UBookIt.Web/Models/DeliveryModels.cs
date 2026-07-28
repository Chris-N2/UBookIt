namespace UBookIt.Web.Models;

/// <summary>
/// Public delivery API contract models. Purpose-built DTOs — Core domain
/// aggregates never appear on the wire (delivery-api spec, "Versioned,
/// self-contained public contract"). Instants are ISO-8601 UTC; the display
/// zone is carried once per response as <c>ZoneId</c>; durations are whole
/// minutes.
/// </summary>
public sealed class ResourceReadModel
{
    public Guid Id { get; set; }

    public string Type { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ConstraintsModel Constraints { get; set; } = new();

    /// <summary>Site-wide IANA display zone the constraints are interpreted in.</summary>
    public string ZoneId { get; set; } = string.Empty;
}

public sealed class ConstraintsModel
{
    public int GranularityMinutes { get; set; }

    public int MinDurationMinutes { get; set; }

    public int MaxDurationMinutes { get; set; }

    public int LeadTimeMinutes { get; set; }

    public int HorizonDays { get; set; }
}

public sealed class PagedResourcesModel
{
    public int Total { get; set; }

    public List<ResourceReadModel> Items { get; set; } = [];
}

/// <summary>A half-open [StartUtc, EndUtc) interval of absolute time.</summary>
public sealed class IntervalModel
{
    public DateTimeOffset StartUtc { get; set; }

    public DateTimeOffset EndUtc { get; set; }
}

public sealed class FreeTimeResponseModel
{
    public Guid ResourceId { get; set; }

    public string ZoneId { get; set; } = string.Empty;

    public List<IntervalModel> Intervals { get; set; } = [];
}

public sealed class SlotModel
{
    public DateTimeOffset StartUtc { get; set; }

    public int DurationMinutes { get; set; }
}

public sealed class SlotsResponseModel
{
    public Guid ResourceId { get; set; }

    public string ZoneId { get; set; } = string.Empty;

    public int DurationMinutes { get; set; }

    public List<SlotModel> Slots { get; set; } = [];
}

/// <summary>
/// Booker contact details on the wire. There is deliberately no member-key
/// field: v1 placement is anonymous and body-only (delivery-api spec,
/// "Booking placement"). Used for both the placement request and the echoed
/// response.
/// </summary>
public sealed class BookerModel
{
    public string? Name { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }
}

public sealed class PlacementRequestModel
{
    public Guid ResourceId { get; set; }

    public DateTimeOffset Start { get; set; }

    public int DurationMinutes { get; set; }

    public BookerModel Booker { get; set; } = new();
}

public sealed class PlacementResponseModel
{
    public Guid BookingId { get; set; }

    public string Status { get; set; } = string.Empty;

    public Guid ResourceId { get; set; }

    public IntervalModel Interval { get; set; } = new();

    public BookerModel Booker { get; set; } = new();
}

/// <summary>One failed rule, using the domain's stable codes.</summary>
public sealed class ApiErrorModel
{
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? Field { get; set; }
}
