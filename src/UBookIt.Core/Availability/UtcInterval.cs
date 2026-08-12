namespace UBookIt.Core.Availability;

/// <summary>A half-open [StartUtc, EndUtc) interval of absolute time.</summary>
public readonly record struct UtcInterval(DateTimeOffset StartUtc, DateTimeOffset EndUtc)
{
    public TimeSpan Duration => EndUtc - StartUtc;
}

/// <summary>
/// A projected bookable start. Slots are computed, never persisted.
/// </summary>
public readonly record struct Slot(DateTimeOffset StartUtc, TimeSpan Duration)
{
    public DateTimeOffset EndUtc => StartUtc + Duration;
}

/// <summary>
/// A projected start together with how long may be booked from it: the shortest
/// permitted length and the longest that still fits before the resource next
/// becomes unavailable. Both bounds are granularity multiples, so every
/// multiple between them is bookable from this start. Computed, never persisted.
/// </summary>
public readonly record struct BookableStart(
    DateTimeOffset StartUtc, TimeSpan MinDuration, TimeSpan MaxDuration)
{
    /// <summary>True when only one length may be booked from this start.</summary>
    public bool IsSingleLength => MinDuration == MaxDuration;

    /// <summary>Whether a length may be booked from this start.</summary>
    public bool Admits(TimeSpan duration) => duration >= MinDuration && duration <= MaxDuration;
}
