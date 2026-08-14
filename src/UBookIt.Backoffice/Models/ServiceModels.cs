namespace UBookIt.Backoffice.Models;

/// <summary>
/// Management API contract models for services. Purpose-built DTOs — domain
/// types never appear in the HTTP contract. Durations are whole minutes,
/// matching the delivery-API convention.
/// </summary>
public class ServiceRequestModel
{
    public string Name { get; set; } = string.Empty;

    public ServiceDurationModel? Duration { get; set; }

    public List<ServiceRoleModel> Roles { get; set; } = [];
}

public class ServiceResponseModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ServiceDurationModel Duration { get; set; } = new();

    public List<ServiceRoleModel> Roles { get; set; } = [];
}

/// <summary>
/// A service's duration on the wire. The kind is explicit so the contract
/// cannot express a combination the domain has no meaning for: a fixed
/// duration carries <see cref="Minutes"/>, a variable one carries whichever
/// of <see cref="MinMinutes"/>/<see cref="MaxMinutes"/> were supplied, and a
/// null bound defers to the fulfilling resource's own bound.
/// </summary>
public class ServiceDurationModel
{
    public const string FixedKind = "fixed";

    public const string VariableKind = "variable";

    public string Kind { get; set; } = string.Empty;

    /// <summary>The length, when the kind is <c>fixed</c>.</summary>
    public int? Minutes { get; set; }

    /// <summary>The lower bound, when the kind is <c>variable</c>.</summary>
    public int? MinMinutes { get; set; }

    /// <summary>The upper bound, when the kind is <c>variable</c>.</summary>
    public int? MaxMinutes { get; set; }
}

public class ServiceRoleModel
{
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// What a resource must be able to do to fill this role. Omitted or empty
    /// constrains by type alone — the behaviour of every service defined before
    /// capabilities existed.
    /// </summary>
    public List<string> RequiredCapabilities { get; set; } = [];

    public int Count { get; set; } = 1;
}

public class PagedServicesModel
{
    public int Total { get; set; }

    public List<ServiceResponseModel> Items { get; set; } = [];
}

/// <summary>
/// A service configuration to report on: a role, and the duration it would be
/// booked for. Deliberately not a <see cref="ServiceRequestModel"/> — this
/// describes a service being <em>edited</em>, which need not be a valid service
/// and in particular need not have a name (design D2). A role count is not
/// carried either: it is fixed at 1 in v1 and plays no part in resolution.
/// </summary>
public class ServicePreviewRequestModel
{
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>Omitted or empty matches every resource of the type.</summary>
    public List<string> RequiredCapabilities { get; set; } = [];

    public ServiceDurationModel? Duration { get; set; }
}

/// <summary>
/// The resolution chain for a configuration: three successive filters, each a
/// subset of the one before it, plus what the last of them excluded.
/// <para>
/// Reported as a chain rather than a surviving count because a single number
/// cannot distinguish a mistyped resource type from an over-narrow capability
/// set from a duration no resource can provide — three faults corrected in three
/// different places.
/// </para>
/// <para>
/// It describes configuration only. <see cref="CanProvide"/> says which
/// resources are <em>able</em> to provide the service, never that the service is
/// available: opening hours, lead time, booking horizon and existing bookings
/// are not evaluated here (design D5).
/// </para>
/// </summary>
public class ServicePreviewResponseModel
{
    /// <summary>Every resource whose type key is the role's.</summary>
    public ServicePreviewStageModel OfType { get; set; } = new();

    /// <summary>Those of them carrying every required capability.</summary>
    public ServicePreviewStageModel WithCapabilities { get; set; } = new();

    /// <summary>Those of them whose own constraints admit a length the duration permits.</summary>
    public ServicePreviewStageModel CanProvide { get; set; } = new();

    /// <summary>
    /// The resources dropped between <see cref="WithCapabilities"/> and
    /// <see cref="CanProvide"/>, each with the bound that excluded it — the
    /// number an editor has to change.
    /// </summary>
    public List<DurationExclusionModel> DurationExclusions { get; set; } = [];
}

/// <summary>One stage of the chain: how many resources reached it, and which.</summary>
public class ServicePreviewStageModel
{
    public int Total { get; set; }

    public List<PreviewResourceModel> Items { get; set; } = [];
}

/// <summary>A resource as the preview identifies it. Not the full aggregate.</summary>
public class PreviewResourceModel
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>
/// A resource excluded at the duration stage, and the bound responsible.
/// The reason is a stable key, not prose, so a consumer can phrase it.
/// </summary>
public class DurationExclusionModel
{
    /// <summary>
    /// <c>resource-maximum</c> — the service's shortest permitted length exceeds
    /// this resource's maximum; <c>resource-minimum</c> — the service's longest
    /// permitted length is below this resource's minimum; <c>granularity</c> —
    /// the ranges overlap but no multiple of this resource's granularity lies in
    /// the overlap.
    /// </summary>
    public const string ResourceMaximumReason = "resource-maximum";

    public const string ResourceMinimumReason = "resource-minimum";

    public const string GranularityReason = "granularity";

    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    /// <summary>The bound named by <see cref="Reason"/>, in whole minutes.</summary>
    public int BoundMinutes { get; set; }
}
