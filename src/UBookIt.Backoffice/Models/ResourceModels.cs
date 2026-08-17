using System.Text.Json.Serialization;

namespace UBookIt.Backoffice.Models;

/// <summary>
/// Management API contract models. Purpose-built DTOs — domain types never
/// appear in the HTTP contract (design D5). This contract is a compatibility
/// surface for alternative UI implementations.
/// </summary>
public class ResourceRequestModel
{
    public string Type { get; set; } = "room";

    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<OpeningHoursModel> OpeningHours { get; set; } = [];

    public List<AvailabilityExceptionModel> Exceptions { get; set; } = [];

    /// <summary>
    /// What this resource can do. Omitted or empty means it carries none. A full
    /// update replaces the set rather than merging into it, like every other
    /// part of this model.
    /// </summary>
    public List<string> Capabilities { get; set; } = [];

    /// <summary>
    /// Whether this resource may be booked on its own. Omitted means <b>no</b>,
    /// matching the domain default and the treatment of an omitted capability
    /// collection — a caller that says nothing is saying no.
    /// <para>
    /// A full update replaces it rather than merging, so an update that omits it
    /// withdraws the permission. That is the same full-replacement semantics as
    /// the rest of this model.
    /// </para>
    /// </summary>
    public bool DirectlyBookable { get; set; }

    /// <summary>Null applies the package defaults.</summary>
    public ConstraintsModel? Constraints { get; set; }
}

public class ResourceResponseModel
{
    public Guid Id { get; set; }

    public string Type { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<OpeningHoursModel> OpeningHours { get; set; } = [];

    public List<AvailabilityExceptionModel> Exceptions { get; set; } = [];

    public List<string> Capabilities { get; set; } = [];

    /// <summary>
    /// Whether this resource may be booked on its own. Always present, so a
    /// reader never has to infer it from absence.
    /// </summary>
    public bool DirectlyBookable { get; set; }

    public ConstraintsModel Constraints { get; set; } = new();
}

public class OpeningHoursModel
{
    /// <summary>
    /// Serialized as the day name ("Monday" … "Sunday"). The converter is
    /// applied at the property so the wire contract matches the swagger
    /// document regardless of the host's global JSON options.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DayOfWeek Day { get; set; }

    public TimeOnly Start { get; set; }

    public TimeOnly End { get; set; }
}

public class AvailabilityExceptionModel
{
    public DateOnly Date { get; set; }

    /// <summary>Empty means the date is closed; otherwise these windows replace the weekly pattern.</summary>
    public List<TimeWindowModel> Windows { get; set; } = [];
}

public class TimeWindowModel
{
    public TimeOnly Start { get; set; }

    public TimeOnly End { get; set; }
}

public class ConstraintsModel
{
    public int GranularityMinutes { get; set; }

    public int MinDurationMinutes { get; set; }

    public int MaxDurationMinutes { get; set; }

    public int LeadTimeMinutes { get; set; }

    public int HorizonDays { get; set; }
}

public class PagedResourcesModel
{
    public int Total { get; set; }

    public List<ResourceResponseModel> Items { get; set; } = [];
}

/// <summary>
/// A resource type key currently in use and how many resources have it.
/// Backs the backoffice type picker; the count lets the UI hint that a chosen
/// type matches no resources without a second request.
/// </summary>
public class ResourceTypeUsageModel
{
    public string Type { get; set; } = string.Empty;

    public int Count { get; set; }
}

/// <summary>
/// A capability key currently carried by resources and how many carry it.
/// Backs the backoffice capability pickers. Descriptive, not prescriptive: it
/// reports what is in use and never constrains what may be entered.
/// </summary>
public class CapabilityUsageModel
{
    public string Key { get; set; } = string.Empty;

    public int Count { get; set; }
}

/// <summary>One failed validation rule, using the domain's stable codes.</summary>
public class ApiErrorModel
{
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? Field { get; set; }
}
