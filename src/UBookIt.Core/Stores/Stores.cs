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

    /// <summary>
    /// Persists a booking's <b>status</b>, and nothing else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Deliberately narrow, after three consecutive defects caused by widening it.</b> The
    /// booker is written by <see cref="EraseBookerAsync"/> and by nothing else, so the two
    /// operations touch disjoint columns and no interleaving of them can lose either.
    /// </para>
    /// <para>
    /// Callers read, mutate and write back with no re-read, so the aggregate handed here can be
    /// older than the stored row. Writing only the status bounds what that staleness can cost
    /// to a lost transition, which the status machine refuses on the next attempt. An
    /// implementation that also wrote the booker would let a cancellation restore a person
    /// somebody erased in between; one that wrote the status from an erasure's aggregate would
    /// revert a cancellation and re-block a released slot. Both have happened here.
    /// </para>
    /// </remarks>
    Task UpdateAsync(Booking booking, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a booking to <paramref name="newInterval"/>: releases its claim on the interval it
    /// holds and takes its claim on the new one, in one atomic step with respect to conflict
    /// detection (bookings spec, "Atomic move contract").
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The third narrow write over an existing row, on the same terms as the other two.</b> It writes the
    /// interval columns — start, end and the zone they were validated against — and nothing
    /// else: not the status, not the booker. It takes an id and values rather than an
    /// aggregate, the erasure write's shape, so there is no stale copy of any other column to
    /// write back.
    /// </para>
    /// <para>
    /// <b>The conflict check excludes the booking being moved.</b> A booking's own claim rows
    /// overlap its own new interval whenever the two intervals overlap; a check that counted
    /// them would refuse every small shift. Everything else about the check is placement's:
    /// half-open overlap, blocking statuses only, every claimed resource, under the same locks
    /// in the same order.
    /// </para>
    /// <para>
    /// <b>The status condition is inside the write.</b> The service reads the booking, decides,
    /// and then calls this; a cancellation committing between the two would otherwise let a
    /// cancelled booking move. An implementation SHALL make the write conditional on the stored
    /// status being one of <paramref name="permittedFrom"/>, as a predicate of the same statement
    /// and not a read before it, and SHALL report a write that changed no row for that reason as
    /// <see cref="FailureCodes.InvalidStatusTransition"/>.
    /// </para>
    /// </remarks>
    /// <returns>
    /// Success; <see cref="FailureCodes.Conflict"/> when a blocking claim on one of the booking's
    /// resources overlaps the new interval; <see cref="FailureCodes.InvalidStatusTransition"/>
    /// when the stored status no longer permits a move; <see cref="FailureCodes.BookingNotFound"/>
    /// when no booking has the id.
    /// </returns>
    Task<DomainResult> MoveAsync(
        Guid bookingId,
        BookingInterval newInterval,
        IReadOnlyCollection<BookingStatus> permittedFrom,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a booking's booker contact details and member key, recording when.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>By id and instant, not by aggregate</b> — so there is no stale copy to write, and no
    /// column outside the booker can be touched. The whole operation is "make this booking
    /// erased, as at this instant".
    /// </para>
    /// <para>
    /// <b>An implementation SHALL make the erasure absorbing at the point of storage.</b> Once a
    /// booking records an erasure, this SHALL leave it exactly as it stands — including the
    /// first erasure's instant — rather than re-erasing it, and no implementation SHALL provide
    /// any way to return an erased booker to carrying contact details. Erasure is irreversible,
    /// and that is a promise the package makes to a data subject, not an incidental property of
    /// one storage engine: a substituted store that let a later write restore a person would
    /// falsify it while every test that runs against it passed.
    /// </para>
    /// <para>
    /// <b>The check SHALL NOT be a read followed by a write.</b> An implementation that reads
    /// the current state, decides, and then writes leaves a window in which an erasure can land
    /// between the two — which is not a smaller version of the guarantee, it is the absence of
    /// it. The test belongs inside the write.
    /// </para>
    /// </remarks>
    /// <returns>
    /// <c>true</c> when a booking with that id exists, whether or not it was already erased;
    /// <c>false</c> when none does.
    /// </returns>
    Task<bool> EraseBookerAsync(
        Guid bookingId, DateTimeOffset erasedUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// The ids of at most <paramref name="take"/> bookings whose interval ended before
    /// <paramref name="cutoffUtc"/> and whose booker has not been erased, ordered by end instant
    /// then id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It returns identifiers, and that is a security property rather than an economy.</b> The
    /// <c>booker-erasure</c> capability requires that only a caller permitted to read a booker's
    /// contact details may destroy them. Retention erases with no caller at all, so that gate
    /// cannot be applied to it — and what makes the exception honest instead of a hole is that the
    /// unattended path has nothing to read: this selects on time alone, accepts no contact detail,
    /// and hands back nothing but ids. Widening the return type to a row carrying a name or an
    /// address would put personal data in the hands of the one code path with no user accountable
    /// for it, and would falsify the requirement without changing a line of the requirement.
    /// </para>
    /// <para>
    /// <b>No status filter.</b> A booking that was cancelled, declined or never confirmed holds a
    /// real person's details exactly as firmly as one that went ahead, and a status filter here
    /// would be a way for the sweep to under-erase — the failure mode that matters. Deliberately
    /// absent, on the same reasoning that keeps one off a subject's search.
    /// </para>
    /// <para>
    /// <b>The ordering is required, not a convenience.</b> The sweep repeatedly takes the head of
    /// this set, relying on each erasure removing a booking from it. An unordered top-n may return
    /// different rows on each call, and the sweep's termination argument assumes progress.
    /// </para>
    /// <para>
    /// <b>Callers page by taking the head repeatedly, never by offset.</b> Erasing a booking
    /// removes it from this result, so an offset advanced past a batch just erased skips exactly
    /// as many un-erased bookings as it erased — leaving them behind while the sweep reports
    /// success. There is deliberately no skip parameter to make that mistake with.
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<Guid>> GetBookingIdsDueForErasureAsync(
        DateTimeOffset cutoffUtc, int take, CancellationToken cancellationToken = default);
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
/// It carries the booker's name and email because that is what a list row shows — or, for a
/// booking whose personal data was erased, the fact and instant of that erasure instead.
/// Nothing logs them, and fixtures use invented people — the standing rule, restated because
/// this is the first Core type built to carry contact details in bulk.
/// </para>
/// <para>
/// The service is the attribution <b>stored on the booking</b>, name included, not a join
/// to the service table. That is what lets a row stay answerable for a service since
/// renamed or deleted — a read-time join would report the current name, or nothing at all.
/// A <c>null</c> here means the booking was placed directly, which is a fact rather than a
/// gap. See <see cref="ServiceAttribution"/>.
/// </para>
/// </remarks>
public sealed record BookingSummary(
    Guid BookingId,
    BookingReference Reference,
    BookingInterval Interval,
    BookingStatus Status,
    DateTimeOffset CreatedUtc,
    SummaryBooker Booker,
    IReadOnlyList<BookedResource> Resources,
    ServiceAttribution? Service);

/// <summary>
/// The booker's contact details as a management list row shows them.
/// </summary>
/// <remarks>
/// <b>Name and email only.</b> The booker's phone number and member key are stored and
/// rehydrated by the domain and have never crossed this port; a list row does not display
/// them, and carrying personal data no screen shows would be adding a disclosure with no
/// purpose to serve it. Held together as one value because they are supplied, withheld and
/// erased together.
/// </remarks>
public sealed record SummaryContact(string Name, string Email);

/// <summary>
/// The booker on a management list row: contact details, or the fact that they were erased.
/// </summary>
/// <remarks>
/// <para>
/// The two states the domain permits, carried across the port as two states rather than as
/// strings that might be empty. A consumer cannot reach a name or an email without first
/// establishing that <see cref="Contact"/> is present, which is the same guarantee
/// <see cref="Bookings.Booker"/> makes and for the same reason.
/// </para>
/// <para>
/// <b>This says nothing about who may see the details.</b> The port lists what is stored;
/// whether a particular caller is permitted to read it is the endpoint's question, decided
/// against Umbraco's sensitive-data access and applied when the response is composed. A
/// read port that knew about users would be answering two questions at once.
/// </para>
/// </remarks>
public sealed record SummaryBooker
{
    private SummaryBooker(SummaryContact? contact, DateTimeOffset? erasedUtc)
    {
        Contact = contact;
        ErasedUtc = erasedUtc;
    }

    /// <summary>The contact details, or <c>null</c> where they have been erased.</summary>
    public SummaryContact? Contact { get; }

    /// <summary>When the details were erased, or <c>null</c> for a booker still carrying them.</summary>
    public DateTimeOffset? ErasedUtc { get; }

    /// <summary>True when the contact details have been erased.</summary>
    public bool IsErased => Contact is null;

    /// <summary>A booker whose details are stored.</summary>
    public static SummaryBooker Of(SummaryContact contact) => new(contact, erasedUtc: null);

    /// <summary>A booker whose details were erased, and when.</summary>
    public static SummaryBooker Erased(DateTimeOffset erasedUtc) => new(contact: null, erasedUtc);
}

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

    /// <summary>
    /// The paging a caller gets when it asks for none.
    /// <para>
    /// Named constants rather than bare literals on the factory's parameters, so a caller
    /// that wants "the default" can say so instead of copying the number. A layer that
    /// copies it is a second place the default lives, and which one a caller meets is then
    /// decided by whichever they reach first.
    /// </para>
    /// </summary>
    public const int DefaultSkip = 0;

    /// <inheritdoc cref="DefaultSkip"/>
    public const int DefaultTake = 50;

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
        int skip = DefaultSkip,
        int take = DefaultTake)
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
/// <b>There is deliberately no filter by service — and it is now a scope decision rather
/// than a limit of the data.</b> A booking records the service it was placed for, so the
/// question is answerable; the filter is not offered because it belongs with the screen
/// that would drive it, designed together rather than guessed at in advance.
/// </para>
/// <para>
/// This paragraph previously asserted the opposite — that the service was discarded after
/// choosing resources, and that answering the question would need an additive column. That
/// column now exists. The correction is noted rather than made silently, because a statement
/// of a limit that outlives the limit is worse than no statement at all: a reader inspecting
/// this port was being told the package forgets something it records.
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

    /// <summary>
    /// The bookings whose booker holds <paramref name="query"/>'s email address, ordered and
    /// paged on the same terms as <see cref="ListAsync"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Unwindowed, and that is why it is a second read rather than a filter on the first.</b>
    /// A data subject's request carries an address and no dates, so this must answer without a
    /// window — and <see cref="BookingQuery"/> cannot express one, by construction, because an
    /// unbounded list is the cost hole that type exists to close. Relaxing the window there to
    /// serve this would remove a guarantee from every caller in order to serve one.
    /// </para>
    /// <para>
    /// <b>The address SHALL be matched exactly.</b> No prefix, substring, wildcard or fuzzy
    /// form, and no ordering by a contact detail. An exact match answers whether a given person
    /// is in the records; a partial match answers <i>which people match a fragment</i>, which is
    /// an enumeration facility rather than a lookup and which nothing this package does needs.
    /// See the <c>sensitive-data</c> capability, which requires this of any surface accepting
    /// contact details as input.
    /// </para>
    /// <para>
    /// <b>An erased booking is never returned</b>, because it holds no address to match. That
    /// follows from erasure rather than being enforced here, and it means a subject's bookings
    /// leave their own results as they are erased.
    /// </para>
    /// <para>
    /// <b>This port answers about stored values; it does not decide who may ask.</b> Whether a
    /// caller may reach this at all is the endpoint's question, settled against Umbraco's
    /// sensitive-data access before the call is made. A read port that knew about users would
    /// be answering two questions at once.
    /// </para>
    /// </remarks>
    Task<BookingPage> FindByBookerEmailAsync(
        BookerEmailQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// What a search for a subject's bookings asks for: one address and a page — and, like
/// <see cref="BookingQuery"/>, it cannot be constructed in a state the store would refuse.
/// </summary>
/// <remarks>
/// <para>
/// <b>A type rather than two loose parameters</b>, for the reason <see cref="BookingQuery"/>
/// is one: the guard belongs where the value is made, not in a service in front of the store
/// that a caller could route around. There is no window to bound here, but there is still a
/// blank address and an unbounded page, and both are refused at construction.
/// </para>
/// <para>
/// <b>No status or resource filter, deliberately.</b> A subject asking to be forgotten is
/// asking about every booking they made, and a filtered search would quietly answer a
/// narrower question than the one the law asks. Adding one later would be adding a way to
/// under-report, which is the failure mode that matters here.
/// </para>
/// </remarks>
public sealed class BookerEmailQuery
{
    /// <inheritdoc cref="BookingQuery.MaxTake"/>
    public const int MaxTake = BookingQuery.MaxTake;

    /// <inheritdoc cref="BookingQuery.DefaultSkip"/>
    public const int DefaultSkip = BookingQuery.DefaultSkip;

    /// <inheritdoc cref="BookingQuery.DefaultTake"/>
    public const int DefaultTake = BookingQuery.DefaultTake;

    private BookerEmailQuery(string email, int skip, int take)
    {
        Email = email;
        Skip = skip;
        Take = take;
    }

    /// <summary>
    /// The address to match, trimmed. Compared <b>exactly</b>, under the store's collation.
    /// </summary>
    public string Email { get; }

    public int Skip { get; }

    public int Take { get; }

    /// <summary>
    /// Builds a search, or explains why it is unusable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The address is validated through the same domain rule placement uses, so "what counts as
    /// an address" has one answer in this package rather than two that may disagree. A blank or
    /// malformed one is refused rather than matched: searching for nothing would return
    /// whatever the store happens to hold for a value no booking can have, and telling a caller
    /// "no bookings" for a request that was never a valid question is the wrong answer to give
    /// somebody exercising a right.
    /// </para>
    /// <para>
    /// Trimmed on the way in, matching how the domain stores it, so a copied-and-pasted address
    /// with a trailing space finds the person rather than reporting that they are not there.
    /// </para>
    /// <para>
    /// <b>The name passed to the domain factory is a placeholder and never leaves this method.</b>
    /// <c>Booker.Create</c> validates a whole booker, and only its email rule is wanted here;
    /// reimplementing that rule would be a second answer to "what is an address", which is the
    /// one thing worth avoiding when the answer decides whether somebody's data is found.
    /// </para>
    /// </remarks>
    public static DomainResult<BookerEmailQuery> Create(string? email, int skip, int take)
    {
        var booker = Booker.Create(memberKey: null, name: "unused", email: email);

        if (!booker.Succeeded)
        {
            return DomainResult<BookerEmailQuery>.Failure(booker.Failures);
        }

        // Paging is CLAMPED, not refused — exactly as BookingQuery does it. Two query types
        // that disagreed about whether an over-large page is an error or a ceiling would give
        // the same caller different answers on two endpoints, and neither would be wrong.
        return DomainResult<BookerEmailQuery>.Success(
            new BookerEmailQuery(
                booker.Value.Contact!.Email,
                Math.Max(0, skip),
                Math.Clamp(take, 0, MaxTake)));
    }
}
