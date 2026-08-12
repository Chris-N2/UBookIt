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

    /// <summary>
    /// Every bookable start over the range with the shortest and longest length
    /// bookable from it. Takes no duration — answering "how long can I book from
    /// here" is its purpose — and is a strict superset of
    /// <see cref="GetSlotsAsync"/>: the starts for any duration are those whose
    /// range admits it.
    /// </summary>
    Task<DomainResult<IReadOnlyList<BookableStart>>> GetBookableStartsAsync(
        Guid resourceId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);
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

        var durationFailures = ValidateDuration(duration, constraints);
        if (durationFailures.Count > 0)
        {
            return DomainResult<IReadOnlyList<Slot>>.Failure(durationFailures);
        }

        var slots = SlotProjector.Project(free, constraints, duration, timeProvider.GetUtcNow(), zone);
        return DomainResult<IReadOnlyList<Slot>>.Success(slots);
    }

    public async Task<DomainResult<IReadOnlyList<BookableStart>>> GetBookableStartsAsync(
        Guid resourceId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        // Shares ResolveAsync with the other queries, so range, resource and
        // time-zone failures behave identically across the availability surface.
        var context = await ResolveAsync(resourceId, fromDate, toDate, cancellationToken).ConfigureAwait(false);

        if (!context.Succeeded)
        {
            return DomainResult<IReadOnlyList<BookableStart>>.Failure(context.Failures);
        }

        var (constraints, zone, free) = context.Value;

        var starts = SlotProjector.ProjectBookableStarts(free, constraints, timeProvider.GetUtcNow(), zone);
        return DomainResult<IReadOnlyList<BookableStart>>.Success(starts);
    }

    // One code per failed rule (bookings spec, "Placement validation pipeline"):
    // an unaligned out-of-bounds duration reports both codes, not just the first.
    internal static List<DomainFailure> ValidateDuration(TimeSpan duration, BookingConstraints constraints)
    {
        var failures = new List<DomainFailure>();

        if (duration <= TimeSpan.Zero)
        {
            failures.Add(new DomainFailure(
                FailureCodes.Granularity,
                $"The duration must be a positive multiple of {constraints.Granularity.TotalMinutes:0} minutes."));
            return failures;
        }

        if (duration.Ticks % constraints.Granularity.Ticks != 0)
        {
            failures.Add(new DomainFailure(
                FailureCodes.Granularity,
                $"The duration must be a positive multiple of {constraints.Granularity.TotalMinutes:0} minutes."));
        }

        if (duration < constraints.MinDuration)
        {
            failures.Add(new DomainFailure(
                FailureCodes.DurationTooShort,
                $"The duration must be at least {constraints.MinDuration.TotalMinutes:0} minutes."));
        }

        if (duration > constraints.MaxDuration)
        {
            failures.Add(new DomainFailure(
                FailureCodes.DurationTooLong,
                $"The duration must be at most {constraints.MaxDuration.TotalMinutes:0} minutes."));
        }

        return failures;
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

        // Bounded query range (availability spec): reject an over-wide span
        // before any work — zone resolution, resource load, and the day-by-day
        // open-hours computation all follow — so an unbounded range costs nothing.
        var spanDays = toDate.DayNumber - fromDate.DayNumber + 1;
        if (spanDays > settings.MaxQueryRangeDays)
        {
            return Fail(
                FailureCodes.DateRangeTooLarge,
                $"The queried date range spans {spanDays} days, which exceeds the maximum of {settings.MaxQueryRangeDays}.");
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
