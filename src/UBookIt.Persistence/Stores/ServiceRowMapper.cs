using UBookIt.Core.Services;
using UBookIt.Persistence.Entities;

namespace UBookIt.Persistence.Stores;

/// <summary>
/// Maps between the domain <see cref="Service"/> aggregate and its rows.
/// Rehydration goes through the Core factory so stored data re-passes domain
/// validation; corrupted rows surface as exceptions, never silently-wrong
/// domain objects (mirrors <see cref="ResourceRowMapper"/>).
/// </summary>
internal static class ServiceRowMapper
{
    internal static Service ToDomain(ServiceRow row)
        => Service.Create(
            row.Name,
            row.DurationMinutes is { } minutes ? TimeSpan.FromMinutes(minutes) : null,
            row.Roles.Select(r => new ServiceRole(r.ResourceType, r.Count)),
            row.Id).Value;

    internal static ServiceRow ToRow(Service service)
    {
        var row = new ServiceRow
        {
            Id = service.Id,
            Name = service.Name,
            DurationMinutes = service.Duration is { } d ? (int)d.TotalMinutes : null,
        };
        row.Roles = ToRoleRows(service);
        return row;
    }

    internal static void ApplyScalars(Service service, ServiceRow row)
    {
        row.Name = service.Name;
        row.DurationMinutes = service.Duration is { } d ? (int)d.TotalMinutes : null;
    }

    internal static List<ServiceRoleRow> ToRoleRows(Service service)
        => service.Roles
            .Select(role => new ServiceRoleRow
            {
                ServiceId = service.Id,
                ResourceType = role.ResourceType,
                Count = role.Count,
            })
            .ToList();
}
