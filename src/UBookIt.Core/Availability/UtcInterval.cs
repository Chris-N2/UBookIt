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
