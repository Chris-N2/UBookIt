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

    /// <summary>
    /// The coarsest grid on which two grids coincide — the step of the lengths
    /// both offer.
    /// <para>
    /// Computed as <c>a / gcd(a, b) * b</c>, dividing before multiplying so the
    /// intermediate cannot overflow where <c>a * b</c> would. Granularity has no
    /// upper bound in the domain (a separately-logged hazard), so this
    /// arithmetic has to be safe even though bounding the input is another
    /// change's job.
    /// </para>
    /// </summary>
    internal static TimeSpan Lcm(TimeSpan a, TimeSpan b)
        => TimeSpan.FromTicks(a.Ticks / Gcd(a.Ticks, b.Ticks) * b.Ticks);

    /// <summary>
    /// The finest grid both <paramref name="a"/> and <paramref name="b"/> are
    /// multiples of — and, by Bézout, the spacing of the offsets at which two
    /// grids stepped by them can coincide.
    /// </summary>
    internal static TimeSpan Gcd(TimeSpan a, TimeSpan b) => TimeSpan.FromTicks(Gcd(a.Ticks, b.Ticks));

    private static long Gcd(long a, long b)
    {
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }

        return a;
    }
}
