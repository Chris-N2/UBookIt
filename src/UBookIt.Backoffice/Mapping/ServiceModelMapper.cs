using UBookIt.Backoffice.Models;
using UBookIt.Core.Common;
using UBookIt.Core.Services;

namespace UBookIt.Backoffice.Mapping;

/// <summary>
/// Maps between the service API models and the domain, running validation
/// through the Core factory so a single save reports every failed rule.
/// </summary>
internal static class ServiceModelMapper
{
    internal static DomainResult<Service> ToDomain(ServiceRequestModel model, Guid? id = null)
        => Service.Create(
            model.Name,
            model.DurationMinutes is { } minutes ? TimeSpan.FromMinutes(minutes) : null,
            model.Roles.Select(r => new ServiceRole(r.ResourceType, r.Count)),
            id);

    internal static ServiceResponseModel ToModel(Service service)
        => new()
        {
            Id = service.Id,
            Name = service.Name,
            DurationMinutes = service.Duration is { } d ? (int)d.TotalMinutes : null,
            Roles = service.Roles
                .Select(r => new ServiceRoleModel { ResourceType = r.ResourceType, Count = r.Count })
                .ToList(),
        };
}
