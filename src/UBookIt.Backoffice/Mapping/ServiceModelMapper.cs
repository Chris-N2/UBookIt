using UBookIt.Backoffice.Models;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
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

        // Each role's capabilities validate separately, for the same reason the
        // duration does: they are a value object the role cannot hold in an
        // invalid state, so a malformed key must be caught here and reported
        // alongside — not instead of — whatever else the save got wrong. That is
        // what lets one response carry both `type-key-invalid` and
        // `capability-key-invalid` for a role that fumbled both.
        var capabilities = model.Roles
            .Select(r => CapabilitySet.Create(r.RequiredCapabilities, CapabilitySet.RequiredField))
            .ToList();

        var service = Service.Create(
            model.Name,
            duration.Succeeded ? duration.Value : null,
            model.Roles.Select((r, index) => new ServiceRole(r.ResourceType, r.Count)
            {
                RequiredCapabilities = capabilities[index].Succeeded
                    ? capabilities[index].Value
                    : CapabilitySet.Empty,
            }),
            id);

        var valueObjectFailures = duration.Failures
            .Concat(capabilities.Where(c => !c.Succeeded).SelectMany(c => c.Failures))
            .ToList();

        if (valueObjectFailures.Count == 0)
        {
            return service;
        }

        // Report value-object problems alongside any name or role problems, so
        // one save surfaces every failed rule rather than one round trip per rule.
        var failures = new List<DomainFailure>(valueObjectFailures);

        if (!service.Succeeded)
        {
            failures.AddRange(service.Failures);
        }

        return DomainResult<Service>.Failure(failures);
    }

    /// <summary>
    /// A preview configuration to the pair resolution actually depends on: a
    /// role and a duration. Never a <see cref="Service"/> — that would require a
    /// name the editor may not have entered yet, letting a validator with no
    /// stake in the question decide whether it can be asked (design D2).
    /// <para>
    /// Both parts validate, and both report through their existing stable codes:
    /// a malformed key is a failure here as everywhere else, because a chain
    /// computed from a silently narrowed configuration would describe something
    /// other than what is on screen.
    /// </para>
    /// </summary>
    internal static DomainResult<(ServiceRole Role, ServiceDuration Duration)> ToDomain(
        ServicePreviewRequestModel model)
    {
        var role = ServiceRole.Create(model.ResourceType, model.RequiredCapabilities);
        var duration = ToDomain(model.Duration);

        var failures = role.Failures.Concat(duration.Failures).ToList();

        return failures.Count > 0
            ? DomainResult<(ServiceRole, ServiceDuration)>.Failure(failures)
            : DomainResult<(ServiceRole, ServiceDuration)>.Success((role.Value, duration.Value));
    }

    internal static ServicePreviewResponseModel ToModel(ServiceResolution resolution)
        => new()
        {
            OfType = ToStage(resolution.OfType),
            WithCapabilities = ToStage(resolution.WithCapabilities),
            CanProvide = ToStage(resolution.Candidates.Select(c => c.Resource)),
            DurationExclusions = resolution.DurationExclusions
                .Select(e => new DurationExclusionModel
                {
                    Id = e.Resource.Id,
                    DisplayName = e.Resource.DisplayName,
                    Reason = e.Reason switch
                    {
                        DurationExclusionReason.ResourceMaximum => DurationExclusionModel.ResourceMaximumReason,
                        DurationExclusionReason.ResourceMinimum => DurationExclusionModel.ResourceMinimumReason,
                        _ => DurationExclusionModel.GranularityReason,
                    },
                    BoundMinutes = (int)e.Bound.TotalMinutes,
                })
                .ToList(),
        };

    private static ServicePreviewStageModel ToStage(IEnumerable<Resource> resources)
    {
        var items = resources
            .Select(r => new PreviewResourceModel { Id = r.Id, DisplayName = r.DisplayName })
            .ToList();

        return new ServicePreviewStageModel { Total = items.Count, Items = items };
    }

    internal static ServiceResponseModel ToModel(Service service)
        => new()
        {
            Id = service.Id,
            Name = service.Name,
            Duration = ToModel(service.Duration),
            Roles = service.Roles
                .Select(r => new ServiceRoleModel
                {
                    ResourceType = r.ResourceType,
                    RequiredCapabilities = [.. r.RequiredCapabilities.Keys],
                    Count = r.Count,
                })
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
