using UBookIt.Core.Availability;
using UBookIt.Core.Resources;

namespace UBookIt.Core.Services;

/// <summary>
/// Which of a resource's own bounds kept it out of a service's candidate pool.
/// <para>
/// Named rather than inferred from the numbers, because the three cases are
/// corrected differently: a resource that cannot go long enough, one that cannot
/// go short enough, and one whose grid has no length in the permitted band at
/// all.
/// </para>
/// </summary>
public enum DurationExclusionReason
{
    /// <summary>The service's shortest permitted length exceeds this resource's maximum.</summary>
    ResourceMaximum,

    /// <summary>The service's longest permitted length is below this resource's minimum.</summary>
    ResourceMinimum,

    /// <summary>
    /// The ranges overlap, but no multiple of this resource's granularity lies
    /// inside the overlap — so there is no length it could actually be booked for.
    /// </summary>
    Granularity,
}

/// <summary>
/// A resource that carried the role's type and capabilities but whose own
/// constraints admit no length the service permits, with the bound responsible.
/// <para>
/// The bound is carried alongside the reason because that is what an editor
/// acts on: "Red Room: 2h maximum" says which number to change, where "Red Room
/// is excluded" only says that something is wrong.
/// </para>
/// </summary>
public sealed record DurationExclusion(Resource Resource, DurationExclusionReason Reason, TimeSpan Bound);

/// <summary>
/// One evaluation of a role and a duration against the resources that exist,
/// observable as the ordered chain of filters it applies.
/// <para>
/// The candidate pool is <see cref="Candidates"/> — a projection of this chain,
/// not a separate computation. That is the point of the type: a diagnostic which
/// computed eligibility independently of the booking path would be confidently
/// wrong in exactly the case it exists to detect, namely the two disagreeing
/// (design D1).
/// </para>
/// <para>
/// Each stage is a subset of the one before it, so the count entering a stage
/// and the count leaving it are both derivable. That is what lets a consumer
/// attribute an empty pool to the filter responsible — a mistyped type key, an
/// over-narrow capability set, and a duration no resource can provide are three
/// different faults corrected in three different places, and a single surviving
/// count cannot tell them apart.
/// </para>
/// </summary>
public sealed record ServiceResolution(
    IReadOnlyList<Resource> OfType,
    IReadOnlyList<Resource> WithCapabilities,
    IReadOnlyList<ServiceCandidate> Candidates,
    IReadOnlyList<DurationExclusion> DurationExclusions)
{
    /// <summary>
    /// Classifies why <paramref name="constraints"/> admit no length
    /// <paramref name="duration"/> permits. Only meaningful once
    /// <see cref="ServiceDuration.TryResolveAgainst"/> has returned false; the
    /// caller establishes that, and this names the cause.
    /// </summary>
    internal static DurationExclusion Explain(
        Resource resource, ServiceDuration duration, BookingConstraints constraints)
    {
        // Compared before any granularity rounding, so the reported bound is the
        // one configured on the resource rather than a rounded derivative of it.
        // Order matters only in that the two range tests are mutually exclusive:
        // if the service's floor is above the resource's ceiling it cannot also
        // be true that its ceiling is below the resource's floor.
        var wantedMin = duration.Min ?? constraints.MinDuration;
        var wantedMax = duration.Max ?? constraints.MaxDuration;

        if (wantedMin > constraints.MaxDuration)
        {
            return new DurationExclusion(
                resource, DurationExclusionReason.ResourceMaximum, constraints.MaxDuration);
        }

        if (wantedMax < constraints.MinDuration)
        {
            return new DurationExclusion(
                resource, DurationExclusionReason.ResourceMinimum, constraints.MinDuration);
        }

        // The bands overlap, so what excluded this resource is the grid: every
        // multiple of its granularity falls outside the overlap.
        return new DurationExclusion(
            resource, DurationExclusionReason.Granularity, constraints.Granularity);
    }
}
