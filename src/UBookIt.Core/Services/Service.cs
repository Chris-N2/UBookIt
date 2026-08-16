using UBookIt.Core.Common;

namespace UBookIt.Core.Services;

/// <summary>
/// One required role in a service's composition: a resource type key, the
/// capabilities a resource must carry to fill it, and a count.
/// <para>
/// A count of <c>N</c> means <c>N</c> <b>distinct</b> resources are required
/// simultaneously — <c>N</c> interchangeable slots drawn from this role's
/// eligibility pool, no resource filling more than one of them. Two roles may
/// name the same resource type provided their required capabilities differ; their
/// pools then overlap without being equal, which is precisely the case
/// <see cref="SlotAssignment"/> exists to resolve.
/// </para>
/// </summary>
public sealed record ServiceRole(string ResourceType, int Count)
{
    /// <summary>
    /// The largest count a role may ask for.
    /// <para>
    /// A sanity bound on work rather than a domain claim (design D8): each unit is
    /// a slot the assignment must fill, so an absurd count is an unbounded amount
    /// of work for a configuration that cannot succeed. Twenty is chosen because a
    /// service needing more than a couple of dozen of one resource type is a
    /// different kind of product — a hall booking rather than an appointment — and
    /// twenty slots is trivial for the assignment either way. Trivially adjustable:
    /// nothing in the algorithm depends on the value.
    /// </para>
    /// </summary>
    public const int MaxCount = 20;

    /// <summary>
    /// What a resource must be able to do to fill this role. Empty constrains by
    /// type alone, which is exactly the behaviour of every service defined
    /// before capabilities existed.
    /// </summary>
    public CapabilitySet RequiredCapabilities { get; init; } = CapabilitySet.Empty;

    /// <summary>
    /// The one validating factory for a role, so the same rules apply whether a
    /// role is being saved as part of a service or evaluated on its own for a
    /// configuration preview. A preview asks what a role resolves to, which is a
    /// question about the role and not about the service around it — but the
    /// role itself must still be well formed, or the answer describes something
    /// other than what was asked.
    /// </summary>
    public static DomainResult<ServiceRole> Create(
        string? resourceType,
        IEnumerable<string?>? requiredCapabilities,
        int count = 1,
        int? roleIndex = null)
    {
        var failures = new List<DomainFailure>();

        ValidateResourceType(resourceType, failures, roleIndex);

        ValidateCount(count, failures, roleIndex);

        var capabilities = CapabilitySet.Create(
            requiredCapabilities, FieldFor(roleIndex, CapabilitySet.RequiredField));
        if (!capabilities.Succeeded)
        {
            failures.AddRange(capabilities.Failures);
        }

        return failures.Count > 0
            ? DomainResult<ServiceRole>.Failure(failures)
            : DomainResult<ServiceRole>.Success(
                new ServiceRole(resourceType!, count) { RequiredCapabilities = capabilities.Value });
    }

    /// <summary>
    /// Shape and length for a role's resource type key. Length is bounded here
    /// rather than left to the column: a well-formed but over-long key would
    /// otherwise pass the domain and the API and fail at INSERT as a 500, where
    /// the resources spec promises the stable <c>type-key-invalid</c> code
    /// (design D8).
    /// </summary>
    internal static void ValidateResourceType(
        string? resourceType, List<DomainFailure> failures, int? roleIndex = null)
    {
        if (!NormalizedKey.IsValid(resourceType))
        {
            failures.Add(new DomainFailure(
                FailureCodes.TypeKeyInvalid,
                $"Role resource type key '{resourceType}' must be lower-case kebab-case (e.g. 'room').",
                FieldFor(roleIndex, nameof(ResourceType))));
        }
        else if (resourceType!.Length > NormalizedKey.MaxLength)
        {
            failures.Add(new DomainFailure(
                FailureCodes.TypeKeyInvalid,
                $"A role resource type key may be at most {NormalizedKey.MaxLength} characters.",
                FieldFor(roleIndex, nameof(ResourceType))));
        }
    }

