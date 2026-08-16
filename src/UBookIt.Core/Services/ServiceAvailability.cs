using UBookIt.Core.Availability;
using UBookIt.Core.Resources;

namespace UBookIt.Core.Services;

/// <summary>
/// An arithmetic run of bookable lengths: <c>Min, Min + Step, …, Max</c>
/// inclusive.
/// <para>
/// One contributing resource produces exactly one run at a start. Runs are the
/// wire and domain shape for service availability because a candidate pool does
/// not offer a contiguous band of lengths: candidates differ in granularity and
/// in minimum duration, so their union is in general neither contiguous nor on
/// one grid. Collapsing a start's runs to a single (min, max) pair would
/// advertise lengths no candidate can book (book-via-service design D3).
/// </para>
/// <para>
/// <b>A run is anchored at its step</b>: <see cref="Min"/> is always a multiple
/// of <see cref="Step"/>, so the run denotes exactly the multiples of
/// <see cref="Step"/> in <c>[Min, Max]</c> and two runs are in phase at zero.
/// Enforced here rather than left to the sites that build runs, because more
/// than one algorithm silently depends on it, and an out-of-phase run would break
/// them without raising anything — advertising lengths no resource can book, or
/// discarding lengths that were bookable (multi-role-composition design D3).
/// </para>
/// <para>
/// What depends on it changed with ⑨-2 and the property did not. Subset
/// elimination still compares two runs assuming they are in phase. Intersection
/// across roles — the other original reason — is gone: composition now asks the
/// assignment which lengths are feasible and rebuilds runs from that set, by
/// scanning the multiples of each candidate step. That scan is anchored by
/// construction and only produces valid runs *because* every input run is.
/// </para>
/// <para>
/// The bounds are get-only rather than <c>init</c> so that <c>with</c> cannot
/// reach past the constructor and rebuild a run out of phase.
/// </para>
/// </summary>
public readonly record struct LengthRun
{
    public LengthRun(TimeSpan min, TimeSpan max, TimeSpan step)
    {
        if (step <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(step), step, "A length run's step must be positive.");
        }

        if (min < TimeSpan.Zero || max < min)
        {
            throw new ArgumentOutOfRangeException(
                nameof(max), max, $"A length run's range must be non-negative and ordered (min {min}, max {max}).");
        }

        if (min.Ticks % step.Ticks != 0)
        {
            throw new ArgumentException(
                $"A length run must be anchored at its step: a minimum of {min} is not a multiple of {step}.",
                nameof(min));
        }

        // The union-availability requirement also states that Max is a multiple
        // of Step, "so Max is always reachable". Every producer already floors
        // it; checking it here is what stops that from being a second property
        // three algorithms rely on and nothing enforces.
        if (max.Ticks % step.Ticks != 0)
        {
            throw new ArgumentException(
                $"A length run's maximum must be reachable: {max} is not a multiple of {step}.",
                nameof(max));
        }

        Min = min;
        Max = max;
        Step = step;
    }

    /// <summary>The shortest length this run offers. Always a multiple of <see cref="Step"/>.</summary>
    public TimeSpan Min { get; }

    /// <summary>The longest length this run offers.</summary>
    public TimeSpan Max { get; }

    /// <summary>The spacing between the lengths this run offers.</summary>
    public TimeSpan Step { get; }

    /// <summary>True when this run admits exactly one length.</summary>
    public bool IsSingleLength => Min == Max;

    /// <summary>Whether a length is one of the lengths this run denotes.</summary>
    public bool Admits(TimeSpan duration)
        => duration >= Min && duration <= Max && (duration - Min).Ticks % Step.Ticks == 0;

    /// <summary>The lengths this run denotes, ascending.</summary>
    public IEnumerable<TimeSpan> Lengths()
    {
        for (var length = Min; length <= Max; length += Step)
        {
            yield return length;
        }
    }
}

/// <summary>
/// A start at which a service can be booked, with the lengths available there.
/// Deliberately carries no resource id: v1 resolves the resource at placement
/// time and makes no promise about which candidate a booker will get, so naming
/// one here would imply a guarantee placement does not make (design D11).
/// </summary>
public sealed record ServiceBookableStart(DateTimeOffset StartUtc, IReadOnlyList<LengthRun> Runs)
{
    /// <summary>Whether any run at this start admits the given length.</summary>
    public bool Admits(TimeSpan duration) => Runs.Any(r => r.Admits(duration));
}

/// <summary>
/// A resource able to fulfil a service, paired with the lengths the service
/// permits on it — that resource's own range narrowed by the service's duration
/// specification. Membership of the pool is the final stage of
/// <see cref="ServiceResolution"/>: type key, then required capabilities, then a
/// duration range that admits a permitted length.
/// </summary>
public sealed record ServiceCandidate(Resource Resource, DurationRange Range)
{
    public Guid ResourceId => Resource.Id;

    public TimeSpan Granularity => Resource.Availability.Constraints.Granularity;
}

/// <summary>
/// One role of a service with the resources able to fill it.
/// <para>
/// Pools are kept per role rather than flattened into one because a booking
/// takes one resource from <em>each</em> — a flat pool could not say which role
/// a resource was counted for, and every rule this change adds (a start every
/// role can fulfil, a length every role can provide, one claim per role) is
/// stated over the roles individually.
/// </para>
/// <para>
/// Pools <em>overlap</em> in general: two roles may name one resource type and be
/// told apart only by the capabilities they require, so the same resource can
/// appear in both. That is why choosing each role a candidate independently is no
/// longer correct — a resource taken for one role is unavailable to the other, and
/// a greedy choice can strand a role that had no alternative. Resolving the pools
/// is therefore separate from assigning them: these are the candidates,
/// <see cref="SlotAssignment"/> decides who fills what (design D1).
/// </para>
/// </summary>
public sealed record RoleCandidates(ServiceRole Role, IReadOnlyList<ServiceCandidate> Candidates);
