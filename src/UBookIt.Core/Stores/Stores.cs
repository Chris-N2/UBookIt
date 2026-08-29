using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;

namespace UBookIt.Core.Stores;

/// <summary>A claim as seen by availability queries: which resource, which booking, when, and its status.</summary>
public sealed record ClaimInfo(Guid ResourceId, Guid BookingId, BookingInterval Interval, BookingStatus Status);

/// <summary>Read access to resources. Implemented by UBookIt.Persistence.</summary>
public interface IResourceStore
{
    Task<Resource?> GetAsync(Guid resourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A page of resources plus the unpaged total, for public read/discovery.
    /// A read-only projection over the same aggregates as the management store,
    /// kept on the read port so anonymous callers never depend on the
    /// management (write) surface.
    /// </summary>
    Task<ResourcePage> ListAsync(int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every resource whose type key equals <paramref name="type"/>, with the
    /// same child state as <see cref="GetAsync"/> so each is complete enough to
    /// compute availability from.
    /// <para>
    /// Deliberately unpaged, and deliberately without paging parameters at all:
    /// its consumer is a service's candidate pool, and a truncated pool does not
    /// error — it reports unavailability that does not exist, or resolves to the
    /// wrong resource. The absence of the parameters is the guarantee
    /// (book-via-service design D2).
    /// </para>
    /// <para>
    /// Distinct from <see cref="IResourceManagementStore.ListTypesAsync"/>,
    /// which reports type keys with usage counts for a backoffice picker and
    /// stays on the management port.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Resource>> ListByTypeAsync(string type, CancellationToken cancellationToken = default);
}

/// <summary>One page of resources plus the unpaged total.</summary>
public sealed record ResourcePage(IReadOnlyList<Resource> Items, int Total);

/// <summary>A resource type key currently in use, with how many resources have it.</summary>
public sealed record ResourceTypeUsage(string Type, int Count);

/// <summary>A capability key currently carried by resources, with how many carry it.</summary>
public sealed record CapabilityUsage(string Key, int Count);

/// <summary>
/// Management writes for resources. Implemented by UBookIt.Persistence.
/// Accepts only <see cref="Resource"/> aggregates — which are constructible
/// solely via the validating Core factories — so the store persists only
/// validated state. Availability updates replace the resource's configuration
/// wholesale within one transaction (write-path exception-date uniqueness).
/// </summary>
public interface IResourceManagementStore
{
    Task<DomainResult<Resource>> CreateAsync(Resource resource, CancellationToken cancellationToken = default);

    /// <summary>Full update; fails with <see cref="FailureCodes.ResourceNotFound"/> for unknown ids.</summary>
    Task<DomainResult<Resource>> UpdateAsync(Resource resource, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fails with <see cref="FailureCodes.ResourceInUse"/> when the resource
    /// has any booking claims (including a claim placed concurrently with the
    /// delete), and <see cref="FailureCodes.ResourceNotFound"/> for unknown ids.
    /// </summary>
    Task<DomainResult> DeleteAsync(Guid resourceId, CancellationToken cancellationToken = default);

    Task<ResourcePage> ListAsync(int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// The distinct resource type keys currently in use with a count per type,
    /// ordered by type key. Backs the backoffice type picker so a service role's
    /// resource type is chosen rather than retyped. Kept on the management port
    /// (not the read port) because the only consumer is a backoffice endpoint.
    /// </summary>
    Task<IReadOnlyList<ResourceTypeUsage>> ListTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The distinct capability keys resources currently carry, each with how
    /// many carry it, ordered by key. Backs the backoffice capability picker.
    /// A key required by a service but carried by no resource does not appear —
    /// this reports what exists, not what is wanted.
    /// </summary>
    /// <remarks>
    /// This store deliberately carries no projection selecting resources BY a
    /// required-capability set. Matching resources to a role is an eligibility
    /// question, and eligibility has exactly one implementation — in
    /// <c>UBookIt.Core</c>, over resources the read port hydrates. A
    /// storage-layer projection applying the same rule would be a second
    /// implementation free to diverge in its predicate, its ordering, or its test
    /// doubles, and a backoffice answer that diverges from the booking path's is
    /// worse than no answer (design D4).
    /// </remarks>
    Task<IReadOnlyList<CapabilityUsage>> ListCapabilitiesAsync(CancellationToken cancellationToken = default);
}

/// <summary>One page of services plus the unpaged total.</summary>
public sealed record ServicePage(IReadOnlyList<Service> Items, int Total);

/// <summary>Read access to services. Implemented by UBookIt.Persistence.</summary>
public interface IServiceStore
{
    Task<Service?> GetAsync(Guid serviceId, CancellationToken cancellationToken = default);

    Task<ServicePage> ListAsync(int skip, int take, CancellationToken cancellationToken = default);
}

/// <summary>
/// Management writes for services. Implemented by UBookIt.Persistence. Accepts
/// only validated <see cref="Service"/> aggregates (constructible solely via the
/// Core factory), so the store persists only validated state. Updates replace
/// the service's roles wholesale within one transaction.
/// </summary>
public interface IServiceManagementStore
{
    Task<DomainResult<Service>> CreateAsync(Service service, CancellationToken cancellationToken = default);

    /// <summary>Full update; fails with <see cref="FailureCodes.ServiceNotFound"/> for unknown ids.</summary>
    Task<DomainResult<Service>> UpdateAsync(Service service, CancellationToken cancellationToken = default);

    /// <summary>Fails with <see cref="FailureCodes.ServiceNotFound"/> for unknown ids.</summary>
    Task<DomainResult> DeleteAsync(Guid serviceId, CancellationToken cancellationToken = default);

    Task<ServicePage> ListAsync(int skip, int take, CancellationToken cancellationToken = default);
}

/// <summary>
/// Booking storage. Implemented by UBookIt.Persistence.
/// </summary>
public interface IBookingStore
{
    /// <summary>
    /// All claims (any status) for the resource whose intervals overlap
    /// [fromUtc, toUtc). Callers filter by blocking status; the store does not
    /// interpret statuses.
    /// </summary>
    Task<IReadOnlyList<ClaimInfo>> GetClaimsAsync(
        Guid resourceId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// The same claims as <see cref="GetClaimsAsync(Guid, DateTimeOffset, DateTimeOffset, CancellationToken)"/>
    /// but for several resources at once: every claim of any status whose
    /// booking interval overlaps [fromUtc, toUtc) for any of
    /// <paramref name="resourceIds"/>. The result SHALL equal the union of the
    /// single-resource reads for those ids over the same range.
    /// <para>
    /// Exists so an availability query over a service's candidate pool costs one
    /// round trip rather than one per candidate (book-via-service design D5).
    /// </para>
    /// </summary>
    Task<IReadOnlyList<ClaimInfo>> GetClaimsAsync(
        IReadOnlyCollection<Guid> resourceIds, DateTimeOffset fromUtc, DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a validated booking, atomically with respect to conflict
    /// detection. Contract (bookings spec, "Atomic placement contract"):
    /// between the conflict check and the persistence of a new booking's
    /// claims, no other placement for an overlapping interval on the same
    /// resource may succeed. Under concurrent placement of conflicting
    /// requests, exactly one succeeds and the others fail with code
    /// <see cref="FailureCodes.Conflict"/>. Conflicts are evaluated against
    /// claims of blocking bookings only (status Requested or Confirmed) using
    /// half-open interval overlap.
    /// </summary>
    Task<DomainResult<Booking>> PlaceAsync(Booking booking, CancellationToken cancellationToken = default);

    Task<Booking?> GetBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);

    /// <summary>Persists a status change to an existing booking.</summary>
    Task UpdateAsync(Booking booking, CancellationToken cancellationToken = default);
}

/// <summary>One resource a booking claims, with the name a list row displays.</summary>
/// <remarks>
/// The name is carried deliberately. A claim records only a resource id, so a caller
/// given ids alone must read each resource separately — one lookup per claim, per row.
/// Removing the name from here does not simplify anything; it moves the cost to every
/// caller.
/// </remarks>
public sealed record BookedResource(Guid ResourceId, string DisplayName);

/// <summary>
/// A booking as a management list row: everything such a row displays, and nothing more.
/// </summary>
/// <remarks>
/// <para>
/// It is a summary and it is allowed to stay one. When a detail view is needed,
/// <see cref="IBookingStore.GetBookingAsync"/> already returns the whole
/// <see cref="Booking"/>. Growing this type to serve both is how a list query acquires
/// columns nobody renders.
/// </para>
/// <para>
/// It carries the booker's name and email because that is what a list row shows. Nothing
/// logs them, and fixtures use invented people — the standing rule, restated because this
/// is the first Core type built to carry contact details in bulk.
/// </para>
/// </remarks>
public sealed record BookingSummary(
    Guid BookingId,
    BookingInterval Interval,
    BookingStatus Status,
    DateTimeOffset CreatedUtc,
    string BookerName,
    string BookerEmail,
    IReadOnlyList<BookedResource> Resources);

/// <summary>A page of booking summaries plus the unpaged total, for a management list.</summary>
public sealed record BookingPage(IReadOnlyList<BookingSummary> Items, int Total);

/// <summary>
/// What a management list asks for: a window, filters and a page — and it cannot be
/// constructed in a state the store would have to refuse.
/// </summary>
/// <remarks>
/// <para>
/// <b>The window guard lives here, in the type, rather than in a service in front of the
/// store.</b> Bookings accumulate without limit, so an unwindowed or over-wide list is
/// the cost hole <see cref="SiteBookingSettings.MaxQueryRangeDays"/> already exists to
/// close for availability. Putting the check in a service would make it something a
/// caller could route around; putting it in the constructor makes an invalid query
/// unrepresentable, which is a stronger guarantee and needs no cooperation at all.
/// </para>
/// <para>
/// It also keeps <c>UBookIt.Core</c> free of a service depending on a management store,
/// which the <c>bookings</c> capability forbids so that the read ports stay the only
/// pathway anonymous delivery traffic reaches storage through.
/// </para>
/// <para>
/// <b>A class rather than a record, deliberately.</b> A record's <c>with</c> expression
/// would clone around the factory and hand the store exactly the state this type exists
/// to make impossible.
/// </para>
/// </remarks>
public sealed class BookingQuery
{
    /// <summary>The largest page this query will ask for, matching the other management list reads.</summary>
    public const int MaxTake = 500;

    private BookingQuery(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        IReadOnlyCollection<BookingStatus> statuses,
        IReadOnlyCollection<Guid> resourceIds,
        int skip,
        int take)
    {
        FromUtc = fromUtc;
        ToUtc = toUtc;
        Statuses = statuses;
        ResourceIds = resourceIds;
        Skip = skip;
        Take = take;
    }

    /// <summary>Window start, inclusive.</summary>
    public DateTimeOffset FromUtc { get; }

    /// <summary>Window end, exclusive.</summary>
    public DateTimeOffset ToUtc { get; }

    /// <summary>
    /// The statuses to return, already resolved and never empty. Defaulting happens once,
    /// here, so every <see cref="IBookingManagementStore"/> implementation cannot default
    /// differently and turn one guarantee into several.
    /// </summary>
    public IReadOnlyCollection<BookingStatus> Statuses { get; }

    /// <summary>
    /// Return only bookings claiming <b>any</b> of these resources. Empty means no
    /// resource filter — never "match nothing".
    /// </summary>
    public IReadOnlyCollection<Guid> ResourceIds { get; }

    public int Skip { get; }

    public int Take { get; }

    /// <summary>
    /// Builds a query, or explains why the window is unusable.
    /// </summary>
    /// <param name="statuses">
    /// <c>null</c> or empty means the <b>blocking</b> statuses — the set
    /// <see cref="Booking.IsBlocking"/> already defines — because the default answer to
    /// "what is booked" should not silently include bookings that are not. Supplied, it
    /// is used exactly as given, including asking only for cancelled bookings.
    /// </param>
    /// <param name="resourceIds">
    /// A set rather than a single id, and the reason is compatibility rather than
    /// ambition: widening a published <c>Guid?</c> into a collection later would be a
    /// breaking change, while a set behaves identically when given one id.
    /// </param>
    /// <returns>
    /// Fails with <see cref="FailureCodes.DateRangeInvalid"/> when the window does not run
    /// forwards, and <see cref="FailureCodes.DateRangeTooLarge"/> when it spans more than
    /// the site's configured maximum — the same code an over-wide availability query
    /// produces for the same reason.
    /// </returns>
    public static DomainResult<BookingQuery> Create(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        SiteBookingSettings settings,
        IReadOnlyCollection<BookingStatus>? statuses = null,
        IReadOnlyCollection<Guid>? resourceIds = null,
        int skip = 0,
        int take = 50)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (toUtc <= fromUtc)
        {
            return DomainResult<BookingQuery>.Failure(
                FailureCodes.DateRangeInvalid,
                "The queried window must end after it starts.",
                nameof(toUtc));
        }

        // The window is half-open, so its span is the difference rather than the
        // inclusive day count availability computes over two DateOnly values.
        //
        // Whole days, floored, and that is deliberate rather than sloppy. A site-local
        // day is 23 hours on a spring-forward date and 25 on a fall-back one, so a
        // calendar month resolved to instants can be a few minutes over a whole number of
        // days. Comparing raw elapsed time would refuse "show me October" on any European
        // site running the default guardrail — a predictable, annual, entirely reasonable
        // request. The guard exists to stop unbounded queries, and an hour either way is
        // not what it is protecting against.
        var spanDays = Math.Floor((toUtc - fromUtc).TotalDays);

        if (spanDays > settings.MaxQueryRangeDays)
        {
            // The window is named rather than its span rounded. Rounding produced a
            // message that contradicted itself at the boundary the tests exercise — a
            // window one tick over 31 days reported "spans 31 days, which exceeds the
            // maximum of 31" — and the endpoints are what a caller has to change anyway.
            return DomainResult<BookingQuery>.Failure(
                FailureCodes.DateRangeTooLarge,
                $"The queried window [{fromUtc:O}, {toUtc:O}) exceeds the maximum span of "
                + $"{settings.MaxQueryRangeDays} days.",
                nameof(toUtc));
        }

        return DomainResult<BookingQuery>.Success(
            new BookingQuery(
                fromUtc,
                toUtc,
                statuses is { Count: > 0 } supplied
                    ? [.. supplied]
                    : [BookingStatus.Requested, BookingStatus.Confirmed],
                resourceIds is { Count: > 0 } ids ? [.. ids] : [],
                Math.Max(0, skip),
                Math.Clamp(take, 0, MaxTake)));
    }
}

/// <summary>
/// Lists bookings for the backoffice. Implemented by UBookIt.Persistence.
/// <para>
/// Separate from <see cref="IBookingStore"/> for the same reason
/// <see cref="IResourceManagementStore"/> is separate from <see cref="IResourceStore"/>:
/// the front end reads claims to compute availability, and an operator reads bookings to
/// see what a site has taken. Different question, different caller, different port.
/// </para>
/// <para>
/// <b>There is deliberately no filter by service.</b> That is a limit of the stored data
/// rather than a choice about this port: a booking does not record the service that
/// produced it. A service is used to choose the resources a booking claims and is not
/// retained, so answering "which bookings were for this service" would need an additive
/// column and a decision about bookings already placed without one.
/// </para>
/// <para>
/// <b>It does not validate.</b> Management store reads return their page directly; only
/// mutations carry failures. It does not need to validate either: a <see cref="BookingQuery"/>
/// cannot be constructed with an unusable window, so there is no invalid state for a store
/// to defend against. A store that validated would be the only one here, and the next
/// person would reasonably copy it.
/// </para>
/// </summary>
public interface IBookingManagementStore
{
    /// <summary>
    /// The bookings whose interval <b>overlaps</b> the query's window — half-open, on the
    /// same terms as <see cref="IBookingStore.GetClaimsAsync(Guid, DateTimeOffset, DateTimeOffset, CancellationToken)"/>
    /// — matching its status and resource filters, ordered by start time then id, paged.
    /// <para>
    /// A booking claiming several resources SHALL appear <b>once</b>, carrying all of
    /// them, and count once toward <see cref="BookingPage.Total"/>. Filtering and
    /// projecting across the claim join must not multiply the booking it selects.
    /// </para>
    /// </summary>
    Task<BookingPage> ListAsync(BookingQuery query, CancellationToken cancellationToken = default);
}