    /// <summary>
    /// Bounds a role's count, shared with <see cref="Service.Create"/> so the rule
    /// has one implementation whether a role is validated on its own for a
    /// configuration preview or as part of a service.
    /// <para>
    /// Only the count itself is judged. A count larger than the number of eligible
    /// resources is <em>accepted</em>: that is a property of the pool rather than
    /// of the service, resources may be added later, and refusing the save would
    /// block a configuration that is not wrong (design D8). ⑨-2a reports it as a
    /// diagnostic instead.
    /// </para>
    /// </summary>
    internal static void ValidateCount(int count, List<DomainFailure> failures, int? roleIndex = null)
    {
        if (count < 1 || count > MaxCount)
        {
            failures.Add(new DomainFailure(
                FailureCodes.ServiceRoleCountInvalid,
                $"A service role count must be between 1 and {MaxCount}.",
                FieldFor(roleIndex, nameof(Count))));
        }
    }

    /// <summary>
    /// The failure field for one control of one role. A service now holds
    /// several roles, so "the resource type is malformed" is not enough for a
    /// consumer to know which control to mark — the index has to travel with the
    /// failure or every message lands on the first row.
    /// <para>
    /// A null index means the role is being validated on its own rather than as
    /// part of a list, which is the configuration-preview case; the field is then
    /// the bare control name, as it was before roles could be plural.
    /// </para>
    /// </summary>
    public static string FieldFor(int? roleIndex, string field)
        => roleIndex is { } index ? $"Roles[{index}].{field}" : field;
}

/// <summary>
/// A bookable service: a duration-and-composition template that later resolves
/// to a booking's resource claims. Carries no presentation concerns; content
/// nodes presenting a service reference it by id. Pure domain — no Umbraco/EF.
/// </summary>
public sealed class Service
{
    private readonly List<ServiceRole> _roles;

    private Service(Guid id, string name, ServiceDuration duration, List<ServiceRole> roles)
    {
        Id = id;
        Name = name;
        Duration = duration;
        _roles = roles;
    }

    public Guid Id { get; }

    public string Name { get; }

    /// <summary>
    /// How long this service takes: a fixed length, or a booker-chosen length
    /// within optional bounds. Always narrows the fulfilling resource's own
    /// range rather than widening it — see <see cref="ServiceDuration"/>.
    /// </summary>
    public ServiceDuration Duration { get; }

    public IReadOnlyList<ServiceRole> Roles => _roles;

    public static DomainResult<Service> Create(
        string? name,
        ServiceDuration? duration,
        IEnumerable<ServiceRole> roles,
        Guid? id = null)
    {
        var failures = new List<DomainFailure>();

        if (string.IsNullOrWhiteSpace(name))
        {
            failures.Add(new DomainFailure(
                FailureCodes.ServiceNameRequired, "A service name is required.", nameof(Name)));
        }

        // Duration validity is the value object's own concern — it cannot be
        // constructed invalid — so an omitted specification simply means the
        // unconfigured default: any length the resource permits.
        var durationSpec = duration ?? ServiceDuration.Unbounded;

        var roleList = roles?.ToList() ?? [];

        if (roleList.Count == 0)
        {
            failures.Add(new DomainFailure(
                FailureCodes.ServiceRoleInvalid, "A service must have at least one role.", nameof(Roles)));
        }

        for (var index = 0; index < roleList.Count; index++)
        {
            var role = roleList[index];

            // Delegated so the rule has one implementation shared with
            // ServiceRole.Create, which a configuration preview uses to validate
            // a role that belongs to no service.
            ServiceRole.ValidateResourceType(role.ResourceType, failures, index);
            ServiceRole.ValidateCount(role.Count, failures, index);
        }

        failures.AddRange(DuplicateRoleFailures(roleList));

        if (failures.Count > 0)
        {
            return DomainResult<Service>.Failure(failures);
        }

        // Roles are an unordered set as far as behaviour is concerned, so the
        // aggregate holds them in one canonical order — type key, then required
        // capabilities, then count.
        //
        // Type alone is no longer total now that two roles may share one, and
        // `List.Sort` is unstable: without the tiebreak two same-type roles could
        // exchange places between saves, breaking round-trip equality and the
        // delivery contract's promise of a deterministic role order. The tiebreak
        // is total *because* the duplicate rule above rejects two roles matching
        // on all three, so the two move together (design D6).
        //
        // Canonicalised here rather than at each read: a store that returned
        // rows in insertion order and one that returned them in whatever order
        // an include materialised would otherwise hand back services that
        // compare unequal and publish their roles differently, and the delivery
        // contract's promise of a deterministic order would rest on an
        // accident of query planning rather than on anything this code owns.
        //
        // Validation above reports against the order the caller *supplied*, so
        // a failure still names the row the editor is showing.
        roleList.Sort(CompareCanonically);

        return DomainResult<Service>.Success(
            new Service(id ?? Guid.NewGuid(), name!.Trim(), durationSpec, roleList));
    }

