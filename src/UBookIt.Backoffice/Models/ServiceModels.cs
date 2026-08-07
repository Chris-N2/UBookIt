namespace UBookIt.Backoffice.Models;

/// <summary>
/// Management API contract models for services. Purpose-built DTOs — domain
/// types never appear in the HTTP contract. Duration is expressed as integer
/// minutes (null = no fixed duration), matching the delivery-API convention.
/// </summary>
public class ServiceRequestModel
{
    public string Name { get; set; } = string.Empty;

    public int? DurationMinutes { get; set; }

    public List<ServiceRoleModel> Roles { get; set; } = [];
}

public class ServiceResponseModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? DurationMinutes { get; set; }

    public List<ServiceRoleModel> Roles { get; set; } = [];
}

public class ServiceRoleModel
{
    public string ResourceType { get; set; } = string.Empty;

    public int Count { get; set; } = 1;
}

public class PagedServicesModel
{
    public int Total { get; set; }

    public List<ServiceResponseModel> Items { get; set; } = [];
}
