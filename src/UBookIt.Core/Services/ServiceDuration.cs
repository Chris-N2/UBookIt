using UBookIt.Core.Availability;
using UBookIt.Core.Common;

namespace UBookIt.Core.Services;

/// <summary>Which of the two duration shapes a service carries.</summary>
public enum ServiceDurationKind
{
    /// <summary>A single length. The degenerate range whose minimum and maximum are equal.</summary>
    Fixed,

    /// <summary>The booker chooses the length, within whatever bounds are supplied.</summary>
    Variable,
}

/// <summary>
/// A concrete bookable length range on a specific resource: the result of
/// narrowing a <see cref="ServiceDuration"/> by that resource's constraints.
/// Both bounds are granularity multiples, so every multiple between them is
/// bookable.
/// </summary>
public readonly record struct DurationRange(TimeSpan Min, TimeSpan Max)
{
    /// <summary>True when the range admits exactly one length.</summary>
    public bool IsSingleLength => Min == Max;
}

/// <summary>
/// A service's duration specification: either a fixed length or a variable
/// length with optional bounds. Constructible only through the validating
/// factories, so a service can never hold a combination of duration fields
/// that has no meaning.
/// <para>
/// The specification <em>narrows</em> — never widens — the range the fulfilling
/// resource already permits (services spec, "Service duration semantics"). A
/// resource's maximum is a hard ceiling regardless of what a service says.
/// </para>
/// </summary>
public sealed record ServiceDuration
{
    /// <summary>Failure field for a fixed length.</summary>
    public const string LengthField = "Duration";

    /// <summary>Failure field for a variable lower bound.</summary>
    public const string MinField = "Duration.Min";

    /// <summary>Failure field for a variable upper bound.</summary>
    public const string MaxField = "Duration.Max";

    private ServiceDuration(ServiceDurationKind kind, TimeSpan? min, TimeSpan? max)
    {
        Kind = kind;
        Min = min;
        Max = max;
    }

    public ServiceDurationKind Kind { get; }

    /// <summary>
    /// The lower bound. Always set (and equal to <see cref="Max"/>) when fixed;
    /// null when variable with no minimum, meaning the resource's own minimum applies.
    /// </summary>
    public TimeSpan? Min { get; }

    /// <summary>
    /// The upper bound. Always set (and equal to <see cref="Min"/>) when fixed;
    /// null when variable with no maximum, meaning the resource's own maximum applies.
    /// </summary>
    public TimeSpan? Max { get; }

    /// <summary>The fixed length, or null when this is a variable duration.</summary>
    public TimeSpan? FixedLength => Kind == ServiceDurationKind.Fixed ? Min : null;

    /// <summary>
    /// A variable duration with no bounds — the unconfigured default, which
    /// defers entirely to each resource's own range.
    /// </summary>
    public static ServiceDuration Unbounded { get; } = new(ServiceDurationKind.Variable, null, null);

    /// <summary>A single appointment length.</summary>
    public static DomainResult<ServiceDuration> Fixed(TimeSpan length)
    {
        var failures = new List<DomainFailure>();
        Validate(length, LengthField, failures);

        return failures.Count > 0
            ? DomainResult<ServiceDuration>.Failure(failures)
            : DomainResult<ServiceDuration>.Success(new ServiceDuration(ServiceDurationKind.Fixed, length, length));
    }

    /// <summary>
    /// A booker-chosen length. Either bound may be omitted, in which case the
    /// fulfilling resource's own bound applies. Bounds need not align to any
    /// granularity — they are bounds, not lengths.
    /// </summary>
    public static DomainResult<ServiceDuration> Variable(TimeSpan? min, TimeSpan? max)
    {
        var failures = new List<DomainFailure>();

        if (min is { } lower)
        {
            Validate(lower, MinField, failures);
        }

        if (max is { } upper)
        {
            Validate(upper, MaxField, failures);
        }

        // Only meaningful once both bounds are individually sound; reporting
        // "minimum exceeds maximum" for a negative bound would be noise.
        if (failures.Count == 0 && min is { } l && max is { } u && l > u)
        {
            failures.Add(new DomainFailure(
                FailureCodes.ServiceDurationInvalid,
                $"The minimum duration ({l.TotalMinutes:0} minutes) must not exceed the maximum ({u.TotalMinutes:0} minutes).",
                MinField));
        }

        return failures.Count > 0
            ? DomainResult<ServiceDuration>.Failure(failures)
            : DomainResult<ServiceDuration>.Success(new ServiceDuration(ServiceDurationKind.Variable, min, max));
    }

    /// <summary>
    /// Narrows this specification by a resource's constraints, yielding the
    /// lengths actually bookable on that resource.
    /// <para>
    /// Returns false when the resource cannot fulfil the service — either the
    /// ranges do not intersect, or no granularity multiple lies inside the
    /// intersection. That is an answer rather than a validation failure: a
    /// service spanning many resources cannot know each resource's limits, so
    /// this is how a resource is excluded from eligibility. The caller names
    /// the outcome; booking-via-service turns it into a candidate filter.
    /// </para>
    /// </summary>
    public bool TryResolveAgainst(BookingConstraints constraints, out DurationRange range)
    {
        var granularity = constraints.Granularity;

        // Intersect, then move each bound inward to a granularity multiple —
        // never outward, which would offer a length the resource rejects.
        var min = DurationMath.CeilTo(
            DurationMath.MaxOf(Min ?? constraints.MinDuration, constraints.MinDuration), granularity);
        var max = DurationMath.FloorTo(
            DurationMath.MinOf(Max ?? constraints.MaxDuration, constraints.MaxDuration), granularity);

        if (min > max)
        {
            range = default;
            return false;
        }

        range = new DurationRange(min, max);
        return true;
    }

    private static void Validate(TimeSpan value, string field, List<DomainFailure> failures)
    {
        if (value <= TimeSpan.Zero)
        {
            failures.Add(new DomainFailure(
                FailureCodes.ServiceDurationInvalid, "A service duration must be positive.", field));
            return;
        }

        // Durations are persisted and exposed as whole minutes; reject
        // sub-minute values at the domain boundary so stored state always
        // round-trips.
        if (value.Ticks % TimeSpan.TicksPerMinute != 0)
        {
            failures.Add(new DomainFailure(
                FailureCodes.ServiceDurationInvalid, "A service duration must be a whole number of minutes.", field));
        }
    }

}