    /// <summary>
    /// Rejects two roles identical in resource type <em>and</em> required
    /// capabilities (design D5).
    /// <para>
    /// Narrowed from the type-only rule that made independent per-role assignment
    /// correct. Two `therapist` roles requiring <em>different</em> capabilities are
    /// now valid and are the case this change exists for — their pools overlap
    /// without being equal, which the assignment resolves. What remains rejected is
    /// two roles that are two spellings of one requirement: a count expresses it
    /// exactly once, allowing both would leave two representations that compare
    /// unequal and publish differently while meaning the same thing, and silently
    /// merging them would rewrite an editor's two rows into one row of two.
    /// </para>
    /// <para>
    /// The message names the count as the correction, because that is what the
    /// editor should have entered — a rule that only said "duplicate" would leave
    /// the fix to be guessed at.
    /// </para>
    /// <para>
    /// Reported against every occurrence after the first, so an editor's message
    /// lands on the row that repeated the requirement rather than on the row that
    /// introduced it. Eligibility matches type and capability keys ordinally, so
    /// this does too.
    /// </para>
    /// </summary>
    private static IEnumerable<DomainFailure> DuplicateRoleFailures(List<ServiceRole> roles)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < roles.Count; index++)
        {
            var role = roles[index];

            if (role.ResourceType is null)
            {
                continue;
            }

            // Capability keys are already deduplicated and ordinally sorted by
            // CapabilitySet, so joining them is a canonical spelling of the
            // requirement rather than an ordering accident. The separator is a
            // character NormalizedKey cannot contain, so no two distinct
            // requirements can collide on one string.
            var requirement = $"{role.ResourceType} {string.Join(' ', role.RequiredCapabilities.Keys)}";

            if (!seen.Add(requirement))
            {
                yield return new DomainFailure(
                    FailureCodes.ServiceRoleDuplicateType,
                    role.RequiredCapabilities.IsEmpty
                        ? $"Two roles require resource type '{role.ResourceType}' with no required capabilities. "
                          + "Use a single role with a count instead."
                        : $"Two roles require resource type '{role.ResourceType}' with the same capabilities "
                          + $"({role.RequiredCapabilities}). Use a single role with a count instead.",
                    ServiceRole.FieldFor(index, nameof(ServiceRole.ResourceType)));
            }
        }
    }

    /// <summary>
    /// The canonical role order: resource type, then required capabilities, then
    /// count (design D6).
    /// <para>
    /// Total, because <see cref="DuplicateRoleFailures"/> has already rejected two
    /// roles matching on type and capabilities — so any two roles reaching here
    /// differ in one of the three terms. That dependency is why the two must move
    /// together: relaxing the duplicate rule without this tiebreak would let an
    /// unstable sort exchange two same-type roles between saves.
    /// </para>
    /// </summary>
    private static int CompareCanonically(ServiceRole left, ServiceRole right)
    {
        var byType = string.CompareOrdinal(left.ResourceType, right.ResourceType);
        if (byType != 0)
        {
            return byType;
        }

        var byCapabilities = CompareCapabilities(
            left.RequiredCapabilities.Keys, right.RequiredCapabilities.Keys);

        return byCapabilities != 0 ? byCapabilities : left.Count.CompareTo(right.Count);
    }

    /// <summary>
    /// Two capability sets compared as ordered sequences of their keys — element
    /// by element, then by length, so <c>{a}</c> precedes <c>{a, b}</c> and
    /// <c>{a}</c> precedes <c>{b}</c>. Both sides are already ordinally sorted by
    /// <c>CapabilitySet</c>, which is what makes the comparison a property of the
    /// requirement rather than of how it was typed.
    /// </summary>
    private static int CompareCapabilities(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        for (var index = 0; index < Math.Min(left.Count, right.Count); index++)
        {
            var comparison = string.CompareOrdinal(left[index], right[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return left.Count.CompareTo(right.Count);
    }
}
