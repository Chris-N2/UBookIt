using UBookIt.Core.Availability;
using UBookIt.Core.Common;

namespace UBookIt.Core.Resources;

/// <summary>Well-known resource type keys. The set is open — see <see cref="Resource"/>.</summary>
public static class ResourceTypes
{
    public const string Room = "room";
}

/// <summary>
/// A bookable resource. The type key is an extensible normalized string
/// (lower-case kebab-case), not an enum, so future types (person, equipment)
/// need no schema or breaking API change. Carries no presentation concerns:
/// content nodes presenting a resource reference it by id.
/// </summary>
public sealed class Resource
{
    private Resource(
        Guid id,
        string type,
        string displayName,
        string? description,
        CapabilitySet capabilities,
        AvailabilityConfiguration availability)
    {
        Id = id;
        Type = type;
        DisplayName = displayName;
        Description = description;
        Capabilities = capabilities;
        Availability = availability;
    }

    public Guid Id { get; }

    public string Type { get; }

    public string DisplayName { get; }

    public string? Description { get; }

    /// <summary>
    /// What this resource can do. Empty means it carries no capabilities —
    /// never that it carries all of them — so an untagged resource is eligible
    /// only for roles that require nothing.
    /// </summary>
    public CapabilitySet Capabilities { get; }

    public AvailabilityConfiguration Availability { get; }

    public static DomainResult<Resource> Create(
        string? type,
        string? displayName,
        string? description = null,
        IEnumerable<string?>? capabilities = null,
        AvailabilityConfiguration? availability = null,
        Guid? id = null)
    {
        var failures = new List<DomainFailure>();

        if (!NormalizedKey.IsValid(type))
        {
            failures.Add(new DomainFailure(
                FailureCodes.TypeKeyInvalid,
                $"Resource type key '{type}' must be lower-case kebab-case (e.g. 'room').",
                nameof(Type)));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            failures.Add(new DomainFailure(
                FailureCodes.DisplayNameRequired, "A display name is required.", nameof(DisplayName)));
        }

        var capabilitySet = CapabilitySet.Create(capabilities, CapabilitySet.ResourceField);
        if (!capabilitySet.Succeeded)
        {
            failures.AddRange(capabilitySet.Failures);
        }

        return failures.Count > 0
            ? DomainResult<Resource>.Failure(failures)
            : DomainResult<Resource>.Success(new Resource(
                id ?? Guid.NewGuid(),
                type!,
                displayName!.Trim(),
                string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                capabilitySet.Value,
                availability ?? AvailabilityConfiguration.Closed));
    }
}
