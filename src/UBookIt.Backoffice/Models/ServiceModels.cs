namespace UBookIt.Backoffice.Models;

/// <summary>
/// Management API contract models for services. Purpose-built DTOs — domain
/// types never appear in the HTTP contract. Durations are whole minutes,
/// matching the delivery-API convention.
/// </summary>
public class ServiceRequestModel
{
    public string Name { get; set; } = string.Empty;

    public ServiceDurationModel? Duration { get; set; }

    public List<ServiceRoleModel> Roles { get; set; } = [];
}

public class ServiceResponseModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ServiceDurationModel Duration { get; set; } = new();

    public List<ServiceRoleModel> Roles { get; set; } = [];
}

/// <summary>
/// A service's duration on the wire. The kind is explicit so the contract
/// cannot express a combination the domain has no meaning for: a fixed
/// duration carries <see cref="Minutes"/>, a variable one carries whichever
/// of <see cref="MinMinutes"/>/<see cref="MaxMinutes"/> were supplied, and a
/// null bound defers to the fulfilling resource's own bound.
/// </summary>
public class ServiceDurationModel
{
    public const string FixedKind = "fixed";

    public const string VariableKind = "variable";

    public string Kind { get; set; } = string.Empty;

    /// <summary>The length, when the kind is <c>fixed</c>.</summary>
    public int? Minutes { get; set; }

    /// <summary>The lower bound, when the kind is <c>variable</c>.</summary>
    public int? MinMinutes { get; set; }

    /// <summary>The upper bound, when the kind is <c>variable</c>.</summary>
    public int? MaxMinutes { get; set; }
}

public class ServiceRoleModel
{
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// What a resource must be able to do to fill this role. Omitted or empty
    /// constrains by type alone — the behaviour of every service defined before
    /// capabilities existed.
    /// </summary>
    public List<string> RequiredCapabilities { get; set; } = [];

    public int Count { get; set; } = 1;
}

public class PagedServicesModel
{
    public int Total { get; set; }

    public List<ServiceResponseModel> Items { get; set; } = [];
}
