namespace UBookIt.Core.Availability;

/// <summary>
/// Granularity arithmetic shared by duration resolution and slot projection.
/// All callers pass positive values, so integer division truncates towards
/// zero and is therefore a floor.
/// </summary>
internal static class DurationMath
{
    internal static TimeSpan MaxOf(TimeSpan a, TimeSpan b) => a > b ? a : b;

    internal static TimeSpan MinOf(TimeSpan a, TimeSpan b) => a < b ? a : b;

    /// <summary>The smallest multiple of <paramref name="step"/> that is at least <paramref name="value"/>.</summary>
    internal static TimeSpan CeilTo(TimeSpan value, TimeSpan step)
        => TimeSpan.FromTicks((value.Ticks + step.Ticks - 1) / step.Ticks * step.Ticks);

    /// <summary>The largest multiple of <paramref name="step"/> that is at most <paramref name="value"/>.</summary>
    internal static TimeSpan FloorTo(TimeSpan value, TimeSpan step)
        => TimeSpan.FromTicks(value.Ticks / step.Ticks * step.Ticks);
}
