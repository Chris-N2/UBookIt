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
/// A service configuration to report on: its roles, and the duration they would
/// be booked for. Deliberately not a <see cref="ServiceRequestModel"/> — this
/// describes a service being <em>edited</em>, which need not be a valid service
/// and in particular need not have a name (design D2).
/// </summary>
public class ServicePreviewRequestModel
{
    /// <summary>
    /// The roles to report on, in the order they appear on screen. Duplicated
    /// resource types are accepted here even though saving such a service is
    /// rejected: previewing one is how an editor sees what each role resolves to
    /// while correcting it.
    /// </summary>
    public List<ServicePreviewRoleModel> Roles { get; set; } = [];

    public ServiceDurationModel? Duration { get; set; }
}

/// <summary>One role of a configuration being previewed.</summary>
public class ServicePreviewRoleModel
{
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>Omitted or empty matches every resource of the type.</summary>
    public List<string> RequiredCapabilities { get; set; } = [];

    /// <summary>
    /// How many <em>distinct</em> resources this role needs at once. Defaults to
    /// 1, which is both the value every configuration had before counts existed
    /// and what a caller omitting it means.
    /// <para>
    /// It plays no part in the resolution <b>chains</b> — a role of count 3 draws
    /// on exactly the pool a role of count 1 does — and it is carried because the
    /// sufficiency finding beside them is an assignment question, which cannot be
    /// asked without knowing how many of each role are needed.
    /// </para>
    /// </summary>
    public int Count { get; set; } = 1;
}

/// <summary>
/// What a configuration resolves to: one chain per role, and — only when there
/// is one — a pair of roles whose start times can never coincide.
/// <para>
/// The chains describe configuration only. They say which resources are
/// <em>able</em> to provide the service, never that the service is available:
/// opening hours, lead time, booking horizon and existing bookings are not
/// evaluated in them (⑧a design D5). That boundary is unchanged by the
/// misalignment finding, which is why the finding sits beside the chains as a
/// member of its own rather than inside one.
/// </para>
/// <para>
/// Nothing here reports that a service <em>is</em> bookable. The chains stop at
/// eligibility, and the finding only ever reports impossibility.
/// </para>
/// </summary>
public class ServicePreviewResponseModel
{
    /// <summary>
    /// One chain per role, in the order the roles were supplied.
    /// <para>
    /// Never one combined chain: the roles constrain different pools — every
    /// role names a distinct resource type in a saveable service — so a merged
    /// count would describe no filter that resolution applies. Because the pools
    /// are disjoint, each chain is independently true: no role can consume a
    /// resource another role's chain counted (design D5).
    /// </para>
    /// </summary>
    public List<ServiceRoleChainModel> Roles { get; set; } = [];

    /// <summary>
    /// Two roles whose start times can never coincide, when the configuration has
    /// such a pair — otherwise absent.
    /// <para>
    /// A member of its own, never folded into a role's chain (design D6). The
    /// chains report type, capabilities and duration and say nothing about
    /// opening hours; a stage that mentioned opening times would be the chain
    /// claiming something about availability. Misalignment is also a property of
    /// a <em>pair</em> of roles and belongs to neither of them.
    /// </para>
    /// <para>
    /// Present only when the roles are permanently misaligned. Alignment is never
    /// reported positively: sharing a start grid is necessary for a bookable
    /// start and nowhere near sufficient, so saying so would be read as a promise
    /// the endpoint cannot make.
    /// </para>
    /// </summary>
    public StartMisalignmentModel? StartMisalignment { get; set; }

    /// <summary>
    /// The roles that cannot be filled <em>at once</em> by the resources that
    /// exist, when the configuration has such a group — otherwise absent.
    /// <para>
    /// A member of its own, never folded into a chain. The finding belongs to a
    /// <b>set</b> of roles and to none of them individually: each chain is
    /// independently true of the role it describes, and one reporting "2 eligible"
    /// is not wrong merely because another role competes for the same two. Folding
    /// it in would also make a chain assert something no filter of resolution
    /// applies.
    /// </para>
    /// <para>
    /// Absent when the roles can be filled together — never a positive statement
    /// of sufficiency, and never a shortfall of zero. The check is one-directional
    /// and the endpoint adds no claim Core declines to make: a sufficient pool
    /// says nothing about opening hours, lead time, horizon, or the booking
    /// calendar, so reporting it would read as a promise about availability.
    /// </para>
    /// </summary>
    public PoolShortfallModel? PoolShortfall { get; set; }
}

