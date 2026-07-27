using System.Text.RegularExpressions;
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
public sealed partial class Resource
{
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex TypeKeyPattern();

    private Resource(Guid id, string type, string displayName, string? description, AvailabilityConfiguration availability)
    {
        Id = id;
        Type = type;
        DisplayName = displayName;
        Description = description;
        Availability = availability;
    }

    public Guid Id { get; }

    public string Type { get; }

    public string DisplayName { get; }

    public string? Description { get; }

    public AvailabilityConfiguration Availability { get; }

    public static DomainResult<Resource> Create(
        string? type,
        string? displayName,
        string? description = null,
        AvailabilityConfiguration? availability = null,
        Guid? id = null)
    {
        var failures = new List<DomainFailure>();

        if (type is null || !TypeKeyPattern().IsMatch(type))
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

        return failures.Count > 0
            ? DomainResult<Resource>.Failure(failures)
            : DomainResult<Resource>.Success(new Resource(
                id ?? Guid.NewGuid(),
                type!,
                displayName!.Trim(),
                string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                availability ?? AvailabilityConfiguration.Closed));
    }
}
