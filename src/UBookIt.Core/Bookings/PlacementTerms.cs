using UBookIt.Core.Availability;

namespace UBookIt.Core.Bookings;

/// <summary>
/// The terms the two <em>policy</em> rules of the placement pipeline — lead time (rule 5) and
/// horizon (rule 6) — are evaluated on. Every other rule is a property of the resource or of
/// the calendar and reads neither of these.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a value and not a flag.</b> Lead time and horizon exist to police visitors: a site
/// does not want a stranger booking a room for ten minutes from now, or for three years hence.
/// An operator moving a booking closer in because somebody telephoned is the person the site
/// trusts to decide that, and a rule that refused them would be protecting the site from its
/// own staff. So there are two sets of terms, and which set applies is a property of <i>who is
/// placing</i>, not of any one operation. Naming that as a value means the next operator-side
/// operation — recording a booking on somebody's behalf — is a one-line caller of
/// <see cref="Operator"/>, and the rule bodies stay identical for both callers: a test that the
/// visitor path is unchanged is a test that the two constructors differ and nothing else does.
/// </para>
/// <para>
/// <b>Operator terms are a lead of zero, not an absent rule.</b> Zero lead is precisely "the
/// start has not yet passed", which is the one guard nobody can be trusted to waive — a booking
/// in the past holds time that cannot be used, and its end moves it into the retention sweep's
/// window. Evaluating the rule with zero keeps that guard, keeps its existing stable code, and
/// keeps the pipeline's ordering and reporting untouched. The horizon has no such residue and is
/// simply not applied.
/// </para>
/// </remarks>
public sealed record PlacementTerms
{
    private PlacementTerms(TimeSpan leadTime, int? horizonDays)
    {
        LeadTime = leadTime;
        HorizonDays = horizonDays;
    }

    /// <summary>The minimum notice a start must give, measured from now.</summary>
    public TimeSpan LeadTime { get; }

    /// <summary>
    /// How many days ahead a start may be, or <c>null</c> for no limit.
    /// </summary>
    public int? HorizonDays { get; }

    /// <summary>
    /// The terms a visitor places under: the resource's own configured lead time and horizon,
    /// exactly as every placement has always been evaluated.
    /// </summary>
    public static PlacementTerms Visitor(BookingConstraints constraints)
    {
        ArgumentNullException.ThrowIfNull(constraints);
        return new PlacementTerms(constraints.LeadTime, constraints.HorizonDays);
    }

    /// <summary>
    /// The terms an operator places under: a lead of zero — the start must not have passed —
    /// and no horizon.
    /// </summary>
    public static PlacementTerms Operator { get; } = new(TimeSpan.Zero, horizonDays: null);
}
