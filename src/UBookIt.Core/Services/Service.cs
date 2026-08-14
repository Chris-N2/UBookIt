using UBookIt.Core.Common;

namespace UBookIt.Core.Services;

/// <summary>
/// One required role in a service's composition: a resource type key, the
/// capabilities a resource must carry to fill it, and a count. v1 enforces
/// exactly one role of count 1; the shape is deliberately plural so multi-role
/// composition is additive (see the services roadmap).
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
        string? resourceType, IEnumerable<string?>? requiredCapabilities, int count = 1)
    {
        var failures = new List<DomainFailure>();

        ValidateResourceType(resourceType, failures);

        if (count != 1)
        {
            failures.Add(new DomainFailure(
                FailureCodes.ServiceRoleInvalid, "A service role count must be 1 in v1.", nameof(Count)));
        }

        var capabilities = CapabilitySet.Create(requiredCapabilities, CapabilitySet.RequiredField);
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
    internal static void ValidateResourceType(string? resourceType, List<DomainFailure> failures)
    {
        if (!NormalizedKey.IsValid(resourceType))
        {
            failures.Add(new DomainFailure(
                FailureCodes.TypeKeyInvalid,
                $"Role resource type key '{resourceType}' must be lower-case kebab-case (e.g. 'room').",
                nameof(ResourceType)));
        }
        else if (resourceType!.Length > NormalizedKey.MaxLength)
        {
            failures.Add(new DomainFailure(
                FailureCodes.TypeKeyInvalid,
                $"A role resource type key may be at most {NormalizedKey.MaxLength} characters.",
                nameof(ResourceType)));
        }
    }
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

        if (roleList.Count != 1)
        {
            failures.Add(new DomainFailure(
                FailureCodes.ServiceRoleInvalid, "A service must have exactly one role in v1.", nameof(Roles)));
        }
        else
        {
            var role = roleList[0];

            // Delegated so the rule has one implementation shared with
            // ServiceRole.Create, which a configuration preview uses to validate
            // a role that belongs to no service.
            ServiceRole.ValidateResourceType(role.ResourceType, failures);

            if (role.Count != 1)
            {
                failures.Add(new DomainFailure(
                    FailureCodes.ServiceRoleInvalid, "A service role count must be 1 in v1.", nameof(ServiceRole.Count)));
            }
        }

        return failures.Count > 0
            ? DomainResult<Service>.Failure(failures)
            : DomainResult<Service>.Success(new Service(id ?? Guid.NewGuid(), name!.Trim(), durationSpec, roleList));
    }
}
