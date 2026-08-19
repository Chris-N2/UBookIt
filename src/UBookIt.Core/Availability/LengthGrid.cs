namespace UBookIt.Core.Availability;

/// <summary>
/// The whole-minute lengths on an arithmetic grid.
/// <para>
/// In Core rather than in the front end because the question "does any length
/// exist that every role can provide" is a <b>domain</b> question, and it is one
/// of the three the structural-unfulfillability check asks
/// (<see cref="Services.ServiceFulfillability"/>). It used to live beside the
/// booking form, which is why the delivery API could not reach it and why
/// publishing the same answer would have meant a second implementation of the
/// rule (design D6).
/// </para>
/// </summary>
public static class LengthGrid
{
    /// <summary>
    /// Every whole-minute length on the grid, from a minimum to a maximum
    /// inclusive, ascending.
    /// <para>
    /// A sub-minute granularity truncates to a zero step, which would loop
    /// forever. The domain permits it — it only requires the bounds to be exact
    /// multiples — and callers hand this values that came from a domain
    /// aggregate, so it guards rather than assumes.
    /// </para>
    /// </summary>
    public static IReadOnlyList<int> Minutes(int minMinutes, int maxMinutes, int stepMinutes)
    {
        if (stepMinutes <= 0)
        {
            return [];
        }

        var lengths = new List<int>();
        for (var minutes = minMinutes; minutes <= maxMinutes; minutes += stepMinutes)
        {
            lengths.Add(minutes);
        }

        return lengths;
    }
}
