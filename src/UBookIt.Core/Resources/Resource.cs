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
        AvailabilityConfiguration availability,
        bool directlyBookable)
    {
        Id = id;
        Type = type;
        DisplayName = displayName;
        Description = description;
        Capabilities = capabilities;
        Availability = availability;
        DirectlyBookable = directlyBookable;
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

    /// <summary>
    /// Whether this resource may be booked <em>on its own</em>. False by default:
    /// a resource that has not been given the permission does not have it.
    /// <para>
    /// A statement about what the business offers, not about what the system can
    /// compute. A resource may be perfectly available, perfectly eligible, and
    /// still meaningless alone — a therapist with no room to work in — and no rule
    /// over type, capability or calendar distinguishes that from a room that is
    /// genuinely lettable. Only the editor knows.
    /// </para>
    /// <para>
    /// It constrains <b>direct</b> booking alone. A resource withholding it stays
    /// fully usable as part of a service: it resolves into candidate pools,
    /// contributes to composite availability, and is claimed by a service booking
    /// exactly as before. Withholding makes a resource unbookable <em>by itself</em>,
    /// never unbookable.
    /// </para>
    /// <para>
    /// It is deliberately <b>not</b> part of eligibility. Candidate resolution is
    /// type, then required capabilities, then a duration the service permits;
    /// a fourth term here would make a service's pool depend on whether its members
    /// happen to be separately lettable, which has nothing to do with whether they
    /// can fulfil the service.
    /// </para>
    /// <para>
    /// It is not a validation rule either. No configuration becomes invalid by
    /// withholding it and none becomes valid by granting it, so
    /// <see cref="Create"/> rejects neither answer.
    /// </para>
    /// </summary>
    public bool DirectlyBookable { get; }

    public static DomainResult<Resource> Create(
        string? type,
        string? displayName,
        string? description = null,
        IEnumerable<string?>? capabilities = null,
        AvailabilityConfiguration? availability = null,
        bool directlyBookable = false,
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
        else if (type!.Length > NormalizedKey.MaxLength)
        {
            // Length is validated here rather than left to the column: a
            // well-formed but over-long key would otherwise pass the domain and
            // the API and fail at INSERT as a 500, where the spec promises the
            // stable `type-key-invalid` code.
            failures.Add(new DomainFailure(
                FailureCodes.TypeKeyInvalid,
                $"A resource type key may be at most {NormalizedKey.MaxLength} characters.",
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
                availability ?? AvailabilityConfiguration.Closed,
                // Defaulted to withheld, and never validated: neither answer makes
                // a resource invalid, so there is no branch above that can reject
                // one. A caller that says nothing is saying no.
                directlyBookable));
    }
}
