using UBookIt.Core.Services;

namespace UBookIt.Tests;

/// <summary>
/// The assignment itself (multi-role-matching design D1), tested from the problem
/// rather than through availability or placement.
/// <para>
/// Every fixture here states a slot/candidate structure and the answer the
/// structure forces, so an implementation that merely looks plausible fails. The
/// cases that matter are the ones a greedy walk gets wrong: the shared resource
/// that has to be displaced, and the count that cannot be met by one resource
/// counted twice.
/// </para>
/// <para>
/// <b>Why one fixture uses incomparable pools</b> (task 1.4). Pools for one
/// resource type are nested exactly when the capability sets are comparable, and
/// the shape a real configuration usually takes — <c>{cert-x}</c> against
/// <c>{}</c> — is nested. Greedy assignment is optimal over nested pools, so a
/// suite built only from realistic configurations would pass a greedy
/// implementation and demonstrate nothing about the assignment at all. The
/// three-slot fixture below draws <c>{cert-x}</c> against <c>{welsh}</c>, where
/// neither pool contains the other, and defeats both first-fit and
/// most-constrained-first.
/// </para>
/// </summary>
public class SlotAssignmentTests
{
    private static readonly Guid R1 = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid R2 = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid R3 = new("00000000-0000-0000-0000-000000000003");
    private static readonly Guid R4 = new("00000000-0000-0000-0000-000000000004");

    /// <summary>
    /// One slot's candidates, in the ascending-id order every caller supplies —
    /// stated here rather than assumed, so a fixture cannot accidentally hand the
    /// assignment an order production never produces.
    /// </summary>
    private static IReadOnlyList<Guid> Pool(params Guid[] ids) => [.. ids.OrderBy(id => id)];

    private static Guid[]? Saturate(params IReadOnlyList<Guid>[] slots)
        => SlotAssignment.TrySaturate(slots).Assignment;

    /// <summary>The assignment, asserted to exist, so the cases below can name it.</summary>
    private static Guid[] Assigned(params IReadOnlyList<Guid>[] slots)
    {
        var saturation = SlotAssignment.TrySaturate(slots);

        Assert.NotNull(saturation.Assignment);

        // The two are exclusive by construction, and a test that only ever read
        // one of them would not notice a failure reporting both.
        Assert.Null(saturation.Deficiency);
        return saturation.Assignment;
    }

    /// <summary>The deficient set, asserted to exist, so the cases below can name it.</summary>
    private static SlotDeficiency Deficient(params IReadOnlyList<Guid>[] slots)
    {
        var saturation = SlotAssignment.TrySaturate(slots);

        Assert.Null(saturation.Assignment);
        Assert.NotNull(saturation.Deficiency);
        return saturation.Deficiency;
    }

    private static Guid[] AssignedIncluding(Guid required, params IReadOnlyList<Guid>[] slots)
    {
        var assignment = SlotAssignment.TrySaturateIncluding(slots, required);

        Assert.NotNull(assignment);
        return assignment;
    }

    [Fact]
    public void A_single_slot_takes_the_lowest_id_candidate()
    {
        // The order ⑦-2 established, unchanged: the assignment does not reorder
        // what it is given.
        Assert.Equal([R1], Assigned(Pool(R1, R2)));
    }

    [Fact]
    public void Disjoint_pools_resolve_to_the_independent_choice()
    {
        // Every role naming a distinct type is the case that exists today: the
        // pools cannot overlap, so the assignment is each slot's own first
        // candidate and nothing is displaced. This is the property design D3
        // rests on.
        Assert.Equal([R1, R3], Assigned(Pool(R1, R2), Pool(R3, R4)));
    }

    [Fact]
    public void A_slot_with_no_candidate_is_not_saturable()
    {
        Assert.Null(Saturate(Pool(R1), Pool()));
    }

    [Fact]
    public void A_pool_larger_than_the_slots_still_assigns_distinct_resources()
    {
        Assert.Equal([R1, R2], Assigned(Pool(R1, R2, R3, R4), Pool(R1, R2, R3, R4)));
    }

    [Fact]
    public void A_count_of_two_is_not_met_by_one_resource_counted_twice()
    {
        // Two slots of one role, one free candidate between them. The distinctness
        // constraint is the whole answer here: without it this returns [R1, R1],
        // which `Booking.Create` throws on rather than refusing.
        Assert.Null(Saturate(Pool(R1), Pool(R1)));
    }

    [Fact]
    public void A_count_of_two_against_two_free_candidates_claims_both()
    {
        Assert.Equal([R1, R2], Assigned(Pool(R1, R2), Pool(R1, R2)));
    }

    [Fact]
    public void The_resource_eligible_for_both_slots_goes_to_the_slot_with_no_alternative()
    {
        // First-fit in slot order gives R1 to the first slot and strands the
        // second, reporting unavailable a structure that is plainly satisfiable.
        // The shared resource has to be displaced.
        Assert.Equal([R2, R1], Assigned(Pool(R1, R2), Pool(R1)));
    }

    [Fact]
    public void Incomparable_pools_defeat_a_greedy_walk_in_either_order()
    {
        // Three `therapist` resources: R1 holds both `cert-x` and `welsh`, R2
        // holds `cert-x` alone, R3 holds `welsh` alone.
        //
        //   slot 0 — role requiring {cert-x}          → {R1, R2}
        //   slot 1 — role requiring {welsh}, count 2  → {R1, R3}
        //   slot 2 — the same role's second slot      → {R1, R3}
        //
        // Neither pool contains the other, which is what makes this fixture worth
        // more than a nested one. First-fit in slot order gives R1 to slot 0 and
        // strands slot 2. Most-constrained-first does no better: all three slots
        // start with two candidates, and recomputing after each choice still
        // begins with slot 0. Only displacement finds the answer.
        var assignment = Assigned(Pool(R1, R2), Pool(R1, R3), Pool(R1, R3));

        Assert.Equal([R2, R3, R1], assignment);
        Assert.Equal(3, assignment.Distinct().Count());
    }

