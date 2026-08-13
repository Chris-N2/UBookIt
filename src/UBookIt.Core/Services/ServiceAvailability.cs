using UBookIt.Core.Availability;
using UBookIt.Core.Resources;

namespace UBookIt.Core.Services;

/// <summary>
/// An arithmetic run of bookable lengths: <c>Min, Min + Step, …, Max</c>
/// inclusive. Both bounds are multiples of <see cref="Step"/>, so
/// <see cref="Max"/> is always itself reachable.
/// <para>
/// One contributing resource produces exactly one run at a start. Runs are the
/// wire and domain shape for service availability because a candidate pool does
/// not offer a contiguous band of lengths: candidates differ in granularity and
/// in minimum duration, so their union is in general neither contiguous nor on
/// one grid. Collapsing a start's runs to a single (min, max) pair would
/// advertise lengths no candidate can book (book-via-service design D3).
/// </para>
/// </summary>
public readonly record struct LengthRun(TimeSpan Min, TimeSpan Max, TimeSpan Step)
{
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
/// specification. Membership of the pool is decided by resource type key alone
/// in v1; capability-constrained eligibility is a later slice.
/// </summary>
public sealed record ServiceCandidate(Resource Resource, DurationRange Range)
{
    public Guid ResourceId => Resource.Id;

    public TimeSpan Granularity => Resource.Availability.Constraints.Granularity;
}
