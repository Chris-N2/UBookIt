using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Stores;

namespace UBookIt.Core.Services;

/// <summary>
/// A request to book a service. The resource is resolved by the candidate loop,
/// not supplied by the caller — except as an optional preference.
/// </summary>
public sealed record ServiceBookingRequest
{
    public required Guid ServiceId { get; init; }

    public required DateTimeOffset Start { get; init; }

    /// <summary>
    /// The requested length. Required for every service, including a
    /// fixed-duration one, and never substituted: a booker who asks for a length
    /// the service cannot provide is told so rather than silently confirmed for
    /// a different one (book-via-service design D8).
    /// </summary>
    public required TimeSpan Duration { get; init; }

    public required Booker Booker { get; init; }

    /// <summary>
    /// An optional preference for which eligible resource fulfils the booking.
    /// An in-pool preference is attempted first and falls through when it cannot
    /// take the booking; a preference naming a resource outside the pool is
    /// rejected rather than ignored (design D9).
    /// </summary>
    public Guid? PreferredResourceId { get; init; }
}

/// <summary>Availability and placement for a service, resolving its role to eligible resources.</summary>
public interface IServiceBookingService
{
    /// <summary>
    /// Evaluates a role and a duration against the resources that exist,
    /// returning the whole filter chain — not only its survivors.
    /// <para>
    /// Takes the role and duration directly rather than a service or a service
    /// id (design D2). A configuration being previewed may not be a valid
    /// service — most obviously it may have no name — and requiring an aggregate
    /// to be constructible in order to ask which resources a role resolves to
    /// would let a validator with no stake in the question decide whether it can
    /// be asked at all.
    /// </para>
    /// </summary>
    Task<ServiceResolution> ResolveAsync(
        ServiceRole role, ServiceDuration duration, CancellationToken cancellationToken = default);

