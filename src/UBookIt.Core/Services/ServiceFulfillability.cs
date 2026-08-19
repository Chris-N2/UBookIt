using UBookIt.Core.Availability;

namespace UBookIt.Core.Services;

/// <summary>
/// Whether a service can <b>ever</b> be fulfilled as configured, answered from
/// all three structural questions together, as a pure function of its resolved
/// role candidates.
/// <para>
/// One function, so every surface that reports a service permanently unbookable
/// derives its answer from the same computation (design D6). The in-process
/// booking flow and the public availability read both ask it: a second
/// implementation of the rule in a mapping layer would be free to disagree with
/// the first in precisely the case it exists to detect — the fault ⑧a design D1
/// and ⑨-1's classifier seam both exist to prevent.
/// </para>
/// <para>
/// <b>Pure.</b> No store, no clock, no HTTP context, so it can be attacked
/// directly rather than through a host, and so no consumer has to be
/// constructible in order to ask the question.
/// </para>
/// <para>
/// <b>One-directional.</b> Its three questions concern pools, grids and duration
/// ranges; none of them consults the booking calendar. It may report that a
/// service can never be fulfilled; it may <em>never</em> report that a service is
/// available, or that it will be later. A service this function calls fulfillable
/// may still have no availability today, and the absence of the finding means
/// "not structurally impossible", never "try tomorrow and it will work".
/// </para>
/// </summary>
public static class ServiceFulfillability
{
    /// <summary>
    /// The stable machine-readable code a public read carries to say an empty
    /// answer is permanent (⑨-1a's deferred reason code).
    /// <para>
    /// It discloses nothing: not the role, the resource type, the required
    /// capability, the count, nor how many resources exist. The backoffice
    /// diagnostics that name those are for the person who can fix them, and the
    /// response carrying this is anonymous.
    /// </para>
    /// </summary>
    public const string NotFulfillableCode = "service-not-fulfillable";

    /// <summary>
    /// Whether the service can never be fulfilled as configured, from all three
    /// structural questions.
    /// <para>
    /// Answering only the first would leave the other two reporting ordinary
    /// unavailability forever, which is the exact confusion the distinction
    /// exists to remove: a permanently misaligned pair of grids, and a service no
    /// single length can satisfy, are both perfectly healthy pools that can never
    /// produce a start.
    /// </para>
    /// </summary>
    public static bool IsPermanentlyUnfulfillable(IReadOnlyList<RoleCandidates> pools)
    {
        ArgumentNullException.ThrowIfNull(pools);

        // The roles cannot be filled at once by distinct resources — which
        // subsumes a role with no eligible resource at all: a slot with no
        // candidates has no saturating assignment, so asking separately would be
        // two answers to one question.
        return PoolSufficiency.FindShortfall(pools) is not null

            // Two roles whose start grids can never coincide: a correctly
            // configured service that is permanently unbookable, and the case
            // "no times available" describes most misleadingly of all.
            || StartAlignment.FindMisalignment(pools) is not null

            // No length every role can provide. The service can be fulfilled by
            // nobody for any duration, whatever the calendar says.
            || CommonLengthMinutes(pools).Count == 0;
    }

    /// <summary>
    /// Every whole-minute length that <em>some</em> candidate of <em>every</em>
    /// role can provide, ascending.
    /// <para>
    /// An <b>affordance, not a trust boundary</b>. A length surviving this
    /// intersection is one each role could provide separately; whether distinct
    /// resources can provide it simultaneously is an assignment question, asked
    /// per start by the availability query and again by placement. So a control
    /// built from this can offer a length that no start admits — the times list is
    /// then empty and says so — but it can never offer one the service could not
    /// possibly book, which is what a control is for.
    /// </para>
    /// <para>
    /// An empty result is the third structural question's "no": no length exists
    /// that every role can provide.
    /// </para>
    /// </summary>
    public static IReadOnlyList<int> CommonLengthMinutes(IReadOnlyList<RoleCandidates> pools)
    {
        ArgumentNullException.ThrowIfNull(pools);

        if (pools.Count == 0)
        {
            return [];
        }

        HashSet<int>? shared = null;

        foreach (var pool in pools)
        {
            var offered = pool.Candidates
                .SelectMany(candidate => LengthGrid.Minutes(
                    (int)candidate.Range.Min.TotalMinutes,
                    (int)candidate.Range.Max.TotalMinutes,
                    (int)candidate.Granularity.TotalMinutes))
                .ToHashSet();

            if (shared is null)
            {
                shared = offered;
            }
            else
            {
                shared.IntersectWith(offered);
            }
        }

        return [.. shared!.Order()];
    }
}
