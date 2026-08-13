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
    /// <summary>The service's candidate pool: every eligible resource with the lengths the service permits on it.</summary>
    Task<DomainResult<IReadOnlyList<ServiceCandidate>>> ResolveCandidatesAsync(
        Guid serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every start over the range at which some candidate can fulfil the
    /// service, with the lengths bookable there as arithmetic runs. Takes no
    /// duration: one response answers every length.
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
    public async Task<DomainResult<IReadOnlyList<ServiceCandidate>>> ResolveCandidatesAsync(
        Guid serviceId, CancellationToken cancellationToken = default)
    {
        var service = await serviceStore.GetAsync(serviceId, cancellationToken).ConfigureAwait(false);
        if (service is null)
        {
            return DomainResult<IReadOnlyList<ServiceCandidate>>.Failure(
                FailureCodes.ServiceNotFound, $"No service exists with id {serviceId}.");
        }

        // v1 guarantees exactly one role of count 1 (services spec); multi-role
        // composition is a later slice.
        var role = service.Roles[0];

        var resources = await resourceStore
            .ListByTypeAsync(role.ResourceType, cancellationToken)
            .ConfigureAwait(false);

        var candidates = new List<ServiceCandidate>(resources.Count);

        foreach (var resource in resources.OrderBy(r => r.Id))
        {
            // A resource whose constraints admit no length the service permits
            // is excluded silently — that is an answer about the resource, not a
            // validation failure of the service (services spec, TryResolveAgainst).
            if (service.Duration.TryResolveAgainst(resource.Availability.Constraints, out var range))
            {
                candidates.Add(new ServiceCandidate(resource, range));
            }
        }

        return DomainResult<IReadOnlyList<ServiceCandidate>>.Success(candidates);
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

        var candidates = candidateResult.Value;
        if (candidates.Count == 0)
        {
            return DomainResult<IReadOnlyList<ServiceBookableStart>>.Success([]);
        }

        var zone = precondition.Value;

        // One claims read for the whole pool rather than one per candidate
        // (design D5). The window is the queried range in local terms, which
        // contains every open interval any candidate can have in it; claims
        // outside a given candidate's own windows simply subtract nothing.
        var claims = await bookingStore
            .GetClaimsAsync(
                [.. candidates.Select(c => c.ResourceId)],
                WallClockMapper.ToUtc(fromDate, TimeOnly.MinValue, zone),
                WallClockMapper.ToUtc(toDate.AddDays(1), TimeOnly.MinValue, zone),
                cancellationToken)
            .ConfigureAwait(false);

        // Runs are accumulated per start. A set per start collapses identical
        // runs — two candidates configured alike offer the same lengths, and
        // saying so twice tells a consumer nothing.
        var byStart = new Dictionary<DateTimeOffset, HashSet<LengthRun>>();

        foreach (var candidate in candidates)
        {
            var starts = availability.ProjectBookableStarts(candidate.Resource, claims, fromDate, toDate);
            if (!starts.Succeeded)
            {
                return DomainResult<IReadOnlyList<ServiceBookableStart>>.Failure(starts.Failures);
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

        IReadOnlyList<ServiceBookableStart> result = byStart
            .OrderBy(entry => entry.Key)
            .Select(entry => new ServiceBookableStart(entry.Key, Collapse(entry.Value)))
            .ToList();

        return DomainResult<IReadOnlyList<ServiceBookableStart>>.Success(result);
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
    /// <paramref name="outer"/>. Same step and a contained range is sufficient:
    /// both minima are multiples of that shared step, so the two grids are in
    /// phase and no length can fall between <paramref name="outer"/>'s.
    /// </summary>
    private static bool Subsumes(LengthRun outer, LengthRun inner)
        => outer.Step == inner.Step && outer.Min <= inner.Min && outer.Max >= inner.Max;

    public async Task<DomainResult<Booking>> PlaceAsync(
        ServiceBookingRequest request, CancellationToken cancellationToken = default)
    {
        var candidateResult = await ResolveCandidatesAsync(request.ServiceId, cancellationToken).ConfigureAwait(false);
        if (!candidateResult.Succeeded)
        {
            return DomainResult<Booking>.Failure(candidateResult.Failures);
        }

        var candidates = candidateResult.Value;

        // Before the empty-pool guard: a preference naming a resource outside
        // the pool is equally wrong whether the pool is empty or merely lacks
        // that resource, and the caller's own mistake is the more useful thing
        // to report.
        if (request.PreferredResourceId is { } preferred && candidates.All(c => c.ResourceId != preferred))
        {
            // Rejected rather than ignored: the caller named a resource, and
            // quietly booking a different one discards that invisibly (design D9).
            return DomainResult<Booking>.Failure(
                FailureCodes.ResourceNotEligible,
                $"Resource {preferred} cannot fulfil this service.",
                nameof(ServiceBookingRequest.PreferredResourceId));
        }

        if (candidates.Count == 0)
        {
            return Unavailable("No resource is currently able to fulfil this service.");
        }

        // Pool-wide bounds are decided from constraints alone, before any
        // free-time or placement work, so an obviously wrong length gets an
        // actionable code rather than falling through the loop (design D8).
        var boundsFailure = ValidateAgainstPoolBounds(request.Duration, candidates);
        if (boundsFailure is not null)
        {
            return DomainResult<Booking>.Failure(boundsFailure);
        }

        var attemptable = Order(candidates, request.PreferredResourceId)
            .Where(c => Admits(c, request.Duration))
            .ToList();

        if (attemptable.Count == 0)
        {
            // In bounds pool-wide, but no single candidate's own range admits it
            // — a gap between candidates, or off every grid. Deterministic.
            return Unavailable(
                $"No resource able to fulfil this service can be booked for {request.Duration.TotalMinutes:0} minutes.");
        }

        var raced = false;

        foreach (var candidate in attemptable)
        {
            // The full per-resource pipeline, unchanged, one attempt at a time:
            // at most one resource lock is held at any moment, and a failed
            // attempt leaves no rows (bookings spec, atomic placement contract).
            var placed = await bookingService
                .PlaceAsync(
                    new BookingRequest
                    {
                        ResourceId = candidate.ResourceId,
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

            raced |= placed.Failures.Any(f => !IsDeterministic(f.Code));
        }

        // Echoing the last candidate's failures would be arbitrary — it depends
        // on iteration order and describes a resource the caller never named.
        // Instead: was anything actually taken, or was this never bookable?
        return raced
            ? DomainResult<Booking>.Failure(
                FailureCodes.Conflict, "Every resource able to fulfil this service is already booked at that time.")
            : Unavailable("This service cannot be booked at that time.");
    }

    private static DomainResult<Booking> Unavailable(string message)
        => DomainResult<Booking>.Failure(FailureCodes.ServiceUnavailable, message);

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

    private static IEnumerable<ServiceCandidate> Order(
        IReadOnlyList<ServiceCandidate> candidates, Guid? preferredResourceId)
        => preferredResourceId is { } preferred
            ? candidates.Where(c => c.ResourceId == preferred)
                .Concat(candidates.Where(c => c.ResourceId != preferred))
            : candidates;

    private static DomainFailure? ValidateAgainstPoolBounds(
        TimeSpan duration, IReadOnlyList<ServiceCandidate> candidates)
    {
        var shortest = candidates.Min(c => c.Range.Min);
        var longest = candidates.Max(c => c.Range.Max);

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
