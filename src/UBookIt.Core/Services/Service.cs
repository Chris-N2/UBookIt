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

            if (!NormalizedKey.IsValid(role.ResourceType))
            {
                failures.Add(new DomainFailure(
                    FailureCodes.TypeKeyInvalid,
                    $"Role resource type key '{role.ResourceType}' must be lower-case kebab-case (e.g. 'room').",
                    nameof(ServiceRole.ResourceType)));
            }

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
