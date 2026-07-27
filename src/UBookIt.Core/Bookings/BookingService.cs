using UBookIt.Core.Availability;
using UBookIt.Core.Common;
using UBookIt.Core.Stores;

namespace UBookIt.Core.Bookings;

/// <summary>A request to place a booking. Start is an absolute instant; wall-clock rules are applied in the site zone.</summary>
public sealed record BookingRequest
{
    public required Guid ResourceId { get; init; }

    public required DateTimeOffset Start { get; init; }

    public required TimeSpan Duration { get; init; }

    public required Booker Booker { get; init; }
}

/// <summary>Places and cancels bookings.</summary>
public interface IBookingService
{
    /// <summary>
    /// Runs the placement validation pipeline (bookings spec order) and, when
    /// valid, atomically places an auto-confirmed booking via the store.
    /// </summary>
    Task<DomainResult<Booking>> PlaceAsync(BookingRequest request, CancellationToken cancellationToken = default);

    Task<DomainResult<Booking>> CancelAsync(Guid bookingId, CancellationToken cancellationToken = default);
}

public sealed class BookingService(
    IResourceStore resourceStore,
    IBookingStore bookingStore,
    TimeProvider timeProvider,
    SiteBookingSettings settings) : IBookingService
{
    public async Task<DomainResult<Booking>> PlaceAsync(
        BookingRequest request, CancellationToken cancellationToken = default)
    {
        var zoneResult = AvailabilityService.ResolveZone(settings);
        if (!zoneResult.Succeeded)
        {
            return DomainResult<Booking>.Failure(zoneResult.Failures);
        }

        var zone = zoneResult.Value;

        var resource = await resourceStore.GetAsync(request.ResourceId, cancellationToken).ConfigureAwait(false);
        if (resource is null)
        {
            return DomainResult<Booking>.Failure(
                FailureCodes.ResourceNotFound, $"No resource exists with id {request.ResourceId}.");
        }

        // Rule 1: interval-invalid
        var intervalResult = BookingInterval.Create(request.Start, request.Start + request.Duration, settings.TimeZoneId);
        if (!intervalResult.Succeeded)
        {
            return DomainResult<Booking>.Failure(intervalResult.Failures);
        }

        var interval = intervalResult.Value;
        var constraints = resource.Availability.Constraints;
        var nowUtc = timeProvider.GetUtcNow();
        var failures = new List<DomainFailure>();

        // Rules 2–4: granularity (duration part), duration bounds
        var durationFailure = AvailabilityService.ValidateDuration(request.Duration, constraints);
        if (durationFailure is not null)
        {
            failures.Add(durationFailure);
        }

        // Rule 5: lead-time
        if (interval.StartUtc < nowUtc + constraints.LeadTime)
        {
            failures.Add(new DomainFailure(
                FailureCodes.LeadTime,
                $"Bookings require at least {constraints.LeadTime.TotalMinutes:0} minutes notice."));
        }

        // Rule 6: horizon
        var localStartDate = WallClockMapper.ToLocalDate(interval.StartUtc, zone);
        var lastLocalDate = WallClockMapper.ToLocalDate(nowUtc, zone).AddDays(constraints.HorizonDays);
        if (localStartDate > lastLocalDate)
        {
            failures.Add(new DomainFailure(
                FailureCodes.Horizon,
                $"Bookings may be placed at most {constraints.HorizonDays} days ahead."));
        }

        // Rule 7: outside-open-hours — the interval must fit inside one open
        // window; granularity of the start is relative to its window's start,
        // which keeps placement consistent with slot projection.
        var open = FreeTimeCalculator.OpenIntervals(
            resource.Availability, zone, localStartDate.AddDays(-1), localStartDate.AddDays(1));
        var window = open.FirstOrDefault(w => w.StartUtc <= interval.StartUtc && interval.EndUtc <= w.EndUtc);

        if (window == default)
        {
            failures.Add(new DomainFailure(
                FailureCodes.OutsideOpenHours, "The requested interval is outside the resource's open hours."));
        }
        else if ((interval.StartUtc - window.StartUtc).Ticks % constraints.Granularity.Ticks != 0)
        {
            failures.Add(new DomainFailure(
                FailureCodes.Granularity,
                $"The start time must align to {constraints.Granularity.TotalMinutes:0}-minute steps from the window's start."));
        }

        if (failures.Count > 0)
        {
            return DomainResult<Booking>.Failure(OrderByPipeline(failures));
        }

        // Rule 8: conflict — checked atomically by the store (bookings spec,
        // "Atomic placement contract"). v1 auto-confirms on placement.
        var booking = Booking.Create(
            Guid.NewGuid(), interval, request.Booker, [new ResourceClaim(resource.Id)],
            BookingStatus.Confirmed, nowUtc);

        return await bookingStore.PlaceAsync(booking, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DomainResult<Booking>> CancelAsync(
        Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await bookingStore.GetBookingAsync(bookingId, cancellationToken).ConfigureAwait(false);
        if (booking is null)
        {
            return DomainResult<Booking>.Failure(
                FailureCodes.BookingNotFound, $"No booking exists with id {bookingId}.");
        }

        var transition = booking.Cancel();
        if (!transition.Succeeded)
        {
            return DomainResult<Booking>.Failure(transition.Failures);
        }

        await bookingStore.UpdateAsync(booking, cancellationToken).ConfigureAwait(false);
        return DomainResult<Booking>.Success(booking);
    }

    private static readonly string[] PipelineOrder =
    [
        FailureCodes.IntervalInvalid,
        FailureCodes.Granularity,
        FailureCodes.DurationTooShort,
        FailureCodes.DurationTooLong,
        FailureCodes.LeadTime,
        FailureCodes.Horizon,
        FailureCodes.OutsideOpenHours,
        FailureCodes.Conflict,
    ];

    private static List<DomainFailure> OrderByPipeline(List<DomainFailure> failures)
        => [.. failures.OrderBy(f => Array.IndexOf(PipelineOrder, f.Code))];
}
