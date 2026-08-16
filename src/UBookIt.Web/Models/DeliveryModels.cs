using System.ComponentModel.DataAnnotations;

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

    /// <summary>
    /// What this resource can do, deterministically ordered; empty when it
    /// carries none.
    /// <para>
    /// Published deliberately. Capabilities are an input to service eligibility,
    /// and publishing them keeps a service's candidate pool computable from
    /// public reads — which is what keeps the <c>resource-not-eligible</c>
    /// failure from disclosing anything a consumer could not already derive.
    /// Capability keys are visible to anonymous callers and must not be used to
    /// record anything not intended to be public.
    /// </para>
    /// </summary>
    public List<string> Capabilities { get; set; } = [];

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
/// A start with the range of lengths bookable from it. Every whole multiple of
/// the resource's granularity between the two bounds may be booked.
/// </summary>
public sealed class BookableStartModel
{
    public DateTimeOffset StartUtc { get; set; }

    public int MinDurationMinutes { get; set; }

    public int MaxDurationMinutes { get; set; }
}

/// <summary>
/// Every bookable start over the queried range. Carries no requested duration:
/// the response answers how long may be booked from each start, so a client
/// filters it locally for whichever length it needs.
/// </summary>
public sealed class BookableStartsResponseModel
{
    public Guid ResourceId { get; set; }

    public string ZoneId { get; set; } = string.Empty;

    public List<BookableStartModel> Starts { get; set; } = [];
}

/// <summary>
/// A service's duration on the wire. The kind is stated explicitly rather than
/// left to be inferred from which bounds are present, so a consumer never has to
/// re-derive the rule from nullable fields.
/// </summary>
public sealed class ServiceDurationModel
{
    /// <summary>Either <c>fixed</c> or <c>variable</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>The fixed length, when the kind is <c>fixed</c>; otherwise null.</summary>
    public int? DurationMinutes { get; set; }

    /// <summary>The lower bound when variable and bounded; null means each resource's own minimum applies.</summary>
    public int? MinDurationMinutes { get; set; }

    /// <summary>The upper bound when variable and bounded; null means each resource's own maximum applies.</summary>
    public int? MaxDurationMinutes { get; set; }
}

public sealed class ServiceReadModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ServiceDurationModel Duration { get; set; } = new();

    /// <summary>
    /// Every role this service requires, in a deterministic order. A booking
    /// claims one resource per role.
    /// <para>
    /// A collection even for a single-role service, so a consumer written
    /// against this contract needs no change when a service gains a role.
    /// </para>
    /// </summary>
    public List<ServiceRoleReadModel> Roles { get; set; } = [];
}

/// <summary>
/// One role of a service: the resource type it resolves against and the
/// capabilities a resource must carry to fill it.
/// <para>
/// Publishing these alongside each resource's own capabilities is what makes a
/// service's candidate pools computable by an anonymous consumer — which is what
/// keeps the <c>resource-not-eligible</c> failure free of any disclosure the
/// public reads do not already make.
/// </para>
/// </summary>
public sealed class ServiceRoleReadModel
{
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// Deterministically ordered; an empty collection — never null or absent —
    /// when the role constrains by type alone.
    /// </summary>
    public List<string> RequiredCapabilities { get; set; } = [];

    /// <summary>
    /// How many <b>distinct</b> resources this role consumes at once.
    /// <para>
    /// Published because eligibility alone does not say what a service requires: a
    /// consumer can compute this role's candidate pool exactly and still present
    /// "one therapist" for a service that needs two. Count is not an input to
    /// eligibility — a role of count 3 draws on the same pool a role of count 1
    /// does — which is why publishing it does not widen the disclosure the
    /// <c>resource-not-eligible</c> failure rests on.
    /// </para>
    /// <para>
    /// Always present, carrying 1 for a role needing a single resource, so a
    /// consumer never has to treat its absence as a default.
    /// </para>
    /// </summary>
    public int Count { get; set; } = 1;
}

public sealed class PagedServicesModel
{
    public int Total { get; set; }

    public List<ServiceReadModel> Items { get; set; } = [];
}

/// <summary>
/// An arithmetic run of bookable lengths: every whole multiple of
/// <c>stepMinutes</c> from <c>minDurationMinutes</c> to <c>maxDurationMinutes</c>
/// inclusive. One contributing resource produces one run.
/// </summary>
public sealed class LengthRunModel
{
    public int MinDurationMinutes { get; set; }

    public int MaxDurationMinutes { get; set; }

    public int StepMinutes { get; set; }
}

/// <summary>
/// A start at which a service can be booked, with the lengths available there.
/// The lengths are a list of runs rather than a single minimum/maximum pair:
/// resources backing a service differ in granularity and minimum, so the lengths
/// on offer are in general neither contiguous nor on one grid, and a collapsed
/// pair would advertise lengths no resource can book. No resource is named — the
/// resource is resolved at placement time.
/// </summary>
public sealed class ServiceBookableStartModel
{
    public DateTimeOffset StartUtc { get; set; }

    public List<LengthRunModel> Runs { get; set; } = [];
}

public sealed class ServiceBookableStartsResponseModel
{
    public Guid ServiceId { get; set; }

    public string ZoneId { get; set; } = string.Empty;

    public List<ServiceBookableStartModel> Starts { get; set; } = [];
}

/// <summary>
/// A request to book a service. The service comes from the route. There is no
/// resource id: the resource is resolved from the service's eligible pool.
/// <c>DurationMinutes</c> is required for every service, including a
/// fixed-duration one — a length is never inferred or substituted.
/// </summary>
public sealed class ServicePlacementRequestModel
{
    public DateTimeOffset Start { get; set; }

    /// <summary>
    /// Required, and nullable so an omitted length is a validation failure
    /// naming the field rather than a silent zero that gets reported as some
    /// unrelated duration error.
    /// </summary>
    [Required]
    public int? DurationMinutes { get; set; }

    /// <summary>
    /// Optionally ask for a particular eligible resource. It is attempted first
    /// and falls through when unavailable; naming a resource that cannot fulfil
    /// the service is rejected rather than ignored.
    /// </summary>
    public Guid? PreferredResourceId { get; set; }

    public BookerModel Booker { get; set; } = new();
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

/// <summary>
/// The response to a direct placement, which claims exactly one resource and
/// reports it as it always has. Service placement has its own response model:
/// see <see cref="ServicePlacementResponseModel"/>.
/// </summary>
public sealed class PlacementResponseModel
{
    public Guid BookingId { get; set; }

    public string Status { get; set; } = string.Empty;

    public Guid ResourceId { get; set; }

    public IntervalModel Interval { get; set; } = new();

    public BookerModel Booker { get; set; } = new();
}

/// <summary>
/// The response to a service placement. A service claims one resource per role,
/// so the resources it resolved to are a collection: a single-valued member
/// could only report one of them or none.
/// </summary>
public sealed class ServicePlacementResponseModel
{
    public Guid BookingId { get; set; }

    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Every resource the service resolved to, one per role, in a deterministic
    /// order. Present and of length one for a single-role service.
    /// </summary>
    public List<ResolvedResourceModel> Resources { get; set; } = [];

    public IntervalModel Interval { get; set; } = new();

    public BookerModel Booker { get; set; } = new();
}

/// <summary>One resource a service placement resolved to.</summary>
public sealed class ResolvedResourceModel
{
    public Guid ResourceId { get; set; }
}

/// <summary>One failed rule, using the domain's stable codes.</summary>
public sealed class ApiErrorModel
{
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? Field { get; set; }
}
