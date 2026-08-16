namespace UBookIt.Core.Services;

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
    /// One resource per slot, all distinct, or null when no such assignment
    /// exists.
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
    internal static Guid[]? TrySaturate(IReadOnlyList<IReadOnlyList<Guid>> slotCandidates)
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
            if (!TryAugment(slot, slotCandidates, filledBy, []))
            {
                // No augmenting path for this slot means no matching saturates the
                // slots at all — Kuhn's guarantee, not an artefact of the order
                // they were tried in.
                return null;
            }
        }

        var assignment = new Guid[slotCandidates.Count];

        foreach (var (resource, slot) in filledBy)
        {
            assignment[slot] = resource;
        }

        return assignment;
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

            if (TrySaturate(constrained) is { } assignment)
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