    [Fact]
    public void The_same_input_yields_the_same_assignment()
    {
        // Determinism is what lets placement be reproducible and lets the tests
        // above name resources rather than count them.
        var first = Saturate(Pool(R1, R2), Pool(R1, R3), Pool(R1, R3));
        var second = Saturate(Pool(R1, R2), Pool(R1, R3), Pool(R1, R3));

        Assert.Equal(first, second);
    }

    [Fact]
    public void An_assignment_including_a_preferred_resource_places_it_where_it_fits()
    {
        // R1 is eligible for both slots; slot 1 has no other candidate. Pinning R1
        // to slot 0 leaves slot 1 empty, so the pin moves — the preference names
        // the booking, not a slot (design D4).
        Assert.Equal([R2, R1], AssignedIncluding(R1, Pool(R1, R2), Pool(R1)));
    }

    [Fact]
    public void A_preference_is_reported_as_none_only_when_nothing_saturates()
    {
        // This case asserts less than it looks like it does, and the reason is worth
        // stating so nobody strengthens it by mistake.
        //
        // Whenever a saturating assignment exists AND the pinned resource is
        // eligible for some slot, an assignment *containing* it always exists: pin
        // it to that slot, and the original matching restricted to the remaining
        // slots avoids it, because a matching uses each resource at most once. So
        // TrySaturateIncluding returns null exactly when TrySaturate does — this
        // cannot be a test of the preference falling through.
        //
        // Here nothing saturates at all: slot 1 needs R1 and slot 2 needs R2, so
        // slot 0 has nothing left, with or without a preference.
        Assert.Null(SlotAssignment.TrySaturateIncluding([Pool(R1, R2), Pool(R1), Pool(R2)], R1));
        Assert.Null(SlotAssignment.TrySaturate([Pool(R1, R2), Pool(R1), Pool(R2)]).Assignment);

        // A pin that cannot be honoured while some assignment still can is not
        // reachable here, and that is the whole point: it arises one level up, where
        // the claims pre-filter removes the pinned resource from the pool before the
        // assignment sees it. Covered by
        // MultiRolePlacementTests.Spec_scenario_a_pin_that_cannot_be_honoured_fails_rather_than_substituting.
        //
        // This property is what design D4 of `resource-pin` rests on: a pin has no
        // structural failure mode, so the only reason one fails is that the resource
        // is out of the graph.
    }

    [Fact]
    public void The_deficient_set_names_the_slots_that_cannot_be_filled()
    {
        // Two slots of one role over one candidate. Both slots are in the set and
        // the single resource is its whole neighbourhood.
        var deficiency = Deficient(Pool(R1), Pool(R1));

        Assert.Equal([0, 1], deficiency.Slots);
        Assert.Equal([R1], deficiency.Resources);
    }

    [Fact]
    public void A_slot_with_no_candidate_is_deficient_on_its_own()
    {
        var deficiency = Deficient(Pool(R1), Pool());

        Assert.Equal([1], deficiency.Slots);
        Assert.Empty(deficiency.Resources);
    }

    [Fact]
    public void The_deficient_set_is_the_smallest_group_that_cannot_be_satisfied()
    {
        // Slot 0 is perfectly satisfiable and shares nothing with the other two;
        // slots 1 and 2 compete for one resource.
        //
        // This is the fixture that separates a real witness from a plausible one.
        // Reporting *every* slot would name a set of three with a neighbourhood of
        // three, which satisfies Hall's inequality and is therefore not deficient
        // at all — a report that is not merely unhelpful but false. Reporting the
        // neighbourhood as the union of every pool would say three resources are
        // eligible for a group that can only reach one.
        var deficiency = Deficient(Pool(R1, R2), Pool(R3), Pool(R3));

        Assert.Equal([1, 2], deficiency.Slots);
        Assert.Equal([R3], deficiency.Resources);
    }

    [Fact]
    public void The_deficient_set_is_short_by_exactly_one()
    {
        // Three slots over two resources, reached only by displacement — the walk
        // has to move R1 and R2 around before it can conclude anything. The set is
        // still tight: three slots, two resources.
        var deficiency = Deficient(Pool(R1, R2), Pool(R1, R2), Pool(R1, R2));

        Assert.Equal([0, 1, 2], deficiency.Slots);
        Assert.Equal([R1, R2], deficiency.Resources);
        Assert.Equal(deficiency.Slots.Count - 1, deficiency.Resources.Count);
    }

    [Fact]
    public void The_same_input_yields_the_same_deficient_set()
    {
        // Determinism matters as much here as it does for the assignment: this
        // witness becomes a sentence naming resources, and an editor told to go
        // and fix a different pair on every keystroke would not believe any of it.
        var first = Deficient(Pool(R1, R2), Pool(R1, R2), Pool(R1, R2));
        var second = Deficient(Pool(R1, R2), Pool(R1, R2), Pool(R1, R2));

        Assert.Equal(first.Slots, second.Slots);
        Assert.Equal(first.Resources, second.Resources);
    }

    [Fact]
    public void A_preference_falls_through_to_the_slot_that_admits_it()
    {
        // Pinning R2 to slot 0 works and is found first, because slots are tried
        // in order.
        Assert.Equal([R2, R1], AssignedIncluding(R2, Pool(R1, R2), Pool(R1, R2)));
    }
}
