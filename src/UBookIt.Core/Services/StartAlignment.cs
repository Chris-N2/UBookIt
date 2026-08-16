using UBookIt.Core.Availability;
using UBookIt.Core.Resources;

namespace UBookIt.Core.Services;

/// <summary>
/// Whether two arithmetic start grids can ever coincide.
/// <para>
/// A resource's starts advance in granularity steps from a window start, so two
/// windows anchored at <c>a₁</c>, <c>a₂</c> and stepped by <c>s₁</c>, <c>s₂</c>
/// share an instant exactly when <c>a₁ + k·s₁ = a₂ + m·s₂</c> has an integer
/// solution — which, by Bézout, is exactly when <c>gcd(s₁, s₂)</c> divides
/// <c>a₂ − a₁</c> (design D3).
/// </para>
/// <para>
/// Kept a pure function over two (start, step) pairs so the arithmetic can be
/// tested without a service, a store or a clock — and so nothing in it can come
/// to depend on the booking calendar, which is what would make a structural
/// fault flicker.
/// </para>
/// </summary>
internal static class StartGrid
{
    /// <summary>
    /// Whether the two grids share an instant on the infinite grids. True is
    /// necessary for a shared start and nowhere near sufficient: the solution
    /// may fall outside both windows, and neither resource need be free there.
    /// False is exact — no shared start can exist, ever (design D1).
    /// </summary>
    internal static bool CanMeet(TimeOnly firstStart, TimeSpan firstStep, TimeOnly secondStart, TimeSpan secondStep)
    {
        // Ticks rather than TimeOnly subtraction: `TimeOnly - TimeOnly` measures
        // elapsed clock time and wraps a negative difference by 24 hours, which
        // changes the residue for any step that does not divide a day — a 7-minute
        // granularity would answer a different question than the one asked.
        var offset = firstStart.Ticks - secondStart.Ticks;

        return offset % DurationMath.Gcd(firstStep, secondStep).Ticks == 0;
    }
}

/// <summary>
/// One side of a reported clash: the role, the resource that fills it, and the
/// two numbers an editor changes to fix it.
/// <para>
/// The window start and granularity travel with the resource because the fix is
/// to edit a <em>resource</em> — its opening time or its step size — and a report
/// naming only the service would send an editor to the wrong screen.
/// </para>
/// </summary>
public sealed record MisalignedRole(
    ServiceRole Role, Resource Resource, TimeOnly WindowStart, TimeSpan Granularity);

/// <summary>Two roles whose start grids can never coincide.</summary>
public sealed record RoleMisalignment(MisalignedRole First, MisalignedRole Second);

