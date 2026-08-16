namespace UBookIt.Core.Services;

/// <summary>
/// Why no saturating assignment exists: a set of slots with fewer distinct
/// eligible resources between them than it has slots.
/// <para>
/// Hall's condition, stated as its own failure. A saturating matching exists
/// exactly when every set of slots has at least as many distinct eligible
/// resources between them as it has slots, so a failure <em>is</em> such a set —
/// there is no other way for one to fail, and no second search is needed to find
/// one (design D4).
/// </para>
/// <para>
/// The set carried here is <b>tight</b>: <c>|Resources| = |Slots| - 1</c>, short
/// by exactly one. That is the useful kind — it names the smallest group that
/// cannot be satisfied rather than restating the whole configuration — and it is
/// what the failed augmenting walk produces without being asked, since the walk
/// visits exactly the resources reachable from the unfilled slot.
/// </para>
/// <para>
/// In terms of <b>slots</b>, deliberately: this type is the algorithm's own
/// answer, and slots are its unit. Collapsing to roles is
/// <see cref="PoolSufficiency"/>'s job, at the Core boundary, because roles are
/// what a consumer can act on (design D1).
/// </para>
/// </summary>
/// <param name="Slots">
/// The indices, ascending, of the slots that cannot all be filled.
/// </param>
/// <param name="Resources">
/// Every resource eligible for any of those slots, ascending by id — the set's
/// neighbourhood, and nothing wider.
/// </param>
internal sealed record SlotDeficiency(IReadOnlyList<int> Slots, IReadOnlyList<Guid> Resources);

/// <summary>
/// The assignment, or the reason there is none. Exactly one of the two is
/// present.
/// <para>
/// A bare null used to be the whole answer, on the ground that inventing a
/// witness shape with no consumer is a liability. This change is the consumer:
/// both the configuration-time sufficiency report and the placement-time message
/// are the deficient set, asked over different graphs (design D2).
/// </para>
/// </summary>
internal sealed record SlotSaturation
{
    private SlotSaturation(Guid[]? assignment, SlotDeficiency? deficiency)
    {
        Assignment = assignment;
        Deficiency = deficiency;
    }

    /// <summary>One resource per slot, all distinct — or null when there is none.</summary>
    internal Guid[]? Assignment { get; }

    /// <summary>
    /// The slots that cannot all be filled and the resources they compete for —
    /// null exactly when <see cref="Assignment"/> is not.
    /// </summary>
    internal SlotDeficiency? Deficiency { get; }

    internal static SlotSaturation Assigned(Guid[] assignment) => new(assignment, null);

    internal static SlotSaturation Short(SlotDeficiency deficiency) => new(null, deficiency);
}

