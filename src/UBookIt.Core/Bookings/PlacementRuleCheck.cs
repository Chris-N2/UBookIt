using UBookIt.Core.Common;
using UBookIt.Core.Resources;

namespace UBookIt.Core.Bookings;

/// <summary>
/// The placement rules evaluated under explicit <see cref="PlacementTerms"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Internal, and that is the point.</b> <see cref="IBookingService"/>'s public rule check is
/// the visitor's, deliberately: which terms a rule is evaluated on is decided by an operation in
/// this assembly, never chosen by a caller. Publishing a terms-taking check would let a host — or
/// the anonymous delivery endpoint — ask what an operator would be allowed, which is a question
/// no caller outside an operator operation has any business asking.
/// </para>
/// <para>
/// <b>It exists because a rule waived by the pipeline is otherwise re-imposed when a placement
/// is explained.</b> Service placement asks this check, after an attempt has failed, which
/// candidates to condemn and what an all-fail should report — and <c>lead-time</c> and
/// <c>horizon</c> count as deterministic refusals there. Asked on a visitor's terms during an
/// operator's placement, a service whose only candidate is merely BUSY is reported as
/// <c>service-unavailable</c> rather than <c>conflict</c>: the operator is told the service
/// cannot be booked at that time, and not to retry, because of a rule they are not subject to.
/// </para>
/// <para>
/// <b>It is not a pre-filter, and an earlier version of this note said it was.</b> The success
/// path never reaches this check — which is why a test that only placed a booking inside the
/// lead time passed with the terms wired wrong. The guard that measures it is
/// <c>ServicePlaceOnBehalfTests.A_busy_candidate_inside_lead_time_is_reported_as_a_conflict</c>.
/// </para>
/// </remarks>
internal interface IPlacementRuleCheck
{
    DomainResult CheckPlacementRules(
        Resource resource, DateTimeOffset start, TimeSpan duration, PlacementTerms terms);
}
