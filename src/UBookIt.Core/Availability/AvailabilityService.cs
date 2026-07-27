using UBookIt.Core.Common;
using UBookIt.Core.Stores;

namespace UBookIt.Core.Availability;

/// <summary>Free-time and slot-projection queries for a resource over a date range (inclusive).</summary>
public interface IAvailabilityQueryService
{
    Task<DomainResult<IReadOnlyList<UtcInterval>>> GetFreeTimeAsync(
        Guid resourceId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);

    Task<DomainResult<IReadOnlyList<Slot>>> GetSlotsAsync(
        Guid resourceId, DateOnly fromDate, DateOnly toDate, TimeSpan duration,
        CancellationToken cancellationToken = default);
}

public sealed class AvailabilityService(
    IResourceStore resourceStore,
    IBookingStore bookingStore,
    TimeProvider timeProvider,
    SiteBookingSettings settings) : IAvailabilityQueryService
{
    public async Task<DomainResult<IReadOnlyList<UtcInterval>>> GetFreeTimeAsync(
        Guid resourceId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        var context = await ResolveAsync(resourceId, fromDate, toDate, cancellationToken).ConfigureAwait(false);

        return context.Succeeded
            ? DomainResult<IReadOnlyList<UtcInterval>>.Success(context.Value.Free)
            : DomainResult<IReadOnlyList<UtcInterval>>.Failure(context.Failures);
    }

    public async Task<DomainResult<IReadOnlyList<Slot>>> GetSlotsAsync(
        Guid resourceId, DateOnly fromDate, DateOnly toDate, TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        var context = await ResolveAsync(resourceId, fromDate, toDate, cancellationToken).ConfigureAwait(false);

        if (!context.Succeeded)
        {
            return DomainResult<IReadOnlyList<Slot>>.Failure(context.Failures);
        }

        var (constraints, zone, free) = context.Value;

        var durationFailure = ValidateDuration(duration, constraints);
        if (durationFailure is not null)
        {
            return DomainResult<IReadOnlyList<Slot>>.Failure(durationFailure);
        }

        var slots = SlotProjector.Project(free, constraints, duration, timeProvider.GetUtcNow(), zone);
        return DomainResult<IReadOnlyList<Slot>>.Success(slots);
    }

    internal static DomainFailure? ValidateDuration(TimeSpan duration, BookingConstraints constraints)
    {
        if (duration <= TimeSpan.Zero || duration.Ticks % constraints.Granularity.Ticks != 0)
        {
            return new DomainFailure(
                FailureCodes.Granularity,
                $"The duration must be a positive multiple of {constraints.Granularity.TotalMinutes:0} minutes.");
        }

        if (duration < constraints.MinDuration)
        {
            return new DomainFailure(
                FailureCodes.DurationTooShort,
                $"The duration must be at least {constraints.MinDuration.TotalMinutes:0} minutes.");
        }

        if (duration > constraints.MaxDuration)
        {
            return new DomainFailure(
                FailureCodes.DurationTooLong,
                $"The duration must be at most {constraints.MaxDuration.TotalMinutes:0} minutes.");
        }

        return null;
    }

    internal static DomainResult<TimeZoneInfo> ResolveZone(SiteBookingSettings settings)
    {
        try
        {
            return DomainResult<TimeZoneInfo>.Success(TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId));
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return DomainResult<TimeZoneInfo>.Failure(
                FailureCodes.TimeZoneInvalid, $"Unknown or invalid time zone id '{settings.TimeZoneId}'.");
        }
    }

    private async Task<DomainResult<(BookingConstraints Constraints, TimeZoneInfo Zone, IReadOnlyList<UtcInterval> Free)>>
        ResolveAsync(Guid resourceId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
    {
        if (fromDate > toDate)
        {
            return Fail(FailureCodes.DateRangeInvalid, "The from date must not be after the to date.");
        }

        var zoneResult = ResolveZone(settings);
        if (!zoneResult.Succeeded)
        {
            return DomainResult<(BookingConstraints, TimeZoneInfo, IReadOnlyList<UtcInterval>)>.Failure(zoneResult.Failures);
        }

        var zone = zoneResult.Value;

        var resource = await resourceStore.GetAsync(resourceId, cancellationToken).ConfigureAwait(false);
        if (resource is null)
        {
            return Fail(FailureCodes.ResourceNotFound, $"No resource exists with id {resourceId}.");
        }

        var open = FreeTimeCalculator.OpenIntervals(resource.Availability, zone, fromDate, toDate);

        if (open.Count == 0)
        {
            return Ok(resource.Availability.Constraints, zone, open);
        }

        var claims = await bookingStore
            .GetClaimsAsync(resourceId, open[0].StartUtc, open[^1].EndUtc, cancellationToken)
            .ConfigureAwait(false);

        var blocking = claims.Where(c =>
            c.Status is Bookings.BookingStatus.Requested or Bookings.BookingStatus.Confirmed);

        return Ok(resource.Availability.Constraints, zone, FreeTimeCalculator.Subtract(open, blocking));

        static DomainResult<(BookingConstraints, TimeZoneInfo, IReadOnlyList<UtcInterval>)> Fail(string code, string message)
            => DomainResult<(BookingConstraints, TimeZoneInfo, IReadOnlyList<UtcInterval>)>.Failure(code, message);

        static DomainResult<(BookingConstraints, TimeZoneInfo, IReadOnlyList<UtcInterval>)> Ok(
            BookingConstraints constraints, TimeZoneInfo zone, IReadOnlyList<UtcInterval> free)
            => DomainResult<(BookingConstraints, TimeZoneInfo, IReadOnlyList<UtcInterval>)>.Success((constraints, zone, free));
    }
}
