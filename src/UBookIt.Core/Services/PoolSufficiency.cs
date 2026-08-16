namespace UBookIt.Core.Services;

/// <summary>
/// A group of roles that cannot all be filled at once, and the arithmetic that
/// says so.
/// <para>
/// Stated over <b>roles</b> rather than over the internal slot expansion (design
/// D1). A role of count 3 is three interchangeable slots, and an editor has no
/// slots — it has rows. Every part of this record therefore names something that
/// can be acted on: a row to change, or a resource to add.
/// </para>
/// <para>
/// The collapse from slots to roles preserves the deficiency rather than merely
/// approximating it. Slots of one role share one candidate list, so including a
/// role's remaining slots adds slots without adding a single neighbour — the
/// neighbourhood of the collapsed set is exactly the neighbourhood of the slot
/// set the assignment reported. <see cref="Required"/> can therefore only grow
/// while <see cref="Eligible"/> stays put, and a true witness cannot become a
/// false one.
/// </para>
/// </summary>
/// <param name="Roles">
/// The roles that cannot be filled together, in the order the configuration
/// declares them, each with its position in that configuration.
/// </param>
/// <param name="Required">
/// How many <em>distinct</em> resources those roles need between them — the sum
/// of their counts.
/// </param>
/// <param name="Eligible">
/// How many distinct resources are eligible for any of them. Always less than
/// <see cref="Required"/>: a shortfall of zero is not a finding, and is never
/// reported as one.
/// </param>
public sealed record RoleShortfall(IReadOnlyList<ShortfallRole> Roles, int Required, int Eligible);

/// <summary>
/// One role a shortfall names, with <b>where it sits</b> in the configuration it
/// was computed for.
/// <para>
/// The position travels because two roles of one resource type requiring the same
/// capabilities are equal in every other respect — a configuration the domain
/// rejects but an editor can be halfway through — so nothing else distinguishes
/// them. A consumer with rows on screen can point at one; a consumer without them
/// can ignore it.
/// </para>
/// <para>
/// It is the index into the pools this check was given, which is the order the
/// caller supplied. Deliberately not a row number: Core has no rows, and turning
/// a position into a label is the consumer's business.
/// </para>
/// </summary>
public sealed record ShortfallRole(int Index, ServiceRole Role);

/// <summary>
/// Whether a service's roles can be filled <em>at once</em> by the resources that
/// exist.
/// <para>
/// Each role having candidates is necessary and not sufficient, and that gap is
/// the whole reason this exists: a role of count 2 with one candidate has
/// candidates, and two roles of one type drawing on one resource each have
/// candidates. The question is an assignment — the roles can be filled together
/// exactly when a matching saturating every slot exists — so it is answered by
/// <see cref="SlotAssignment"/>, the same function placement and availability
/// use, never by a second implementation of the rule (design D2).
/// </para>
/// <para>
/// The claim is <b>one-directional</b>, exactly as <see cref="StartAlignment"/>'s
/// is (design D3): this may report that the roles can never be filled together,
/// and never that they can. A sufficient pool means only that the configuration is
/// not structurally impossible. It says nothing about opening hours, lead time,
/// booking horizon, granularity, whether the resources are ever free at the same
/// instant, or whether their start grids ever coincide — none of which is
/// evaluated here. Reporting sufficiency positively would assert availability,
/// which the configuration surfaces are forbidden to do (⑧a design D5).
/// </para>
/// <para>
/// Computed over <b>eligibility alone</b>, from the resolved pools the booking
/// path acts on — never a second eligibility filter of its own (⑧a design D1),
/// and never the booking calendar (⑨-1a design D2). A report that consulted free
/// time would appear and disappear as bookings came and went, and would withdraw
/// itself precisely while an editor was performing the repair it asked for.
/// </para>
/// </summary>
public static class PoolSufficiency
{
    /// <summary>
    /// The roles that cannot be filled together, or null when an assignment
    /// exists.
    /// <para>
    /// Null is silence, not reassurance. It means only that no structural
    /// impossibility was found, which is the same silence a sufficient pool and an
    /// unreadable one produce — deliberately, because neither is a claim that the
    /// service can be booked.
    /// </para>
    /// <para>
    /// Takes the resolved pools rather than a service id, so the candidates are
    /// the ones the booking path acts on — projected from the same resolution,
    /// never filtered a second time.
    /// </para>
    /// </summary>
    public static RoleShortfall? FindShortfall(IReadOnlyList<RoleCandidates> pools)
    {
        ArgumentNullException.ThrowIfNull(pools);

        var roleOfSlot = new List<int>();
        var slotCandidates = new List<IReadOnlyList<Guid>>();

        for (var role = 0; role < pools.Count; role++)
        {
            // The candidate list is built once per role and shared by its slots:
            // the slots of one role are interchangeable by definition, and
            // distinctness is the assignment's job rather than the expansion's.
            // This is also what makes the collapse back to roles exact.
            IReadOnlyList<Guid> candidates = [.. pools[role].Candidates.Select(c => c.ResourceId)];

            for (var unit = 0; unit < pools[role].Role.Count; unit++)
            {
                roleOfSlot.Add(role);
                slotCandidates.Add(candidates);
            }
        }

        return SlotAssignment.TrySaturate(slotCandidates).Deficiency is { } deficiency
            ? Collapse(deficiency, roleOfSlot, [.. pools.Select(p => p.Role)])
            : null;
    }

    /// <summary>
    /// The assignment's slot-level witness, restated in roles.
    /// <para>
    /// Roles are collected by <b>index</b>, not by value. Two roles of one type
    /// requiring the same capabilities are equal as records — a configuration the
    /// domain rejects but an editor can be halfway through — and de-duplicating
    /// them would report one row where the configuration has two, understating what
    /// the editor is looking at.
    /// </para>
    /// <para>
    /// Shared with the placement path rather than reimplemented there (design D2):
    /// the two questions run over different graphs, and this is the one place that
    /// turns either answer into something a consumer can read.
    /// </para>
    /// </summary>
    internal static RoleShortfall Collapse(
        SlotDeficiency deficiency, IReadOnlyList<int> roleOfSlot, IReadOnlyList<ServiceRole> roles)
    {
        var indices = deficiency.Slots
            .Select(slot => roleOfSlot[slot])
            .Distinct()
            .OrderBy(index => index)
            .ToList();

        return new RoleShortfall(
            [.. indices.Select(index => new ShortfallRole(index, roles[index]))],

            // The sum of the whole roles' counts, not of the deficient slots: a
            // role is a row, and reporting "2 of the 3 you asked for" would name a
            // number that appears nowhere on screen. Including a role's remaining
            // slots adds no neighbours, so the set stays deficient (design D1).
            indices.Sum(index => roles[index].Count),
            deficiency.Resources.Count);
    }
}
