using UBookIt.Core.Availability;

namespace UBookIt.Core.Bookings;

/// <summary>
/// The terms a placement is made under, according to <i>who is placing</i>: the two
/// <em>policy</em> rules of the pipeline — lead time (rule 5) and horizon (rule 6) — plus
/// whether the site's approval setting binds it. Every other rule is a property of the
/// resource or of the calendar and reads none of these.
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
/// That operation has since arrived and did exactly that.
/// </para>
/// <para>
/// <b>A visitor's terms are per resource; an operator's are one value for the whole placement.</b>
/// A visitor's lead and horizon <i>are</i> each resource's own configuration, so there is no
/// single visitor value a caller could hand to a multi-resource placement — which is why the
/// pipeline resolves them per resource, and why only the operator case has something to pass in.
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
    private PlacementTerms(TimeSpan leadTime, int? horizonDays, bool approvalApplies)
    {
        LeadTime = leadTime;
        HorizonDays = horizonDays;
        ApprovalApplies = approvalApplies;
    }

    /// <summary>The minimum notice a start must give, measured from now.</summary>
    public TimeSpan LeadTime { get; }

    /// <summary>
    /// How many days ahead a start may be, or <c>null</c> for no limit.
    /// </summary>
    public int? HorizonDays { get; }

    /// <summary>
    /// Whether the site's approval setting binds this placement: <c>true</c> for a visitor,
    /// <c>false</c> for an operator, whose booking is <c>Confirmed</c> whatever the site's
    /// <c>AutoConfirm</c> says.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Approval is the third waiver, and it lives here for the reason the other two do.</b>
    /// The setting exists so that a stranger's request can be reviewed before the site's time is
    /// committed. An operator taking a booking at the desk has reviewed it by taking it, and
    /// leaving it <c>Requested</c> would ask them to approve their own action — a queue item
    /// nobody is waiting on, which the booker's message would nonetheless describe as awaiting a
    /// decision.
    /// </para>
    /// <para>
    /// <b>It is a member of the terms rather than a decision at the entry point</b> so that the
    /// single site that decides what a new booking IS keeps deciding it. Two deciders is how the
    /// direct and the service paths would come to disagree about the same request, which is the
    /// exact thing that site was created to prevent.
    /// </para>
    /// <para>
    /// <b>Direct bookability is deliberately NOT a member here</b>, though it is equally waived
    /// for an operator. That rule is not evaluated on these terms; it is not reached at all,
    /// because the operator's entry point does not contain it. A member for it would state the
    /// rule in a value and enforce it in an overload — two places, free to disagree — and would
    /// reintroduce the "this is a direct booking" flag that placement's own design forbids. The
    /// absence is asserted by a test, so it is guarded rather than merely intended.
    /// </para>
    /// </remarks>
    public bool ApprovalApplies { get; }

    /// <summary>
    /// The terms a visitor places under: the resource's own configured lead time and horizon,
    /// exactly as every placement has always been evaluated.
    /// </summary>
    public static PlacementTerms Visitor(BookingConstraints constraints)
    {
        ArgumentNullException.ThrowIfNull(constraints);
        return new PlacementTerms(constraints.LeadTime, constraints.HorizonDays, approvalApplies: true);
    }

    /// <summary>
    /// The terms an operator places under: a lead of zero — the start must not have passed —
    /// no horizon, and the site's approval setting waived.
    /// </summary>
    public static PlacementTerms Operator { get; } =
        new(TimeSpan.Zero, horizonDays: null, approvalApplies: false);
}
