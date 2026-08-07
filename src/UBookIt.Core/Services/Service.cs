using System.Text.RegularExpressions;
using UBookIt.Core.Common;

namespace UBookIt.Core.Services;

/// <summary>
/// One required role in a service's composition: a resource type key and a
/// count. v1 enforces exactly one role of count 1 with no capability
/// requirements; the shape is deliberately plural/extensible so multi-role and
/// required-capabilities are additive (see the services roadmap).
/// </summary>
public sealed record ServiceRole(string ResourceType, int Count);

/// <summary>
/// A bookable service: a duration-and-composition template that later resolves
/// to a booking's resource claims. Carries no presentation concerns; content
/// nodes presenting a service reference it by id. Pure domain — no Umbraco/EF.
/// </summary>
public sealed partial class Service
{
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex TypeKeyPattern();

    private readonly List<ServiceRole> _roles;

    private Service(Guid id, string name, TimeSpan? duration, List<ServiceRole> roles)
    {
        Id = id;
        Name = name;
        Duration = duration;
        _roles = roles;
    }

    public Guid Id { get; }

    public string Name { get; }

    /// <summary>The appointment length. When null, booking falls back to the fulfilling resource's minimum duration.</summary>
    public TimeSpan? Duration { get; }

    public IReadOnlyList<ServiceRole> Roles => _roles;

    public static DomainResult<Service> Create(
        string? name,
        TimeSpan? duration,
        IEnumerable<ServiceRole> roles,
        Guid? id = null)
    {
        var failures = new List<DomainFailure>();

        if (string.IsNullOrWhiteSpace(name))
        {
            failures.Add(new DomainFailure(
                FailureCodes.ServiceNameRequired, "A service name is required.", nameof(Name)));
        }

        if (duration is { } d)
        {
            if (d <= TimeSpan.Zero)
            {
                failures.Add(new DomainFailure(
                    FailureCodes.ServiceDurationInvalid, "A service duration must be positive when supplied.", nameof(Duration)));
            }
            else if (d.Ticks % TimeSpan.FromMinutes(1).Ticks != 0)
            {
                // Duration is persisted and exposed as whole minutes; reject
                // sub-minute values at the domain boundary so stored state
                // always round-trips.
                failures.Add(new DomainFailure(
                    FailureCodes.ServiceDurationInvalid, "A service duration must be a whole number of minutes.", nameof(Duration)));
            }
        }

        var roleList = roles?.ToList() ?? [];

        if (roleList.Count != 1)
        {
            failures.Add(new DomainFailure(
                FailureCodes.ServiceRoleInvalid, "A service must have exactly one role in v1.", nameof(Roles)));
        }
        else
        {
            var role = roleList[0];

            if (role.ResourceType is null || !TypeKeyPattern().IsMatch(role.ResourceType))
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
            : DomainResult<Service>.Success(new Service(id ?? Guid.NewGuid(), name!.Trim(), duration, roleList));
    }
}
