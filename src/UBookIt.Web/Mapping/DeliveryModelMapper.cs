using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Web.Models;

namespace UBookIt.Web.Mapping;

/// <summary>
/// Maps Core types to delivery view models and the placement request model to a
/// Core <see cref="BookingRequest"/>. The booker is built from the request body
/// alone with a null member key — no ambient identity is trusted (delivery-api
/// spec, "Anonymous access and auth stance").
/// </summary>
internal static class DeliveryModelMapper
{
    internal static ResourceReadModel ToReadModel(Resource resource, string zoneId)
    {
        var constraints = resource.Availability.Constraints;

        return new ResourceReadModel
        {
            Id = resource.Id,
            Type = resource.Type,
            DisplayName = resource.DisplayName,
            Description = resource.Description,
            ZoneId = zoneId,
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

    internal static IntervalModel ToIntervalModel(UtcInterval interval)
        => new() { StartUtc = interval.StartUtc, EndUtc = interval.EndUtc };

    internal static SlotModel ToSlotModel(Slot slot)
        => new() { StartUtc = slot.StartUtc, DurationMinutes = (int)slot.Duration.TotalMinutes };

    internal static BookableStartModel ToBookableStartModel(BookableStart start)
        => new()
        {
            StartUtc = start.StartUtc,
            MinDurationMinutes = (int)start.MinDuration.TotalMinutes,
            MaxDurationMinutes = (int)start.MaxDuration.TotalMinutes,
        };

    internal static ServiceReadModel ToReadModel(Service service)
        => new()
        {
            Id = service.Id,
            Name = service.Name,
            // v1 guarantees exactly one role (services spec).
            ResourceType = service.Roles[0].ResourceType,
            Duration = ToDurationModel(service.Duration),
        };

    internal static ServiceDurationModel ToDurationModel(ServiceDuration duration)
        => duration.Kind == ServiceDurationKind.Fixed
            ? new ServiceDurationModel
            {
                Kind = "fixed",
                DurationMinutes = (int)duration.FixedLength!.Value.TotalMinutes,
            }
            : new ServiceDurationModel
            {
                Kind = "variable",
                MinDurationMinutes = Minutes(duration.Min),
                MaxDurationMinutes = Minutes(duration.Max),
            };

    internal static ServiceBookableStartModel ToServiceBookableStartModel(ServiceBookableStart start)
        => new()
        {
            StartUtc = start.StartUtc,
            Runs = start.Runs.Select(ToRunModel).ToList(),
        };

    internal static LengthRunModel ToRunModel(LengthRun run)
        => new()
        {
            MinDurationMinutes = (int)run.Min.TotalMinutes,
            MaxDurationMinutes = (int)run.Max.TotalMinutes,
            StepMinutes = (int)run.Step.TotalMinutes,
        };

    internal static PlacementResponseModel ToPlacementResponse(Booking booking)
        => new()
        {
            BookingId = booking.Id,
            Status = booking.Status.ToString(),
            // v1 is single-claim (bookings spec); the claim's resource is the booked resource.
            ResourceId = booking.Claims[0].ResourceId,
            Interval = new IntervalModel { StartUtc = booking.Interval.StartUtc, EndUtc = booking.Interval.EndUtc },
            Booker = new BookerModel
            {
                Name = booking.Booker.Name,
                Email = booking.Booker.Email,
                Phone = booking.Booker.Phone,
            },
        };

    /// <summary>
    /// Builds a validated <see cref="BookingRequest"/> from the request body.
    /// The member key is always null — an anonymous body may not assert identity.
    /// Booker validation (name/email) runs through the Core factory so failures
    /// carry the domain's stable codes and field names.
    /// </summary>
    internal static DomainResult<BookingRequest> ToBookingRequest(PlacementRequestModel model)
    {
        var booker = Booker.Create(
            memberKey: null, model.Booker.Name, model.Booker.Email, model.Booker.Phone);

        if (!booker.Succeeded)
        {
            return DomainResult<BookingRequest>.Failure(booker.Failures);
        }

        return DomainResult<BookingRequest>.Success(new BookingRequest
        {
            ResourceId = model.ResourceId,
            Start = model.Start,
            Duration = TimeSpan.FromMinutes(model.DurationMinutes),
            Booker = booker.Value,
        });
    }

    /// <summary>
    /// Builds a validated <see cref="ServiceBookingRequest"/> from the route's
    /// service id and the request body. The submitted length is carried through
    /// verbatim — Core validates it and never substitutes a permitted one
    /// (book-via-service design D8).
    /// </summary>
    internal static DomainResult<ServiceBookingRequest> ToServiceBookingRequest(
        Guid serviceId, ServicePlacementRequestModel model)
    {
        var booker = Booker.Create(
            memberKey: null, model.Booker.Name, model.Booker.Email, model.Booker.Phone);

        if (!booker.Succeeded)
        {
            return DomainResult<ServiceBookingRequest>.Failure(booker.Failures);
        }

        return DomainResult<ServiceBookingRequest>.Success(new ServiceBookingRequest
        {
            ServiceId = serviceId,
            Start = model.Start,
            // Model validation guarantees the value is present.
            Duration = TimeSpan.FromMinutes(model.DurationMinutes!.Value),
            Booker = booker.Value,
            PreferredResourceId = model.PreferredResourceId,
        });
    }

    private static int? Minutes(TimeSpan? value) => value is { } v ? (int)v.TotalMinutes : null;
}