/// <summary>
/// Filling a service's role slots with <em>distinct</em> resources.
/// <para>
/// A role of count <c>N</c> contributes <c>N</c> interchangeable slots; a slot may
/// be filled by any resource eligible for its role; and a resource fills at most
/// one slot, because a booking cannot claim the same resource twice. A service is
/// fulfillable at an instant exactly when a matching saturating every slot exists
/// (design D1).
/// </para>
/// <para>
/// This matters only once pools overlap. With one role per resource type the pools
/// are disjoint and taking each slot's first free candidate is optimal, which is
/// why the previous restriction could get away with an independent choice per
/// role. Once two slots can draw on one pool, a greedy choice can consume the only
/// candidate a later slot had and report a service unavailable when it was
/// bookable — a wrong answer rather than a slow one.
/// </para>
/// <para>
/// Kept a pure function over slots and candidate id lists — no store, no clock, no
/// service — so the assignment can be tested from the problem rather than through
/// availability or placement, and so neither of those can grow its own variant of
/// the rule.
/// </para>
/// </summary>
internal static class SlotAssignment
{
    /// <summary>
    /// One resource per slot, all distinct — or the deficient set of slots that
    /// makes such an assignment impossible.
    /// <para>
    /// Augmenting paths (Kuhn's). Pools are small — tens, not thousands — the work
    /// is in memory over already-loaded candidates, and the complexity is
    /// <c>O(V·E)</c>. Hopcroft–Karp is asymptotically better and not worth its
    /// complexity at this size; if pools ever grow, this function is the seam
    /// (design D1).
    /// </para>
    /// <para>
    /// <b>Deterministic.</b> Slots are processed in the order given — role order,
    /// then slot index — and each slot tries candidates in the order given, which
    /// callers supply ascending by resource id, as ⑦-2 established. Identical
    /// inputs therefore yield an identical assignment, which is what lets a test
    /// assert <em>which</em> resources were chosen rather than only that some were.
    /// </para>
    /// </summary>
    /// <param name="slotCandidates">
    /// For each slot, the resources eligible to fill it, ascending by id.
    /// </param>
    internal static SlotSaturation TrySaturate(IReadOnlyList<IReadOnlyList<Guid>> slotCandidates)
    {
        ArgumentNullException.ThrowIfNull(slotCandidates);

        // Which slot each resource currently fills. Inverted at the end rather
        // than maintained in both directions: a resource fills at most one slot,
        // so this direction is the one that has to be a lookup — the augmenting
        // walk asks "is this resource taken, and by whom".
        var filledBy = new Dictionary<Guid, int>();

        for (var slot = 0; slot < slotCandidates.Count; slot++)
        {
            // Visited is per slot, not shared across slots: it stops the search
            // for *this* slot from revisiting a resource within one augmenting
            // walk. A resource rejected while filling an earlier slot may well be
            // the one that has to move for a later one.
            var reached = new HashSet<Guid>();

            if (!TryAugment(slot, slotCandidates, filledBy, reached))
            {
                // No augmenting path for this slot means no matching saturates the
                // slots at all — Kuhn's guarantee, not an artefact of the order
                // they were tried in.
                return SlotSaturation.Short(Deficiency(slot, filledBy, reached));
            }
        }

        var assignment = new Guid[slotCandidates.Count];

        foreach (var (resource, slot) in filledBy)
        {
            assignment[slot] = resource;
        }

        return SlotSaturation.Assigned(assignment);
    }

    /// <summary>
    /// The deficient set a failed walk has already computed (design D4).
    /// <para>
    /// <paramref name="reached"/> holds exactly the resources the walk could get
    /// to from <paramref name="unfilled"/> along alternating paths, and every one
    /// of them is filled: an unfilled candidate would have been taken outright
    /// rather than walked through, so the walk could not have failed. The set is
    /// therefore the unfilled slot plus the slots holding those resources, and its
    /// neighbourhood is <paramref name="reached"/> and nothing more — the walk
    /// enumerated every candidate of every slot it entered.
    /// </para>
    /// <para>
    /// It follows that <c>|reached| = |slots| - 1</c> exactly: one slot per reached
    /// resource, plus the one that could not be filled. Short by exactly one, which
    /// is the tight witness design D4 promises rather than a set that merely
    /// satisfies Hall's inequality.
    /// </para>
    /// <para>
    /// Both parts are ordered — slots ascending, resources by id — so the same
    /// input yields the same witness. The dictionary's own enumeration order is an
    /// implementation detail and would make the report depend on insertion history.
    /// </para>
    /// </summary>
    private static SlotDeficiency Deficiency(
        int unfilled, Dictionary<Guid, int> filledBy, HashSet<Guid> reached)
    {
        var slots = new List<int>(reached.Count + 1) { unfilled };

        foreach (var resource in reached)
        {
            slots.Add(filledBy[resource]);
        }

        slots.Sort();

        return new SlotDeficiency(slots, [.. reached.OrderBy(id => id)]);
    }