    /// <summary>
    /// The service's candidate pools, one per role: every eligible resource with
    /// the lengths the service permits on it. A thin wrapper that loads the
    /// service and delegates to
    /// <see cref="ResolveAsync(ServiceRole, ServiceDuration, CancellationToken)"/>
    /// once per role, projecting each pool from its chain rather than filtering
    /// again (design D1). Fails with <see cref="FailureCodes.ServiceNotFound"/>
    /// for an unknown id.
    /// <para>
    /// Pools are returned per role rather than merged: a booking claims one
    /// resource from each, so which role a resource was resolved for is part of
    /// the answer, not an implementation detail.
    /// </para>
    /// <para>
    /// There is deliberately no by-id overload returning the whole chain. The
    /// only caller that wants a chain is the configuration preview, which asks
    /// about a configuration being edited and therefore has roles and a
    /// duration rather than an id — so such an overload would be public surface
    /// with no consumer.
    /// </para>
    /// </summary>
    Task<DomainResult<IReadOnlyList<RoleCandidates>>> ResolveCandidatesAsync(
        Guid serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every start over the range at which the service can be booked, with the
    /// lengths bookable there as arithmetic runs. Takes no duration: one
    /// response answers every length.
    /// <para>
    /// Within a role that is the union over its candidates; across roles it is
    /// the intersection, because a booking has one interval and one length and
    /// every role has to be able to fulfil it (design D2).
    /// </para>
    /// </summary>
    Task<DomainResult<IReadOnlyList<ServiceBookableStart>>> GetBookableStartsAsync(
        Guid serviceId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Places a booking on the first candidate that accepts it, running the
    /// existing per-resource placement pipeline for each attempt.
    /// </summary>
    Task<DomainResult<Booking>> PlaceAsync(
        ServiceBookingRequest request, CancellationToken cancellationToken = default);
}

public sealed class ServiceBookingService(
    IServiceStore serviceStore,
    IResourceStore resourceStore,
    IBookingStore bookingStore,
    IAvailabilityQueryService availability,
    IBookingService bookingService,
    SiteBookingSettings settings) : IServiceBookingService
{
    /// <summary>
    /// The single evaluation. Every other resolution entry point on this service
    /// projects from what this returns, so a diagnostic view and the pool the
    /// booking path acts on cannot disagree (design D1).
    /// </summary>
    public async Task<ServiceResolution> ResolveAsync(
        ServiceRole role, ServiceDuration duration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(duration);

        var ofType = await resourceStore
            .ListByTypeAsync(role.ResourceType, cancellationToken)
            .ConfigureAwait(false);

        // Ordered once, here, so every stage and the pool projected from them
        // share one order rather than each imposing its own.
        var stage1 = ofType.OrderBy(r => r.Id).ToList();

        // Eligibility's second term, evaluated here in Core over capabilities
        // the read port hydrated — never as a storage-layer predicate, which
        // would be a second implementation of the rule free to disagree with
        // this one (design D5). Excluded silently: a resource lacking a
        // capability is an answer about that resource, not a validation failure
        // of the service.
        var stage2 = stage1
            .Where(r => role.RequiredCapabilities.IsSatisfiedBy(r.Capabilities))
            .ToList();

        var candidates = new List<ServiceCandidate>(stage2.Count);
        var exclusions = new List<DurationExclusion>();

        foreach (var resource in stage2)
        {
            // A resource whose constraints admit no length the service permits
            // is excluded silently — that is an answer about the resource, not a
            // validation failure of the service (services spec, TryResolveAgainst).
            // It is recorded rather than merely dropped, because this is the
            // exclusion an editor can neither see nor guess at.
            if (duration.TryResolveAgainst(resource.Availability.Constraints, out var range))
            {
                candidates.Add(new ServiceCandidate(resource, range));
            }
            else
            {
                exclusions.Add(ServiceResolution.Explain(
                    resource, duration, resource.Availability.Constraints));
            }
        }

        return new ServiceResolution(stage1, stage2, candidates, exclusions);
    }

    public async Task<DomainResult<IReadOnlyList<RoleCandidates>>> ResolveCandidatesAsync(
        Guid serviceId, CancellationToken cancellationToken = default)
    {
        var service = await serviceStore.GetAsync(serviceId, cancellationToken).ConfigureAwait(false);
        if (service is null)
        {
            return DomainResult<IReadOnlyList<RoleCandidates>>.Failure(
                FailureCodes.ServiceNotFound, $"No service exists with id {serviceId}.");
        }

        var pools = new List<RoleCandidates>(service.Roles.Count);

        foreach (var role in service.Roles)
        {
            // One evaluation per role. The roles name distinct types, so the
            // pools cannot overlap and resolving them independently loses
            // nothing (design D1).
            var resolution = await ResolveAsync(role, service.Duration, cancellationToken).ConfigureAwait(false);

            // The pool is the chain's final stage, projected — never recomputed.
            pools.Add(new RoleCandidates(role, resolution.Candidates));
        }

        return DomainResult<IReadOnlyList<RoleCandidates>>.Success(pools);
    }

    public async Task<DomainResult<IReadOnlyList<ServiceBookableStart>>> GetBookableStartsAsync(
        Guid serviceId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        // Range and zone first, before any load: an over-wide range costs
        // nothing, and it fails identically to the per-resource queries whether
        // or not the service or its pool exist.
        var precondition = AvailabilityService.ValidateQueryPreconditions(settings, fromDate, toDate);
        if (!precondition.Succeeded)
        {
            return DomainResult<IReadOnlyList<ServiceBookableStart>>.Failure(precondition.Failures);
        }

        var candidateResult = await ResolveCandidatesAsync(serviceId, cancellationToken).ConfigureAwait(false);
        if (!candidateResult.Succeeded)
        {
            return DomainResult<IReadOnlyList<ServiceBookableStart>>.Failure(candidateResult.Failures);
        }

        var pools = candidateResult.Value;

        // A role nothing can fill makes the whole service unbookable: the
        // intersection with an empty set is empty, whichever role it was.
        if (pools.Any(p => p.Candidates.Count == 0))
        {
            return DomainResult<IReadOnlyList<ServiceBookableStart>>.Success([]);
        }

        var zone = precondition.Value;

        // One claims read for the whole pool rather than one per candidate
        // (design D5). The window is the queried range in local terms, which
        // contains every open interval any candidate can have in it; claims
        // outside a given candidate's own windows simply subtract nothing.
        //
        // `toDate.AddDays(1)` is safe only because the shared preconditions above
        // have already rejected a range ending at the last representable date
        // (out-of-range-dates D1). That ordering is load-bearing, not incidental.
        var claims = await bookingStore
            .GetClaimsAsync(
                [.. pools.SelectMany(p => p.Candidates).Select(c => c.ResourceId)],
                WallClockMapper.ToUtc(fromDate, TimeOnly.MinValue, zone),
                WallClockMapper.ToUtc(toDate.AddDays(1), TimeOnly.MinValue, zone),
                cancellationToken)
            .ConfigureAwait(false);

        Dictionary<DateTimeOffset, HashSet<LengthRun>>? composite = null;

        foreach (var pool in pools)
        {
            var byStart = UnionOverPool(pool.Candidates, claims, fromDate, toDate, out var failures);
            if (byStart is null)
            {
                return DomainResult<IReadOnlyList<ServiceBookableStart>>.Failure(failures!);
            }

            // Folded pairwise, left to right: the first role seeds the composite
            // and each further role narrows it.
            composite = composite is null ? byStart : Intersect(composite, byStart);

            if (composite.Count == 0)
            {
                break;
            }
        }

        IReadOnlyList<ServiceBookableStart> result = (composite ?? [])
            .OrderBy(entry => entry.Key)
            .Select(entry => new ServiceBookableStart(entry.Key, Collapse(entry.Value)))
            .ToList();

        return DomainResult<IReadOnlyList<ServiceBookableStart>>.Success(result);
    }

    /// <summary>
    /// One role's availability: the union over its candidates of the lengths
    /// each offers at each start.
    /// <para>
    /// Returns null and sets <paramref name="failures"/> when a candidate's
    /// projection fails, so the caller can report it rather than compose an
    /// answer over a pool it could not read.
    /// </para>
    /// </summary>
    private Dictionary<DateTimeOffset, HashSet<LengthRun>>? UnionOverPool(
        IReadOnlyList<ServiceCandidate> candidates,
        IReadOnlyList<ClaimInfo> claims,
        DateOnly fromDate,
        DateOnly toDate,
        out IReadOnlyList<DomainFailure>? failures)
    {
        failures = null;

        // Runs are accumulated per start. A set per start collapses identical
        // runs — two candidates configured alike offer the same lengths, and
        // saying so twice tells a consumer nothing.
        var byStart = new Dictionary<DateTimeOffset, HashSet<LengthRun>>();

        foreach (var candidate in candidates)
        {
            var starts = availability.ProjectBookableStarts(candidate.Resource, claims, fromDate, toDate);
            if (!starts.Succeeded)
            {
                failures = starts.Failures;
                return null;
            }

            foreach (var start in starts.Value)
            {
                // Exact, not approximate: TryResolveAgainst already moved both
                // bounds inward to granularity multiples and the projected
                // maximum is already floored to one, so every value in the
                // intersection is a length this resource can actually book
                // (design D4).
                var min = DurationMath.MaxOf(start.MinDuration, candidate.Range.Min);
                var max = DurationMath.MinOf(start.MaxDuration, candidate.Range.Max);

                if (min > max)
                {
                    continue;
                }

                if (!byStart.TryGetValue(start.StartUtc, out var runs))
                {
                    runs = [];
                    byStart[start.StartUtc] = runs;
                }

                runs.Add(new LengthRun(min, max, candidate.Granularity));
            }
        }

        return byStart;
    }

    /// <summary>
    /// Composes two roles' availability: the starts both offer, and at each of
    /// those the lengths both offer.
    /// <para>
    /// Starts are absolute instants, so intersecting them is plain set
    /// intersection — two roles on different granularities simply share fewer
    /// starts. Lengths need more care: each role offers a <em>set</em> of runs
    /// at a start, and set intersection distributes over union, so the composite
    /// is every pairwise run intersection (design D2). Intersecting only the
    /// outermost bounds would advertise lengths no pair of resources can book.
    /// </para>
    /// </summary>
    private static Dictionary<DateTimeOffset, HashSet<LengthRun>> Intersect(
        Dictionary<DateTimeOffset, HashSet<LengthRun>> left,
        Dictionary<DateTimeOffset, HashSet<LengthRun>> right)
    {
        var composed = new Dictionary<DateTimeOffset, HashSet<LengthRun>>();

        foreach (var (start, leftRuns) in left)
        {
            if (!right.TryGetValue(start, out var rightRuns))
            {
                // A start only one role can fulfil is not a start the service
                // can be booked at.
                continue;
            }

            var shared = new HashSet<LengthRun>();

            foreach (var a in leftRuns)
            {
                foreach (var b in rightRuns)
                {
                    if (a.TryIntersect(b, out var run))
                    {
                        shared.Add(run);
                    }
                }
            }

            // No length common to both roles: the start goes too, rather than
            // being offered with nothing bookable at it.
            if (shared.Count > 0)
            {
                composed[start] = shared;
            }
        }

        return composed;
    }

    /// <summary>
    /// Drops every run whose lengths another run already offers, then orders
    /// what remains deterministically.
    /// <para>
    /// Identically-configured candidates diverge as soon as one of them is
    /// booked — the same grid and minimum, a shorter remaining run — and
    /// emitting both would say nothing the wider one does not already say.
    /// This is subset elimination, not the merging of overlapping runs: two
    /// runs on different grids each carry lengths the other lacks, so they both
    /// survive (design D3).
    /// </para>
    /// </summary>
    private static List<LengthRun> Collapse(HashSet<LengthRun> runs)
    {
        var ordered = runs.OrderBy(r => r.Min).ThenBy(r => r.Step).ThenBy(r => r.Max).ToList();

        return [.. ordered.Where(run => !ordered.Any(other => other != run && Subsumes(other, run)))];
    }

    /// <summary>
    /// Whether every length <paramref name="inner"/> denotes is also denoted by
    /// <paramref name="outer"/>.
    /// <para>
    /// A contained range plus a step that <em>divides</em> the inner one is
    /// sufficient, because every run is anchored at zero: the inner run's
    /// lengths are multiples of its own step, each of which is therefore a
    /// multiple of the outer step, and all of them lie inside the outer range.
    /// Equal steps are the common case and fall out of the same test.
    /// </para>
    /// <para>
    /// Divisibility rather than equality matters now that runs are intersected
    /// across roles: pairing a role's two runs against another's routinely
    /// yields nested runs on <em>different</em> steps — <c>{60,120,60}</c>
    /// beside <c>{60,120,120}</c> — and an equality test would emit both, the
    /// second saying nothing the first does not.
    /// </para>
    /// </summary>
    private static bool Subsumes(LengthRun outer, LengthRun inner)
        => inner.Step.Ticks % outer.Step.Ticks == 0
            && outer.Min <= inner.Min
            && outer.Max >= inner.Max;

    public async Task<DomainResult<Booking>> PlaceAsync(
        ServiceBookingRequest request, CancellationToken cancellationToken = default)
    {
        var candidateResult = await ResolveCandidatesAsync(request.ServiceId, cancellationToken).ConfigureAwait(false);
        if (!candidateResult.Succeeded)
        {
            return DomainResult<Booking>.Failure(candidateResult.Failures);
        }

        var pools = candidateResult.Value;

        // Before the empty-pool guard: a preference naming a resource outside
        // every pool is equally wrong whether some pool is empty or merely lacks
        // that resource, and the caller's own mistake is the more useful thing
        // to report.
        if (request.PreferredResourceId is { } preferred
            && !pools.Any(p => p.Candidates.Any(c => c.ResourceId == preferred)))
        {
            // Rejected rather than ignored: the caller named a resource, and
            // quietly booking a different one discards that invisibly (design D9).
            return DomainResult<Booking>.Failure(
                FailureCodes.ResourceNotEligible,
                $"Resource {preferred} cannot fulfil this service.",
                nameof(ServiceBookingRequest.PreferredResourceId));
        }

        if (pools.Any(p => p.Candidates.Count == 0))
        {
            return Unavailable("No resource is currently able to fulfil this service.");
        }

        // Pool-wide bounds are decided from constraints alone, before any
        // free-time or placement work, so an obviously wrong length gets an
        // actionable code rather than falling through the loop (design D8).
        var boundsFailure = ValidateAgainstPoolBounds(request.Duration, pools);
        if (boundsFailure is not null)
        {
            return DomainResult<Booking>.Failure(boundsFailure);
        }

        // One shortlist per role, in resource-id order.
        //
        // The preferred resource is deliberately NOT ordered to the head any more.
        // It used to be, because a resource belonged to exactly one role's pool; now
        // that it can be eligible for several, hoisting it put the same resource at
        // the head of every one of them — making an assignment that claims it twice
        // the *first* attempt, which `Booking.Create` throws on rather than refuses.
        // Preference is expressed by asking the assignment to include it instead
        // (design D4), which is a constraint on the booking rather than on a role.
        var attemptable = pools
            .Select(pool => pool.Candidates.Where(c => Admits(c, request.Duration)).ToList())
            .ToList();

        if (attemptable.Any(shortlist => shortlist.Count == 0))
        {
            // In bounds pool-wide, but some role has no candidate whose own
            // range admits it — a gap between candidates, or off every grid.
            // Deterministic.
            return Unavailable(
                $"No resource able to fulfil this service can be booked for {request.Duration.TotalMinutes:0} minutes.");
        }

        // Drop candidates already claimed at the requested interval, in one read
        // over every shortlist (design D4a).
        //
        // Without this the search would begin from a pool in which everything is
        // busy, and every assignment it found would be attempted and lost — two
        // roles of sixty candidates each, all busy, is a great many sequential
        // locking transactions for one anonymous request, because the filter above
        // is on the requested *length* and bounds nothing when the resources are
        // merely occupied.
        //
        // The read is advisory, not a substitute for the atomic contract: a
        // candidate free here may be taken before the placement lands, which the
        // loop below still handles. It can only ever cause a false `conflict` —
        // never a booking on a resource that was not free — because placement
        // rechecks under the lock.
        var free = await FreeShortlistsAsync(attemptable, request, cancellationToken).ConfigureAwait(false);

        if (free is null)
        {
            // The interval is unrepresentable, so no claims window can be formed.
            // Left to the placement pipeline, which owns that failure and reports
            // `interval-invalid` for it.
            free = attemptable;
        }

        // Roles become slots: a role of count N contributes N interchangeable
        // slots drawn from its pool, and one resource fills at most one of them.
        // From here on the unit of everything — the attempt, the classification,
        // the exclusion reasoning — is a slot rather than a role.
        var attemptableSlots = ExpandSlots(pools, attemptable);
        var freeSlots = ExpandSlots(pools, free);

        // Candidates the pre-filter dropped were never attempted, so the all-fail
        // outcome has to be reasoned about rather than observed. The reasoning is
        // in RuleClassification, which asks the placement service the same
        // questions an attempt would have answered.
        var rules = new RuleClassification(bookingService, request, attemptableSlots, freeSlots);

        // Whether any assignment that actually ran reached the conflict check and
        // lost. The classification below adds what the excluded ones would have
        // contributed.
        var raced = false;

        // The candidates still worth assigning. Each failed attempt removes the
        // resources it has learned something bad about, so the loop is bounded by
        // the number of candidates rather than by the product of the pool sizes —
        // the bound the cartesian walk could not offer (task 3.5).
        var remaining = freeSlots.Select(slot => slot.ToList()).ToList();

        while (Resolve(remaining, request.PreferredResourceId) is { } assignment)
        {
            // One booking claiming one resource per slot, all distinct by
            // construction, placed through the store's all-or-nothing contract:
            // either every claim is persisted or none is, so attempting
            // assignments in sequence is safe (bookings spec, atomic placement
            // contract).
            var placed = await bookingService
                .PlaceAsync(
                    new MultiClaimBookingRequest
                    {
                        ResourceIds = [.. assignment.Select(c => c.ResourceId)],
                        Start = request.Start,
                        Duration = request.Duration,
                        Booker = request.Booker,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (placed.Succeeded)
            {
                return placed;
            }

            // Some failures are properties of the request or the site, not of
            // the candidate: a broken site time zone, and an interval that
            // cannot be represented at all. They are identical for every
            // candidate, so looping on cannot help and neither all-fail code
            // describes them. Echo them instead of translating them.
            if (placed.Failures.FirstOrDefault(f => IsRequestLevel(f.Code)) is { } requestFailure)
            {
                return DomainResult<Booking>.Failure(requestFailure);
            }

            raced |= placed.Failures.Any(f => !IsDeterministic(f.Code));

            if (!Exclude(remaining, assignment, rules))
            {
                // Nothing was learned, so the next assignment would be the same one
                // and the loop would not terminate. Bail to the classification.
                break;
            }
        }

        // Echoing the last assignment's failures would be arbitrary — it depends on
        // iteration order and describes resources the caller never named. Instead:
        // was anything actually taken, or was this never bookable? The attempts
        // answer for the assignments that ran; the classification answers for the
        // candidates the pre-filter removed.
        return rules.AllFail(raced);
    }

    /// <summary>
    /// A role of count <c>N</c> repeated into <c>N</c> slots, in role order then
    /// slot index — the order the assignment is deterministic in.
    /// <para>
    /// The candidate lists are shared between a role's slots rather than copied:
    /// the slots of one role are interchangeable by definition, and distinctness is
    /// the assignment's job rather than the expansion's.
    /// </para>
    /// </summary>
    private static List<List<ServiceCandidate>> ExpandSlots(
        IReadOnlyList<RoleCandidates> pools, List<List<ServiceCandidate>> byRole)
    {
        var slots = new List<List<ServiceCandidate>>();

        for (var role = 0; role < pools.Count; role++)
        {
            for (var unit = 0; unit < pools[role].Role.Count; unit++)
            {
                slots.Add(byRole[role]);
            }
        }

        return slots;
    }

    /// <summary>
    /// One candidate per slot, all distinct, honouring a preferred resource where
    /// an assignment can — or null when no assignment saturates the slots.
    /// <para>
    /// Preference is a constraint on the booking rather than on a role (design D4):
    /// the assignment is asked for a saturating matching that includes the resource
    /// in whichever slot it fits, and only when none exists does the preference fall
    /// through, as it does today. A preference naming a resource outside every pool
    /// was already rejected before any of this ran.
    /// </para>
    /// </summary>
    private static List<ServiceCandidate>? Resolve(
        List<List<ServiceCandidate>> slots, Guid? preferredResourceId)
    {
        var ids = slots
            .Select(slot => (IReadOnlyList<Guid>)slot.Select(c => c.ResourceId).ToList())
            .ToList();

        var assignment = preferredResourceId is { } preferred
            ? SlotAssignment.TrySaturateIncluding(ids, preferred) ?? SlotAssignment.TrySaturate(ids)
            : SlotAssignment.TrySaturate(ids);

        if (assignment is null)
        {
            return null;
        }

        return [.. assignment.Select((id, slot) => slots[slot].First(c => c.ResourceId == id))];
    }

    /// <summary>
    /// Removes from every slot the resources a failed attempt has condemned, and
    /// reports whether anything was actually removed.
    /// <para>
    /// A resource whose own rules refuse the request is dropped alone, because the
    /// fault is known to be its: the rest of the assignment is innocent and must
    /// stay available to the next one. That is what keeps a deterministic refusal
    /// choosing the same replacement the candidate walk used to choose.
    /// </para>
    /// <para>
    /// A conflict names nobody — the store reports that the placement clashed, not
    /// which claim clashed — so every resource of the assignment is dropped. That is
    /// deliberately conservative: it can report <c>conflict</c> for a service a
    /// different assignment could still have booked, in the window between the
    /// advisory claims read and the attempt. The alternative is a second claims read
    /// per failed attempt, and a false <c>conflict</c> in a genuine race is
    /// precisely what the advisory read is already permitted to cause — the caller
    /// retries and succeeds, where a wrong booking could not be undone.
    /// </para>
    /// </summary>
    private static bool Exclude(
        List<List<ServiceCandidate>> slots,
        List<ServiceCandidate> assignment,
        RuleClassification rules)
    {
        var condemned = assignment
            .Where(candidate => !rules.Check(candidate).Succeeded)
            .Select(candidate => candidate.ResourceId)
            .ToHashSet();

        if (condemned.Count == 0)
        {
            condemned = [.. assignment.Select(candidate => candidate.ResourceId)];
        }

        var removed = false;

        for (var slot = 0; slot < slots.Count; slot++)
        {
            // Each slot holds its own list by this point — the caller copied them
            // out of the shared expansion — so a removal here is a removal from one
            // slot, and the loop applies it to all of them explicitly.
            var kept = slots[slot].Where(c => !condemned.Contains(c.ResourceId)).ToList();

            removed |= kept.Count != slots[slot].Count;
            slots[slot] = kept;
        }

        return removed;
    }

    /// <summary>
    /// Decides what an all-fail placement tells the caller, including for the
    /// assignments the claims pre-filter removed before they could be attempted.
    /// <para>
    /// The unit of an attempt is a <b>saturating assignment</b>, not a candidate and
    /// no longer a combination, and that is the whole difficulty. `BookingService`
    /// accumulates each claimed resource's rules before the conflict check, so an
    /// attempt reaches that check only when <em>every slot</em> is filled by a
    /// resource whose own rules admit the request. One admitting candidate says
    /// nothing if some slot cannot be filled at all — no assignment containing it
    /// could ever have raced, and reporting `conflict` would invite a retry that
    /// cannot succeed.
    /// </para>
    /// <para>
    /// Restating that over slots rather than roles is what makes it survive counts.
    /// "Every role has an admitting candidate" was the right test only while a role
    /// consumed one resource: a role of count 2 with a single admitting candidate
    /// passes it and still cannot be filled. The question is now asked of the
    /// assignment itself, which answers it for same-type roles and counts alike.
    /// </para>
    /// <para>
    /// Rules are evaluated by the placement service and memoised per resource, so
    /// this cannot drift from what an attempt would have done, and each resource
    /// costs one evaluation however many slots it is a candidate for.
    /// </para>
    /// </summary>
    private sealed class RuleClassification(
        IBookingService bookingService,
        ServiceBookingRequest request,
        List<List<ServiceCandidate>> shortlists,
        List<List<ServiceCandidate>> free)
    {
        private readonly Dictionary<Guid, DomainResult> _checked = [];

        /// <summary>
        /// Whether this resource's own rules admit the request, memoised.
        /// <para>
        /// Shared with the attempt loop's exclusion step rather than kept private:
        /// a resource that refused deterministically must be dropped from the
        /// search, and asking the same memoised evaluation is what stops the
        /// exclusion and the classification disagreeing about why an attempt failed.
        /// </para>
        /// </summary>
        internal DomainResult Check(ServiceCandidate candidate)
        {
            if (!_checked.TryGetValue(candidate.ResourceId, out var result))
            {
                result = bookingService.CheckPlacementRules(
                    candidate.Resource, request.Start, request.Duration);

                _checked[candidate.ResourceId] = result;
            }

            return result;
        }

        internal DomainResult<Booking> AllFail(bool raced)
        {
            // A failure that is a property of the request or the site rather
            // than of any resource — a broken site time zone, an interval that
            // cannot be represented — is reported as itself. The attempt loop
            // echoes these when it runs; when every candidate was excluded it
            // never runs, and collapsing the check to a boolean would bury them
            // under an all-fail code that describes the pool instead.
            foreach (var candidate in shortlists.SelectMany(shortlist => shortlist))
            {
                if (Check(candidate).Failures.FirstOrDefault(f => IsRequestLevel(f.Code)) is { } requestFailure)
                {
                    return DomainResult<Booking>.Failure(requestFailure);
                }
            }

            return raced || ExcludedAssignmentCouldHaveRaced()
                ? DomainResult<Booking>.Failure(
                    FailureCodes.Conflict,
                    "Every resource able to fulfil this service is already booked at that time.")
                : Unavailable("This service cannot be booked at that time.");
        }

        /// <summary>
        /// Whether an assignment the pre-filter removed would have reached the
        /// conflict check: a saturating assignment exists among the candidates whose
        /// rules admit the request, and none exists once the claimed ones are taken
        /// out — so being claimed is exactly what broke it.
        /// <para>
        /// Both halves are load-bearing. Without the first, a claimed candidate
        /// would be read as a lost race even where some slot could never be filled
        /// at all, reporting `conflict` for a request that can never succeed and
        /// inviting a retry that cannot help. Without the second, a service with a
        /// perfectly bookable assignment left over would be blamed on the claims.
        /// </para>
        /// <para>
        /// Stated over assignments rather than "every role has an admitting
        /// candidate", which a role of count 2 with one admitting candidate would
        /// pass while being unfillable.
        /// </para>
        /// </summary>
        private bool ExcludedAssignmentCouldHaveRaced()
        {
            var admitting = Ids(shortlists);

            if (SlotAssignment.TrySaturate(admitting) is null)
            {
                return false;
            }

            var stillFree = free
                .Select(slot => slot.Select(c => c.ResourceId).ToHashSet())
                .ToList();

            var admittingAndFree = admitting
                .Select((slot, index) => (IReadOnlyList<Guid>)slot.Where(stillFree[index].Contains).ToList())
                .ToList();

            return SlotAssignment.TrySaturate(admittingAndFree) is null;
        }

        /// <summary>
        /// The resource ids of the candidates whose own rules admit the request, one
        /// list per slot — the graph an attempt could actually have run over.
        /// </summary>
        private List<IReadOnlyList<Guid>> Ids(List<List<ServiceCandidate>> slots)
            => [.. slots.Select(slot => (IReadOnlyList<Guid>)slot
                .Where(candidate => Check(candidate).Succeeded)
                .Select(candidate => candidate.ResourceId)
                .ToList())];
    }

    private static DomainResult<Booking> Unavailable(string message)
        => DomainResult<Booking>.Failure(FailureCodes.ServiceUnavailable, message);

    /// <summary>
    /// Failures that describe the request or the site rather than a candidate's
    /// answer to it. Every candidate would report them identically, so reporting
    /// "no resource could take this" instead would blame the pool for a fault
    /// that has nothing to do with it — and would tell a caller whose date is
    /// simply unrepresentable to look for another time slot.
    /// </summary>
    private static bool IsRequestLevel(string code)
        => code is FailureCodes.TimeZoneInvalid or FailureCodes.IntervalInvalid;

    /// <summary>
    /// The refusals that are a property of the request against a resource's
    /// configuration, and so cannot come good on a retry.
    /// <para>
    /// Whitelisted rather than inferred from "not <c>conflict</c>": treating
    /// every unrecognised failure as deterministic would report
    /// <c>service-unavailable</c> for a transient one — a candidate deleted
    /// mid-loop yields <c>resource-not-found</c> — telling the caller not to
    /// retry when retrying would work, and polluting the drift signal that code
    /// exists to be (design D6).
    /// </para>
    /// </summary>
    private static bool IsDeterministic(string code)
        => code is FailureCodes.IntervalInvalid
            or FailureCodes.Granularity
            or FailureCodes.DurationTooShort
            or FailureCodes.DurationTooLong
            or FailureCodes.LeadTime
            or FailureCodes.Horizon
            or FailureCodes.OutsideOpenHours;

    /// <summary>
    /// Whether the requested length is one this candidate actually offers —
    /// inside its resolved range and on its grid. A candidate that does not
    /// offer it is excluded deterministically rather than attempted and refused.
    /// </summary>
    private static bool Admits(ServiceCandidate candidate, TimeSpan duration)
        => duration >= candidate.Range.Min
            && duration <= candidate.Range.Max
            && duration.Ticks % candidate.Granularity.Ticks == 0;

    /// <summary>
    /// The shortlists with every candidate already claimed at the requested
    /// interval removed, judged from one claims read over all of them.
    /// <para>
    /// Returns null when the requested interval cannot be represented at all, so
    /// no window can be formed to read; the caller leaves that to the placement
    /// pipeline, which owns the failure.
    /// </para>
    /// </summary>
    private async Task<List<List<ServiceCandidate>>?> FreeShortlistsAsync(
        List<List<ServiceCandidate>> shortlists,
        ServiceBookingRequest request,
        CancellationToken cancellationToken)
    {
        if (!CalendarBounds.TryAdd(request.Start, request.Duration, out var end))
        {
            return null;
        }

        var claims = await bookingStore
            .GetClaimsAsync(
                [.. shortlists.SelectMany(s => s).Select(c => c.ResourceId)],
                request.Start,
                end,
                cancellationToken)
            .ConfigureAwait(false);

        // Only a blocking claim occupies a resource; a cancelled booking leaves
        // its interval free, exactly as the availability path treats it.
        var claimed = claims
            .Where(AvailabilityService.IsBlocking)
            .Select(c => c.ResourceId)
            .ToHashSet();

        return [.. shortlists.Select(shortlist =>
            shortlist.Where(c => !claimed.Contains(c.ResourceId)).ToList())];
    }

    /// <summary>
    /// Whether the requested length is one <em>some</em> candidate of
    /// <em>every</em> role could provide, judged from constraints alone.
    /// <para>
    /// The floor is the highest of the roles' own floors and the ceiling the
    /// lowest of their ceilings: a length below any one role's shortest, or
    /// above any one role's longest, cannot be booked whatever the other roles
    /// permit.
    /// </para>
    /// </summary>
    private static DomainFailure? ValidateAgainstPoolBounds(
        TimeSpan duration, IReadOnlyList<RoleCandidates> pools)
    {
        var shortest = pools.Max(p => p.Candidates.Min(c => c.Range.Min));
        var longest = pools.Min(p => p.Candidates.Max(c => c.Range.Max));

        if (duration < shortest)
        {
            return new DomainFailure(
                FailureCodes.DurationTooShort,
                $"This service must be booked for at least {shortest.TotalMinutes:0} minutes.");
        }

        return duration > longest
            ? new DomainFailure(
                FailureCodes.DurationTooLong,
                $"This service may be booked for at most {longest.TotalMinutes:0} minutes.")
            : null;
    }
}