/// <summary>
/// Whether a service's roles can share a bookable start <em>at all</em>.
/// <para>
/// The claim is one-directional (design D1): this may report that no start can
/// ever exist, and never that one does. Sharing a grid instant is necessary for
/// a bookable start and not sufficient — the instant must also fall in free time
/// on every resource, satisfy each lead time and horizon, and admit a length
/// they all permit, none of which is evaluated here. Reporting alignment
/// positively would assert availability, which the configuration surfaces are
/// forbidden to do.
/// </para>
/// <para>
/// Computed over the resources' configured <b>open windows</b>, not their free
/// intervals: placement aligns a start to its open window's start and a booking's
/// length is a multiple of the resource's granularity, so every free-interval
/// start — and therefore every candidate start — lies on the open-window grid
/// (design D2). Computing over free intervals instead would make a structural
/// fault appear and disappear as bookings came and went.
/// </para>
/// <para>
/// That superset argument assumes each booking was placed under the configuration
/// now in force. A booking made <em>before</em> an opening-hours edit can end off
/// the new grid, and the free interval starting at its end is then a start the
/// window grid does not contain — so for as long as that booking survives, a pair
/// reported here may still have a shared start. The report is nonetheless right
/// about the <em>configuration</em>, which is what it describes: once the stale
/// booking clears, no such start remains. Reporting the configuration is the
/// deliberate choice over falling silent, which would couple the diagnostic to
/// the booking calendar and reintroduce exactly the flicker D2 exists to prevent.
/// </para>
/// </summary>
public static class StartAlignment
{
    /// <summary>
    /// The first pair of roles whose candidates can never share a start, or null
    /// when none was found.
    /// <para>
    /// Pairwise, and that is complete rather than a sample: a system of
    /// congruences is solvable exactly when it is solvable pairwise, so one
    /// clashing pair explains the whole service (design D3).
    /// </para>
    /// <para>
    /// Takes the resolved pools rather than a service id, so the candidates are
    /// the ones the booking path acts on — projected from the same resolution,
    /// never filtered a second time (⑧a design D1).
    /// </para>
    /// </summary>
    public static RoleMisalignment? FindMisalignment(IReadOnlyList<RoleCandidates> pools)
    {
        ArgumentNullException.ThrowIfNull(pools);

        for (var first = 0; first < pools.Count; first++)
        {
            for (var second = first + 1; second < pools.Count; second++)
            {
                if (Compare(pools[first], pools[second]) is { } misalignment)
                {
                    return misalignment;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Two roles, compared candidate by candidate.
    /// <para>
    /// A role is a pool, not a resource, so the service is fulfillable as soon as
    /// <em>some</em> candidate of each aligns: one aligning pairing anywhere
    /// clears the pair. Only when no candidate of one shares a grid with any
    /// candidate of the other, on any day both are open, is there a clash
    /// (design D4).
    /// </para>
    /// <para>
    /// The pairing reported is the first one compared, which is a truthful
    /// witness — those exact two windows really can never meet — and deterministic,
    /// because resolution orders each pool by resource id.
    /// </para>
    /// </summary>
    private static RoleMisalignment? Compare(RoleCandidates first, RoleCandidates second)
    {
        RoleMisalignment? witness = null;

        foreach (var left in first.Candidates)
        {
            foreach (var right in second.Candidates)
            {
                if (left.ResourceId == right.ResourceId)
                {
                    // A resource compared against itself. Once two roles may draw
                    // on one pool the same resource appears in both, and it
                    // trivially shares its own grid — so without this the report is
                    // silenced by an assignment that can never occur, since a
                    // booking cannot claim one resource twice (design D7).
                    //
                    // Skipped *before* the daylight-saving guard, not after: a
                    // self-pairing has gcd(g, g) = g, so a granularity that does not
                    // divide an hour would clear the whole role pair by that route
                    // instead — the same silence through a different door.
                    //
                    // Deliberately narrow. Same-type role pairs are NOT skipped
                    // wholesale: two roles of one type requiring different
                    // capabilities can draw on disjoint sets of resources, so a
                    // genuine permanent misalignment between them remains possible
                    // and must still be reported.
                    continue;
                }

                if (!WallClockOffsetSettlesIt(left.Granularity, right.Granularity))
                {
                    // The wall-clock offset cannot settle this pair, so nothing
                    // about it may be reported — and one unsettled pairing is
                    // enough to clear the whole role pair, exactly as an aligning
                    // one is. Silence is the permitted direction (design D1).
                    return null;
                }

                foreach (var (leftWindow, rightWindow) in SharedDateWindows(left.Resource, right.Resource))
                {
                    if (StartGrid.CanMeet(
                        leftWindow.Start, left.Granularity, rightWindow.Start, right.Granularity))
                    {
                        // Early exit on the common case: one aligning pairing is
                        // the whole answer, and the loop below it need never run.
                        return null;
                    }

                    witness ??= new RoleMisalignment(
                        new MisalignedRole(first.Role, left.Resource, leftWindow.Start, left.Granularity),
                        new MisalignedRole(second.Role, right.Resource, rightWindow.Start, right.Granularity));
                }
            }
        }

        // Null when nothing was ever compared — an empty pool, or resources that
        // are never open. That is deliberately not a misalignment: there is no
        // pair of windows to name, and "these two can never meet" would be a
        // strange way to say that one of them is closed or unfilled. The
        // resolution chain already reports an empty pool.
        return witness;
    }

    /// <summary>
    /// Whether the wall-clock offset between two windows decides the question for
    /// <em>every</em> date, rather than only for the dates carrying no
    /// daylight-saving transition.
    /// <para>
    /// Design D5 argues that two windows in one zone shift together across a DST
    /// boundary, so the offset between them is stable. That holds for windows on
    /// the same side of the transition and fails for windows straddling it: on
    /// that one date the later window's UTC instant moves and the real offset
    /// differs from the wall-clock offset by the transition's size.
    /// </para>
    /// <para>
    /// Divisibility is blind to a shift that the divisor divides, so whenever
    /// <c>gcd(s₁, s₂)</c> divides an hour the wall-clock offset gives the same
    /// verdict as the real one and the arithmetic is exact. Every granularity in
    /// practical use — 5, 10, 15, 20, 30, 60 minutes — has a gcd that does. When
    /// it does not, this stays silent rather than risk accusing a configuration
    /// that works on the transition date, which is the asymmetry design D1
    /// requires: being conservative is allowed, accusing wrongly is not.
    /// </para>
    /// <para>
    /// Checked against an hour rather than against the site zone's actual
    /// transitions, so the check needs no zone and no date sweep — the two things
    /// D5 rejected because they make a structural claim depend on when it was
    /// asked. The residue is a zone whose transition is not a whole hour (Lord
    /// Howe Island shifts by 30 minutes) paired with a gcd that divides an hour
    /// but not half of one; recorded as a known limit rather than silently
    /// accepted.
    /// </para>
    /// </summary>
    private static bool WallClockOffsetSettlesIt(TimeSpan firstStep, TimeSpan secondStep)
        => TimeSpan.FromHours(1).Ticks % DurationMath.Gcd(firstStep, secondStep).Ticks == 0;

    /// <summary>
    /// Every pair of windows the two resources can hold on one and the same local
    /// date, collapsed to a finite enumeration.
    /// <para>
    /// The weekly pattern contributes one pairing per day of week, because that
    /// pairing recurs on every date of that day; a date exception contributes the
    /// windows actually in effect on its own date, for both resources. A closure
    /// simply has no windows, so an exception that closes a date removes it from
    /// consideration without a special case (design D5).
    /// </para>
    /// <para>
    /// Compared in wall-clock terms on the same local date, never across dates or
    /// over a swept UTC horizon. Both windows belong to the same site zone, so a
    /// daylight-saving transition moves them together and the offset between them
    /// is stable — which is what keeps a structural claim independent of when it
    /// was asked.
    /// </para>
    /// </summary>
    private static IEnumerable<(DayWindow First, DayWindow Second)> SharedDateWindows(
        Resource first, Resource second)
    {
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            foreach (var pairing in Pairings(
                first.Availability.OpenHours.WindowsFor(day),
                second.Availability.OpenHours.WindowsFor(day)))
            {
                yield return pairing;
            }
        }

        var exceptionDates = first.Availability.Exceptions
            .Concat(second.Availability.Exceptions)
            .Select(e => e.Date)
            .Distinct()
            .OrderBy(date => date);

        foreach (var date in exceptionDates)
        {
            foreach (var pairing in Pairings(
                first.Availability.EffectiveWindows(date),
                second.Availability.EffectiveWindows(date)))
            {
                yield return pairing;
            }
        }
    }

    private static IEnumerable<(DayWindow First, DayWindow Second)> Pairings(
        IReadOnlyList<DayWindow> first, IReadOnlyList<DayWindow> second)
        => from left in first
           from right in second
           select (left, right);
}
