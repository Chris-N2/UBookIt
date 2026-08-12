using UBookIt.Backoffice.Models;
using UBookIt.Core.Common;
using UBookIt.Core.Services;

namespace UBookIt.Backoffice.Mapping;

/// <summary>
/// Maps between the service API models and the domain, running validation
/// through the Core factories so a single save reports every failed rule.
/// </summary>
internal static class ServiceModelMapper
{
    internal static DomainResult<Service> ToDomain(ServiceRequestModel model, Guid? id = null)
    {
        var duration = ToDomain(model.Duration);

        var service = Service.Create(
            model.Name,
            duration.Succeeded ? duration.Value : null,
            model.Roles.Select(r => new ServiceRole(r.ResourceType, r.Count)),
            id);

        if (duration.Succeeded)
        {
            return service;
        }

        // Report duration problems alongside any name or role problems, so one
        // save surfaces every failed rule rather than one round trip per rule.
        var failures = new List<DomainFailure>(duration.Failures);

        if (!service.Succeeded)
        {
            failures.AddRange(service.Failures);
        }

        return DomainResult<Service>.Failure(failures);
    }

    internal static ServiceResponseModel ToModel(Service service)
        => new()
        {
            Id = service.Id,
            Name = service.Name,
            Duration = ToModel(service.Duration),
            Roles = service.Roles
                .Select(r => new ServiceRoleModel { ResourceType = r.ResourceType, Count = r.Count })
                .ToList(),
        };

    /// <summary>
    /// An absent or unrecognised kind is a validation failure, never a silent
    /// default — the duration is an explicit choice, and guessing one would
    /// store something the caller did not ask for.
    /// </summary>
    private static DomainResult<ServiceDuration> ToDomain(ServiceDurationModel? model)
    {
        if (model is null)
        {
            return DomainResult<ServiceDuration>.Failure(
                FailureCodes.ServiceDurationInvalid,
                "A duration is required: supply a kind of 'fixed' or 'variable'.",
                ServiceDuration.LengthField);
        }

        switch (model.Kind)
        {
            case ServiceDurationModel.FixedKind:
                return model.Minutes is { } minutes
                    ? ServiceDuration.Fixed(TimeSpan.FromMinutes(minutes))
                    : DomainResult<ServiceDuration>.Failure(
                        FailureCodes.ServiceDurationInvalid,
                        "A fixed duration requires a length in minutes.",
                        ServiceDuration.LengthField);

            case ServiceDurationModel.VariableKind:
                return ServiceDuration.Variable(
                    model.MinMinutes is { } min ? TimeSpan.FromMinutes(min) : null,
                    model.MaxMinutes is { } max ? TimeSpan.FromMinutes(max) : null);

            default:
                return DomainResult<ServiceDuration>.Failure(
                    FailureCodes.ServiceDurationInvalid,
                    $"Unknown duration kind '{model.Kind}'. Expected '{ServiceDurationModel.FixedKind}' or '{ServiceDurationModel.VariableKind}'.",
                    ServiceDuration.LengthField);
        }
    }

    private static ServiceDurationModel ToModel(ServiceDuration duration)
        => duration.Kind == ServiceDurationKind.Fixed
            ? new ServiceDurationModel
            {
                Kind = ServiceDurationModel.FixedKind,
                Minutes = (int)duration.FixedLength!.Value.TotalMinutes,
            }
            : new ServiceDurationModel
            {
                Kind = ServiceDurationModel.VariableKind,
                MinMinutes = duration.Min is { } min ? (int)min.TotalMinutes : null,
                MaxMinutes = duration.Max is { } max ? (int)max.TotalMinutes : null,
            };
}
