using UBookIt.Core.Availability;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
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

/// <summary>
/// A request to place one booking claiming several resources at once — the
/// shape a multi-role service resolves to. All claims share the booking's single
/// interval, which is what the store's atomic contract is defined over.
/// </summary>
public sealed record MultiClaimBookingRequest
{
    /// <summary>
    /// The resources to claim, one per role. Every one of them is validated
    /// against its own constraints: a booking is placed only where each
    /// resource would have accepted it on its own.
    /// </summary>
    public required IReadOnlyList<Guid> ResourceIds { get; init; }

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

    /// <summary>
    /// The same pipeline for a booking claiming several resources: every
    /// resource is validated against its own constraints, and one booking
    /// carrying a claim for each is placed through the store's all-or-nothing
    /// contract.
    /// <para>
    /// The single-resource overload delegates here rather than duplicating the
    /// rules, so direct placement and service placement cannot drift apart.
    /// </para>
    /// </summary>
    Task<DomainResult<Booking>> PlaceAsync(
        MultiClaimBookingRequest request, CancellationToken cancellationToken = default);

    Task<DomainResult<Booking>> CancelAsync(Guid bookingId, CancellationToken cancellationToken = default);
}

public sealed class BookingService(
    IResourceStore resourceStore,
    IBookingStore bookingStore,
    TimeProvider timeProvider,
    SiteBookingSettings settings) : IBookingService
{
    public Task<DomainResult<Booking>> PlaceAsync(
        BookingRequest request, CancellationToken cancellationToken = default)
        => PlaceAsync(
            new MultiClaimBookingRequest
            {
                ResourceIds = [request.ResourceId],
                Start = request.Start,
                Duration = request.Duration,
                Booker = request.Booker,
            },
            cancellationToken);

    public async Task<DomainResult<Booking>> PlaceAsync(
        MultiClaimBookingRequest request, CancellationToken cancellationToken = default)
    {
        var zoneResult = AvailabilityService.ResolveZone(settings);
        if (!zoneResult.Succeeded)
        {
            return DomainResult<Booking>.Failure(zoneResult.Failures);
        }

        var zone = zoneResult.Value;

        if (request.ResourceIds.Count == 0)
        {
            return DomainResult<Booking>.Failure(
                FailureCodes.ClaimsInvalid, "A booking must claim at least one resource.");
        }

        // Loaded before the interval rules, in the order supplied, so that an
        // unknown resource is still reported as such rather than masked by
        // whatever else the request got wrong.
        var resources = new List<Resource>(request.ResourceIds.Count);

        foreach (var resourceId in request.ResourceIds)
        {
            var loaded = await resourceStore.GetAsync(resourceId, cancellationToken).ConfigureAwait(false);
            if (loaded is null)
            {
                return DomainResult<Booking>.Failure(
                    FailureCodes.ResourceNotFound, $"No resource exists with id {resourceId}.");
            }

            resources.Add(loaded);
        }

        // Rule 1: interval-invalid — including an interval that cannot be
        // represented at all. The addition is guarded rather than attempted:
        // unguarded it throws before `BookingInterval.Create` gets the chance to
        // reject it, which is an unhandled exception out of an anonymous
        // endpoint rather than a structured failure (out-of-range-dates D2).
        if (!CalendarBounds.TryAdd(request.Start, request.Duration, out var end))
        {
            return DomainResult<Booking>.Failure(
                FailureCodes.IntervalInvalid,
                "The requested start and duration do not describe a representable interval.");
        }

        var intervalResult = BookingInterval.Create(request.Start, end, settings.TimeZoneId);
        if (!intervalResult.Succeeded)
        {
            return DomainResult<Booking>.Failure(intervalResult.Failures);
        }

        var interval = intervalResult.Value;
        var localStartDate = WallClockMapper.ToLocalDate(interval.StartUtc, zone);

        // Still rule 1: the open-hours rule below inspects the day either side of
        // the start, and the walk steps once past the later of them. At the edges
        // of the calendar that window cannot be formed, so the request cannot be
        // evaluated — reported as an unrepresentable interval rather than thrown
        // (design D3). Checked here, ahead of the accumulating rules, because
        // `interval-invalid` is first in the documented pipeline order.
        if (!CalendarBounds.TryWindowAround(localStartDate, out var windowFrom, out var windowTo))
        {
            return DomainResult<Booking>.Failure(
                FailureCodes.IntervalInvalid,
                "The requested start is too close to the limits of the calendar to be evaluated.");
        }

        var nowUtc = timeProvider.GetUtcNow();
        var failures = new List<DomainFailure>();

        // Rules 2–7 are properties of a resource, so they run once per claimed
        // resource: a booking is placed only where every one of them would have
        // accepted it alone. For a single-claim request this is exactly the
        // pipeline it has always been.
        foreach (var resource in resources)
        {
            var constraints = resource.Availability.Constraints;

            // Rules 2–4: granularity (duration part), duration bounds
            failures.AddRange(AvailabilityService.ValidateDuration(request.Duration, constraints));

            // Rule 5: lead-time
            if (interval.StartUtc < nowUtc + constraints.LeadTime)
            {
                failures.Add(new DomainFailure(
                    FailureCodes.LeadTime,
                    $"Bookings require at least {constraints.LeadTime.TotalMinutes:0} minutes notice."));
            }

            // Rule 6: horizon. Saturating, because a horizon reaching past the end of
            // the calendar means "no effective limit" rather than an error — and
            // `HorizonDays` is only validated as positive, so a large one would
            // otherwise throw for every request against that resource.
            var lastLocalDate = CalendarBounds.AddDaysSaturating(
                WallClockMapper.ToLocalDate(nowUtc, zone), constraints.HorizonDays);
            if (localStartDate > lastLocalDate)
            {
                failures.Add(new DomainFailure(
                    FailureCodes.Horizon,
                    $"Bookings may be placed at most {constraints.HorizonDays} days ahead."));
            }

            // Rule 7: outside-open-hours — the interval must fit inside one open
            // window; granularity of the start is relative to its window's start,
            // which keeps placement consistent with slot projection.
            var open = FreeTimeCalculator.OpenIntervals(resource.Availability, zone, windowFrom, windowTo);
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
        }

        if (failures.Count > 0)
        {
            return DomainResult<Booking>.Failure(OrderByPipeline(failures));
        }

        // Rule 8: conflict — checked atomically by the store across every claimed
        // resource (bookings spec, "Atomic placement contract"). v1 auto-confirms
        // on placement.
        var booking = Booking.Create(
            Guid.NewGuid(),
            interval,
            request.Booker,
            [.. resources.Select(r => new ResourceClaim(r.Id))],
            BookingStatus.Confirmed,
            nowUtc);

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
