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

    /// <summary>
    /// The site closures this resource is exempt from, by closure id. Omitted or empty
    /// means it inherits every closure the site has.
    /// </summary>
    /// <remarks>
    /// A full update replaces the set rather than merging into it, like every other part
    /// of this model — so an update that omits an id withdraws that exemption. An id no
    /// closure carries is refused with <c>closure-not-found</c> rather than ignored: an
    /// exemption silently dropped would leave a resource closed on a date its editor
    /// believed they had opened.
    /// </remarks>
    public List<Guid> ClosureOptOuts { get; set; } = [];

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

    /// <summary>
    /// Every site closure, with whether this resource is exempt from each. Read-only: the
    /// resource writes only <see cref="ResourceRequestModel.ClosureOptOuts"/>.
    /// </summary>
    public List<ResourceClosureModel> Closures { get; set; } = [];

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

    /// <summary>
    /// Whether a site closure is currently overriding this exception, so that it has no
    /// effect on the date. Computed by the server; <c>null</c> when the server has not
    /// stated it, which is every request — this member is read-only.
    /// </summary>
    /// <remarks>
    /// <b>True only where the outcome actually differs.</b> An exception that is itself a
    /// closure on a closure date closes the date either way, so reporting it as superseded
    /// would describe a difference that does not exist. The determination is made once, by
    /// the server, so that a client cannot derive a second answer to a precedence question
    /// the booking path has already answered.
    /// <para>
    /// <b>Nullable so that a request need not carry it.</b> This model is shared by the
    /// request and the response — a non-nullable member would be <c>required</c> in the
    /// generated client, obliging an editor to send a value the server ignores, which is a
    /// control that looks live and is not. Every response populates it, true or false; a
    /// value sent on a request is discarded.
    /// </para>
    /// </remarks>
    public bool? Superseded { get; set; }
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

/// <summary>
/// A site closure as a resource's editor sees it: the site's date and label, plus whether
/// this resource is exempt from it.
/// </summary>
/// <remarks>
/// <b>Read-only on the resource response.</b> The only closure state a resource writes is
/// its opt-out set (<see cref="ResourceRequestModel.ClosureOptOuts"/>); the date and the
/// label belong to the site and are changed through the closures endpoints.
/// </remarks>
public class ResourceClosureModel
{
    public Guid Id { get; set; }

    public DateOnly Date { get; set; }

    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Whether this resource is exempt from the closure. Always present, so a reader never
    /// has to infer it from absence.
    /// </summary>
    public bool Excluded { get; set; }
}

/// <summary>One site closure, as the closures screen sees it.</summary>
public class SiteClosureModel
{
    public Guid Id { get; set; }

    public DateOnly Date { get; set; }

    public string Label { get; set; } = string.Empty;
}

/// <summary>A closure being created or edited.</summary>
public class SiteClosureRequestModel
{
    public DateOnly Date { get; set; }

    /// <summary>
    /// Required. A closure is offered for opt-out where a resource is edited, and a bare
    /// date asks an operator to exempt something they cannot identify.
    /// </summary>
    public string Label { get; set; } = string.Empty;
}