    /// <summary>
    /// A saturating assignment that includes <paramref name="required"/>, or null
    /// when none does.
    /// <para>
    /// A preferred resource names the booking rather than a role (design D4): a
    /// resource may be eligible for several slots, so "prefer this one" no longer
    /// identifies which slot it fills, and the assignment chooses. Each slot the
    /// resource is eligible for is tried in turn, with the resource pinned to that
    /// slot and removed from every other, so the result is a genuine assignment
    /// containing it rather than one patched afterwards.
    /// </para>
    /// <para>
    /// Deterministic in the same way <see cref="TrySaturate"/> is: the first slot
    /// in order that admits a saturating assignment wins.
    /// </para>
    /// <para>
    /// Returns a bare null rather than a deficient set, deliberately. Its failures
    /// are failures of the <em>constrained</em> graphs it built — one per slot the
    /// resource could have filled — and a witness drawn from one of those would
    /// describe a question the caller never asked, since preference falling through
    /// is not a shortage of resources. The caller that wants a reason asks
    /// <see cref="TrySaturate"/> over the unconstrained graph.
    /// </para>
    /// </summary>
    internal static Guid[]? TrySaturateIncluding(
        IReadOnlyList<IReadOnlyList<Guid>> slotCandidates, Guid required)
    {
        ArgumentNullException.ThrowIfNull(slotCandidates);

        for (var pinned = 0; pinned < slotCandidates.Count; pinned++)
        {
            if (!slotCandidates[pinned].Contains(required))
            {
                continue;
            }

            var constrained = new IReadOnlyList<Guid>[slotCandidates.Count];

            for (var slot = 0; slot < slotCandidates.Count; slot++)
            {
                constrained[slot] = slot == pinned
                    ? [required]
                    : [.. slotCandidates[slot].Where(id => id != required)];
            }

            if (TrySaturate(constrained).Assignment is { } assignment)
            {
                return assignment;
            }
        }

        return null;
    }

    /// <summary>
    /// Tries to fill <paramref name="slot"/>, displacing resources already used
    /// where the slot that holds one can be re-filled from elsewhere.
    /// <para>
    /// The displacement is the whole point: it is what a greedy walk cannot do,
    /// and what stops the resource eligible for two slots being consumed by the
    /// slot that had an alternative.
    /// </para>
    /// <para>
    /// <b>A free candidate is taken before any displacement is attempted.</b>
    /// Kuhn's is correct either way — the search explores the same candidates
    /// whichever order the two cases are tried in — but taking the free one first
    /// makes the assignment agree with the obvious choice wherever a greedy one
    /// would have worked. Without it, two slots over the pool <c>{A, B}</c> resolve
    /// to <c>(B, A)</c>: the second slot displaces A from the first rather than
    /// taking the untouched B. That is a valid assignment and a strange one to
    /// read in a booking, and it would put the wrong resource first in exactly the
    /// single-role case ⑦-2 promises attempts the lowest id.
    /// </para>
    /// <para>
    /// <paramref name="visited"/> is read back by the caller when this returns
    /// false: it is then exactly the reachable set, which is the deficient set's
    /// neighbourhood. That is why the witness costs nothing — the search has
    /// already done the work, and enumerating subsets to find one would be
    /// exponential (design D4).
    /// </para>
    /// </summary>
    private static bool TryAugment(
        int slot,
        IReadOnlyList<IReadOnlyList<Guid>> slotCandidates,
        Dictionary<Guid, int> filledBy,
        HashSet<Guid> visited)
    {
        foreach (var resource in slotCandidates[slot])
        {
            if (visited.Contains(resource) || filledBy.ContainsKey(resource))
            {
                continue;
            }

            visited.Add(resource);
            filledBy[resource] = slot;
            return true;
        }

        foreach (var resource in slotCandidates[slot])
        {
            if (!visited.Add(resource))
            {
                continue;
            }

            // Only filled resources remain: an unfilled one would have been taken
            // above unless an outer walk had already visited it, and an outer walk
            // in this pass has itself passed over every unfilled candidate.
            if (filledBy.TryGetValue(resource, out var holder)
                && TryAugment(holder, slotCandidates, filledBy, visited))
            {
                filledBy[resource] = slot;
                return true;
            }
        }

        return false;
    }
}