/// <summary>
/// A reported shortfall: which roles cannot be filled together, how many distinct
/// resources they need between them, and how many are eligible for any of them.
/// <para>
/// Both numbers travel, because they distinguish the two repairs. A count that is
/// too high is corrected on the requirement row; a pool that is too small is
/// corrected by adding or re-configuring a resource — and a reader cannot tell
/// which without both.
/// </para>
/// </summary>
public class PoolShortfallModel
{
    /// <summary>
    /// The roles involved, in the order the request supplied them, so a reader can
    /// point at the rows on screen.
    /// </summary>
    public List<ShortfallRoleModel> Roles { get; set; } = [];

    /// <summary>How many distinct resources those roles need between them.</summary>
    public int Required { get; set; }

    /// <summary>
    /// How many resources are eligible for any of them. Always less than
    /// <see cref="Required"/> — this model is only ever present for a genuine
    /// shortfall.
    /// </summary>
    public int Eligible { get; set; }
}

/// <summary>
/// One role a shortfall names. Carries what <em>distinguishes</em> it as well as
/// its type: two roles of one resource type are legal exactly when their required
/// capabilities differ, so a finding naming two "therapist" rows and nothing else
/// would leave a reader unable to tell which row is which.
/// </summary>
public class ShortfallRoleModel
{
    /// <summary>
    /// Where this role sits in the request's role list, zero-based.
    /// <para>
    /// Carried because nothing else identifies it. Two roles of one resource type
    /// requiring the same capabilities are equal in every field below — a
    /// configuration the domain rejects, but one an editor is halfway through —
    /// and a consumer rendering them would otherwise emit two identical entries
    /// for two different rows. Position is what a consumer with rows on screen
    /// points at; one without them can ignore it.
    /// </para>
    /// </summary>
    public int RoleIndex { get; set; }

    /// <summary>The role, identified as the chains identify one: by resource type.</summary>
    public string ResourceType { get; set; } = string.Empty;

    public List<string> RequiredCapabilities { get; set; } = [];

    public int Count { get; set; }
}

/// <summary>
/// A reported clash: the two roles that can never share a start, each with the
/// resource and the two numbers responsible.
/// </summary>
public class StartMisalignmentModel
{
    public MisalignedRoleModel First { get; set; } = new();

    public MisalignedRoleModel Second { get; set; } = new();
}

/// <summary>
/// One side of a clash. Carries the window start and granularity because the fix
/// is to edit a <b>resource</b> — its opening time or its step size — and a
/// report naming only the service would send an editor to the wrong screen.
/// </summary>
public class MisalignedRoleModel
{
    /// <summary>The role, identified as the chains identify one: by resource type.</summary>
    public string ResourceType { get; set; } = string.Empty;

    public Guid ResourceId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The wall-clock start of the window whose grid cannot meet the other's, as
    /// <c>HH:mm</c>. A local time of day rather than an instant: the claim is
    /// about a weekly opening time, and an instant would invite a consumer to
    /// read it as a date.
    /// </summary>
    public string WindowStart { get; set; } = string.Empty;

    /// <summary>The resource's granularity, in whole minutes.</summary>
    public int GranularityMinutes { get; set; }
}

/// <summary>
/// The resolution chain for one role: three successive filters, each a subset
/// of the one before it, plus what the last of them excluded — and the role the
/// chain describes.
/// <para>
/// Reported as a chain rather than a surviving count because a single number
/// cannot distinguish a mistyped resource type from an over-narrow capability
/// set from a duration no resource can provide — three faults corrected in three
/// different places.
/// </para>
/// </summary>
public class ServiceRoleChainModel
{
    /// <summary>
    /// The role this chain is about, echoed so the report can be phrased from
    /// the answer rather than from a form that may already have moved on.
    /// </summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// The capabilities this role required. Empty means the role constrains by
    /// type alone, which a consumer must not describe as a capability filter.
    /// </summary>
    public List<string> RequiredCapabilities { get; set; } = [];

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
