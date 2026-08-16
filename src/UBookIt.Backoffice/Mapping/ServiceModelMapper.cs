using System.Globalization;
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
        // An explicit `"roles": null` overwrites the property initializer, and a
        // `[null]` entry survives binding, so both reach here as a null the
        // domain never sees. Neither is a ModelState error without [Required],
        // so left alone they leave the controller as a 500 where the CRUD
        // requirement promises 400 problem details.
        var roles = (model.Roles ?? []).Where(r => r is not null).ToList();

        var duration = ToDomain(model.Duration);

        // Each role's capabilities validate separately, for the same reason the
        // duration does: they are a value object the role cannot hold in an
        // invalid state, so a malformed key must be caught here and reported
        // alongside — not instead of — whatever else the save got wrong. That is
        // what lets one response carry both `type-key-invalid` and
        // `capability-key-invalid` for a role that fumbled both.
        // Each set's failures carry their own role's index, so a malformed key in
        // the third role marks the third row rather than the first.
        var capabilities = roles
            .Select((r, index) => CapabilitySet.Create(
                r.RequiredCapabilities, ServiceRole.FieldFor(index, CapabilitySet.RequiredField)))
            .ToList();

        var service = Service.Create(
            model.Name,
            duration.Succeeded ? duration.Value : null,
            roles.Select((r, index) => new ServiceRole(r.ResourceType, r.Count)
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
    /// A preview configuration to what resolution actually depends on: the roles
    /// and a duration. Never a <see cref="Service"/> — that would require a
    /// name the editor may not have entered yet, letting a validator with no
    /// stake in the question decide whether it can be asked (design D2).
    /// <para>
    /// Both parts validate, and both report through their existing stable codes:
    /// a malformed key is a failure here as everywhere else, because a chain
    /// computed from a silently narrowed configuration would describe something
    /// other than what is on screen.
    /// </para>
    /// </summary>
    internal static DomainResult<(IReadOnlyList<ServiceRole> Roles, ServiceDuration Duration)> ToDomain(
        ServicePreviewRequestModel model)
    {
        var duration = ToDomain(model.Duration);

        // Each role validated on its own, with its own index, so a malformed key
        // says which row it came from. Deliberately NOT through Service.Create:
        // that would reject two roles of one type, and refusing to answer is
        // exactly what withholds the information needed to fix that fault.
        // A null collection, or a null entry inside one, binds straight past the
        // property initializer; dereferencing it would be a 500 where this
        // endpoint promises a validation failure. Dropped rather than defaulted,
        // so an all-null list becomes the empty-list failure below.
        var supplied = (model.Roles ?? []).Where(r => r is not null).ToList();

        // The count is read from the request now, where ⑧a fixed it at 1. Its
        // argument then was that count is not an input to eligibility — a role of
        // count 3 draws on exactly the pool a role of count 1 does — and that is
        // still true of the *chains*, which are unchanged by it. It stopped being
        // the whole story when the response gained the sufficiency finding, which
        // is an assignment question and cannot be asked at all without knowing how
        // many of each role are needed.
        //
        // So an out-of-range count is a validation failure here, exactly as a
        // malformed type key already was: both make the answer describe something
        // other than what is on screen, and refusing to answer is what stops the
        // endpoint reporting on a configuration it silently narrowed.
        var roles = supplied
            .Select((r, index) => ServiceRole.Create(r.ResourceType, r.RequiredCapabilities, r.Count, index))
            .ToList();

        var failures = roles.SelectMany(r => r.Failures).Concat(duration.Failures).ToList();

        if (supplied.Count == 0)
        {
            // An empty list is a request with nothing to report on, not a
            // configuration whose chains are all empty.
            failures.Add(new DomainFailure(
                FailureCodes.ServiceRoleInvalid,
                "At least one role is required to preview a configuration.",
                nameof(ServicePreviewRequestModel.Roles)));
        }

        return failures.Count > 0
            ? DomainResult<(IReadOnlyList<ServiceRole>, ServiceDuration)>.Failure(failures)
            : DomainResult<(IReadOnlyList<ServiceRole>, ServiceDuration)>.Success(
                ([.. roles.Select(r => r.Value)], duration.Value));
    }

    /// <summary>
    /// The preview response: the chains, plus whichever findings Core made.
    /// <para>
    /// The findings are passed in rather than computed here, and both are computed
    /// from the very chains being mapped — so a report cannot describe a
    /// different pool from the one the chains describe or the booking path acts
    /// on.
    /// </para>
    /// </summary>
    internal static ServicePreviewResponseModel ToModel(
        IEnumerable<(ServiceRole Role, ServiceResolution Chain)> chains,
        RoleMisalignment? misalignment = null,
        RoleShortfall? shortfall = null)
        => new()
        {
            Roles = [.. chains.Select(c => ToModel(c.Role, c.Chain))],

            // Null when the roles can align, and null is silence rather than
            // reassurance: the endpoint never reports that a service is
            // bookable, so there is no positive value to carry.
            StartMisalignment = misalignment is null ? null : ToModel(misalignment),

            // Null on the same terms and for the same reason. A sufficient pool is
            // absence, never a shortfall of zero: zero is the number that tells an
            // editor their configuration is wrong.
            PoolShortfall = shortfall is null ? null : ToModel(shortfall),
        };

    private static PoolShortfallModel ToModel(RoleShortfall shortfall)
        => new()
        {
            Roles = [.. shortfall.Roles.Select(entry => new ShortfallRoleModel
            {
                RoleIndex = entry.Index,
                ResourceType = entry.Role.ResourceType,
                RequiredCapabilities = [.. entry.Role.RequiredCapabilities.Keys],
                Count = entry.Role.Count,
            })],
            Required = shortfall.Required,
            Eligible = shortfall.Eligible,
        };

    private static StartMisalignmentModel ToModel(RoleMisalignment misalignment)
        => new()
        {
            First = ToModel(misalignment.First),
            Second = ToModel(misalignment.Second),
        };

    private static MisalignedRoleModel ToModel(MisalignedRole role)
        => new()
        {
            ResourceType = role.Role.ResourceType,
            ResourceId = role.Resource.Id,
            DisplayName = role.Resource.DisplayName,
            WindowStart = role.WindowStart.ToString("HH\\:mm", CultureInfo.InvariantCulture),
            GranularityMinutes = (int)role.Granularity.TotalMinutes,
        };

    private static ServiceRoleChainModel ToModel(ServiceRole role, ServiceResolution resolution)
        => new()
        {
            ResourceType = role.ResourceType,
            RequiredCapabilities = [.. role.RequiredCapabilities.Keys],
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
