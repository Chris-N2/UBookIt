using UBookIt.Backoffice.Models;
using UBookIt.Core.Availability;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Stores;

namespace UBookIt.Backoffice.Mapping;

/// <summary>
/// Maps between API models and the domain, running all validation through the
/// Core factories and accumulating failures across sections so a single save
/// reports every failed rule (resource-management spec).
/// </summary>
internal static class ResourceModelMapper
{
    internal static DomainResult<Resource> ToDomain(ResourceRequestModel model, Guid? id = null)
    {
        var failures = new List<DomainFailure>();

        var windows = new List<(DayOfWeek Day, DayWindow Window)>();
        foreach (var openingHours in model.OpeningHours)
        {
            var window = DayWindow.Create(openingHours.Start, openingHours.End);
            if (window.Succeeded)
            {
                windows.Add((openingHours.Day, window.Value));
            }
            else
            {
                failures.AddRange(window.Failures);
            }
        }

        var weekly = WeeklyOpenHours.Create(windows);
        if (!weekly.Succeeded)
        {
            failures.AddRange(weekly.Failures);
        }

        var exceptions = new List<DateException>();
        foreach (var exceptionModel in model.Exceptions)
        {
            if (exceptionModel.Windows.Count == 0)
            {
                exceptions.Add(DateException.Closure(exceptionModel.Date));
                continue;
            }

            var exceptionWindows = new List<DayWindow>();
            foreach (var windowModel in exceptionModel.Windows)
            {
                var window = DayWindow.Create(windowModel.Start, windowModel.End);
                if (window.Succeeded)
                {
                    exceptionWindows.Add(window.Value);
                }
                else
                {
                    failures.AddRange(window.Failures);
                }
            }

            var exception = DateException.Override(exceptionModel.Date, exceptionWindows);
            if (exception.Succeeded)
            {
                exceptions.Add(exception.Value);
            }
            else
            {
                failures.AddRange(exception.Failures);
            }
        }

        BookingConstraints? constraints = null;
        if (model.Constraints is { } constraintsModel)
        {
            var constraintsResult = BookingConstraints.Create(
                granularity: TimeSpan.FromMinutes(constraintsModel.GranularityMinutes),
                minDuration: TimeSpan.FromMinutes(constraintsModel.MinDurationMinutes),
                maxDuration: TimeSpan.FromMinutes(constraintsModel.MaxDurationMinutes),
                leadTime: TimeSpan.FromMinutes(constraintsModel.LeadTimeMinutes),
                horizonDays: constraintsModel.HorizonDays);

            if (constraintsResult.Succeeded)
            {
                constraints = constraintsResult.Value;
            }
            else
            {
                failures.AddRange(constraintsResult.Failures);
            }
        }

        // Run configuration assembly even when the weekly pattern failed
        // (using an empty pattern as a stand-in) so exception-level failures
        // like duplicate-exception-date still surface in the same response —
        // one save reports every failed rule.
        AvailabilityConfiguration? availability = null;
        var availabilityResult = AvailabilityConfiguration.Create(
            weekly.Succeeded ? weekly.Value : WeeklyOpenHours.Empty, exceptions, constraints);
        if (availabilityResult.Succeeded)
        {
            availability = weekly.Succeeded ? availabilityResult.Value : null;
        }
        else
        {
            failures.AddRange(availabilityResult.Failures);
        }

        // Run resource creation even when availability failed so name/type
        // failures are reported in the same response as availability failures.
        var resource = Resource.Create(
            model.Type, model.DisplayName, model.Description, model.Capabilities,
            availability ?? AvailabilityConfiguration.Closed, id);

        if (!resource.Succeeded)
        {
            failures.InsertRange(0, resource.Failures);
        }

        return failures.Count > 0
            ? DomainResult<Resource>.Failure(failures)
            : DomainResult<Resource>.Success(resource.Value);
    }

    internal static ResourceResponseModel ToModel(Resource resource)
    {
        var constraints = resource.Availability.Constraints;

        return new ResourceResponseModel
        {
            Id = resource.Id,
            Type = resource.Type,
            DisplayName = resource.DisplayName,
            Description = resource.Description,
            Capabilities = [.. resource.Capabilities.Keys],
            OpeningHours = Enum.GetValues<DayOfWeek>()
                .SelectMany(day => resource.Availability.OpenHours.WindowsFor(day)
                    .Select(window => new OpeningHoursModel { Day = day, Start = window.Start, End = window.End }))
                .ToList(),
            Exceptions = resource.Availability.Exceptions
                .OrderBy(exception => exception.Date)
                .Select(exception => new AvailabilityExceptionModel
                {
                    Date = exception.Date,
                    Windows = exception.Windows
                        .Select(window => new TimeWindowModel { Start = window.Start, End = window.End })
                        .ToList(),
                })
                .ToList(),
            Constraints = new ConstraintsModel
            {
                GranularityMinutes = (int)constraints.Granularity.TotalMinutes,
                MinDurationMinutes = (int)constraints.MinDuration.TotalMinutes,
                MaxDurationMinutes = (int)constraints.MaxDuration.TotalMinutes,
                LeadTimeMinutes = (int)constraints.LeadTime.TotalMinutes,
                HorizonDays = constraints.HorizonDays,
            },
        };
    }

    internal static ResourceTypeUsageModel ToModel(ResourceTypeUsage usage) =>
        new() { Type = usage.Type, Count = usage.Count };

    internal static CapabilityUsageModel ToModel(CapabilityUsage usage) =>
        new() { Key = usage.Key, Count = usage.Count };

    internal static ResourceMatchModel ToModel(ResourceMatch match) =>
        new() { Id = match.Id, DisplayName = match.DisplayName };
}
