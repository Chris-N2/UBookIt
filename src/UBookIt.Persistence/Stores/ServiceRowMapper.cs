using UBookIt.Core.Common;
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
        => WithStoredTemplateSet(ToDomainWithoutTemplateSet(row), row.TemplateSet);

    /// <summary>
    /// Applies a stored template set through the domain rule, so a value written outside the package
    /// reads back normalised. A value the rule rejects reads back as <b>no set</b>, deliberately
    /// unlike the instructions above, whose <c>.Value</c> throws on a failed result: one bad row must
    /// not stop the service loading, or stop the messages for its bookings being sent, and "no set"
    /// is the safe reading because every message then falls back to the views for every service
    /// (service-email-templates, design 6). <c>WithTemplateSet</c> cannot fail on a value the rule
    /// has already accepted, nor on <c>null</c>.
    /// </summary>
    private static Service WithStoredTemplateSet(Service service, string? stored)
    {
        var normalized = Service.NormalizeTemplateSet(stored);

        return service.WithTemplateSet(normalized.Succeeded ? normalized.Value : null).Value;
    }

    private static Service ToDomainWithoutTemplateSet(ServiceRow row)
        => Service.Create(
            row.Name,
            ToDuration(row),
            // Row order is not relied on: `Service.Create` orders the roles
            // canonically, so a service rehydrated from rows materialised in
            // any order is the same service.
            row.Roles.Select(r => new ServiceRole(r.ResourceType, r.Count)
            {
                RequiredCapabilities = CapabilitySet
                    .Create(r.Capabilities.Select(c => (string?)c.Key), CapabilitySet.RequiredField)
                    .Value,
                VisitorSelectable = r.VisitorSelectable,
            }),
            row.Id).Value
            // Through the domain rule, so stored text is normalised exactly as saved text is.
            .WithBookerInstructions(row.BookerInstructions).Value;

    internal static ServiceRow ToRow(Service service)
    {
        var row = new ServiceRow
        {
            Id = service.Id,
            Name = service.Name,
        };
        ApplyScalars(service, row);
        row.Roles = ToRoleRows(service);
        return row;
    }

    internal static void ApplyScalars(Service service, ServiceRow row)
    {
        row.Name = service.Name;
        row.BookerInstructions = service.BookerInstructions;
        row.TemplateSet = service.TemplateSet;
        ApplyDuration(service.Duration, row);
    }

    /// <summary>
    /// Rebuilds the duration through the Core factories, so a row that cannot
    /// describe a valid duration throws rather than yielding a silently-wrong
    /// aggregate.
    /// </summary>
    private static ServiceDuration ToDuration(ServiceRow row)
    {
        var min = row.MinDurationMinutes is { } lower ? TimeSpan.FromMinutes(lower) : (TimeSpan?)null;
        var max = row.MaxDurationMinutes is { } upper ? TimeSpan.FromMinutes(upper) : (TimeSpan?)null;

        return row.DurationKind switch
        {
            ServiceDurationKind.Fixed => ServiceDuration.Fixed(
                min ?? throw new InvalidOperationException(
                    $"Service {row.Id} is stored as a fixed duration but has no length.")).Value,
            ServiceDurationKind.Variable => ServiceDuration.Variable(min, max).Value,
            _ => throw new InvalidOperationException(
                $"Service {row.Id} has an unknown duration kind '{row.DurationKind}'."),
        };
    }

    private static void ApplyDuration(ServiceDuration duration, ServiceRow row)
    {
        row.DurationKind = duration.Kind;
        row.MinDurationMinutes = duration.Min is { } min ? (int)min.TotalMinutes : null;
        row.MaxDurationMinutes = duration.Max is { } max ? (int)max.TotalMinutes : null;
    }

    internal static List<ServiceRoleRow> ToRoleRows(Service service)
        => service.Roles
            .Select(role => new ServiceRoleRow
            {
                ServiceId = service.Id,
                ResourceType = role.ResourceType,
                Count = role.Count,
                VisitorSelectable = role.VisitorSelectable,

                // The role's own id is database-generated, so the capability
                // rows are attached through the navigation and let EF fill the
                // foreign key when the role is inserted.
                Capabilities = [.. role.RequiredCapabilities.Keys
                    .Select(key => new ServiceRoleCapabilityRow { Key = key })],
            })
            .ToList();
}
