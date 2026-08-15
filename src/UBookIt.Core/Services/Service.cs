using UBookIt.Core.Common;

namespace UBookIt.Core.Services;

/// <summary>
/// One required role in a service's composition: a resource type key, the
/// capabilities a resource must carry to fill it, and a count. A service may
/// hold several roles, each of a distinct resource type, each of count 1.
/// </summary>
public sealed record ServiceRole(string ResourceType, int Count)
{
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

        if (count != 1)
        {
            failures.Add(new DomainFailure(
                FailureCodes.ServiceRoleInvalid,
                "A service role count must be 1 in v1.",
                FieldFor(roleIndex, nameof(Count))));
        }

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

            if (role.Count != 1)
            {
                failures.Add(new DomainFailure(
                    FailureCodes.ServiceRoleInvalid,
                    "A service role count must be 1 in v1.",
                    ServiceRole.FieldFor(index, nameof(ServiceRole.Count))));
            }
        }

        failures.AddRange(DuplicateTypeFailures(roleList));

        if (failures.Count > 0)
        {
            return DomainResult<Service>.Failure(failures);
        }

        // Roles are an unordered set as far as behaviour is concerned, so the
        // aggregate holds them in one canonical order — ordinal by type key,
        // which is unique across a service's roles by the rule just applied.
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
        roleList.Sort((left, right) => string.CompareOrdinal(left.ResourceType, right.ResourceType));

        return DomainResult<Service>.Success(
            new Service(id ?? Guid.NewGuid(), name!.Trim(), durationSpec, roleList));
    }

    /// <summary>
    /// Rejects two roles naming the same resource type — the restriction that
    /// makes independent per-role assignment correct (multi-role-composition
    /// design D1).
    /// <para>
    /// Compares the type <em>alone</em>, never the type together with the
    /// required capabilities. Two `therapist` roles requiring different
    /// capabilities are exactly the case the restriction exists for: their
    /// eligibility pools overlap without being equal, so choosing each role its
    /// first available candidate can strand the narrower role and report a
    /// service unavailable when it was bookable. A rule that only rejected
    /// identical roles would leave that case unguarded while looking correct.
    /// </para>
    /// <para>
    /// Reported against every occurrence after the first, so an editor's message
    /// lands on the row that repeated a type rather than on the row that
    /// introduced it. Eligibility matches type keys ordinally, so this does too.
    /// </para>
    /// </summary>
    private static IEnumerable<DomainFailure> DuplicateTypeFailures(List<ServiceRole> roles)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < roles.Count; index++)
        {
            var type = roles[index].ResourceType;

            if (type is not null && !seen.Add(type))
            {
                yield return new DomainFailure(
                    FailureCodes.ServiceRoleDuplicateType,
                    $"Two roles require resource type '{type}'. Each role must name a different resource type.",
                    ServiceRole.FieldFor(index, nameof(ServiceRole.ResourceType)));
            }
        }
    }
}
