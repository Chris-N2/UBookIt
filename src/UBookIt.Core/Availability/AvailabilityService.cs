using UBookIt.Core.Common;
using UBookIt.Core.Resources;
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

    /// <summary>
    /// The same projection as
    /// <see cref="GetBookableStartsAsync(Guid, DateOnly, DateOnly, CancellationToken)"/>,
    /// wholly pure: for a caller that has already loaded the resource and
    /// already read claims for a batch of resources, and so must issue no reads
    /// of its own. Claims for other resources are ignored, so a single batched
    /// read can be passed for every candidate in turn.
    /// <para>
    /// Produces identical results to the id-based query for the same resource,
    /// range, and stored state — it changes only who performs the reads, never
    /// what is computed. This is the seam that lets a service availability query
    /// over N candidates cost one claims round trip instead of N, while sharing
    /// one computation with the async projection so the two cannot drift
    /// (book-via-service design D5).
    /// </para>
    /// </summary>
    DomainResult<IReadOnlyList<BookableStart>> ProjectBookableStarts(
        Resource resource, IReadOnlyList<ClaimInfo> claims, DateOnly fromDate, DateOnly toDate);
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

        return Project(context);
    }

    public DomainResult<IReadOnlyList<BookableStart>> ProjectBookableStarts(
        Resource resource, IReadOnlyList<ClaimInfo> claims, DateOnly fromDate, DateOnly toDate)
    {
        var precondition = ValidatePreconditions(fromDate, toDate);
        if (!precondition.Succeeded)
        {
            return DomainResult<IReadOnlyList<BookableStart>>.Failure(precondition.Failures);
        }

        var zone = precondition.Value;
        var open = FreeTimeCalculator.OpenIntervals(resource.Availability, zone, fromDate, toDate);

        // Claims for other resources are ignored rather than rejected: the point
        // of this overload is that one batched read serves every candidate.
        var free = open.Count == 0
            ? open
            : FreeTimeCalculator.Subtract(open, claims.Where(c => c.ResourceId == resource.Id && IsBlocking(c)));

        return Project(Ok(resource.Availability.Constraints, zone, free));
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

    internal static bool IsBlocking(ClaimInfo claim)
        => claim.Status is Bookings.BookingStatus.Requested or Bookings.BookingStatus.Confirmed;

    private DomainResult<IReadOnlyList<BookableStart>> Project(DomainResult<Context> context)
    {
        if (!context.Succeeded)
        {
            return DomainResult<IReadOnlyList<BookableStart>>.Failure(context.Failures);
        }

        var (constraints, zone, free) = context.Value;

        var starts = SlotProjector.ProjectBookableStarts(free, constraints, timeProvider.GetUtcNow(), zone);
        return DomainResult<IReadOnlyList<BookableStart>>.Success(starts);
    }

    private DomainResult<TimeZoneInfo> ValidatePreconditions(DateOnly fromDate, DateOnly toDate)
        => ValidateQueryPreconditions(settings, fromDate, toDate);

    /// <summary>
    /// Range and time-zone checks, which precede any resource load. Shared so
    /// every availability-shaped query — per-resource or over a service's
    /// candidate pool — rejects a bad range identically and before any work.
    /// </summary>
    internal static DomainResult<TimeZoneInfo> ValidateQueryPreconditions(
        SiteBookingSettings settings, DateOnly fromDate, DateOnly toDate)
    {
        if (fromDate > toDate)
        {
            return DomainResult<TimeZoneInfo>.Failure(
                FailureCodes.DateRangeInvalid, "The from date must not be after the to date.");
        }

        // Bounded query range (availability spec): reject an over-wide span
        // before any work — zone resolution, resource load, and the day-by-day
        // open-hours computation all follow — so an unbounded range costs nothing.
        // Checked ahead of the walkability guard below so that an over-wide range
        // reports as over-wide, which is the more useful answer, even when it also
        // happens to end at the calendar's limit.
        var spanDays = toDate.DayNumber - fromDate.DayNumber + 1;
        if (spanDays > settings.MaxQueryRangeDays)
        {
            return DomainResult<TimeZoneInfo>.Failure(
                FailureCodes.DateRangeTooLarge,
                $"The queried date range spans {spanDays} days, which exceeds the maximum of {settings.MaxQueryRangeDays}.");
        }

        // The range must also lie clear of the calendar's edges
        // (out-of-range-dates design D1). Bounding the span is not enough: a
        // range at either edge can be one day wide and pass the span check.
        //
        // At the end: the day-by-day open-hours walk increments once past its
        // final day and steps off the calendar.
        //
        // At the start: mapping a wall-clock time on the first representable date
        // to UTC subtracts the zone's offset, and for any site zone east of UTC
        // that lands before year one, which `DateTimeOffset` refuses to
        // represent. Rejecting both ends keeps the rule zone-independent rather
        // than working in London and throwing in Auckland.
        if (!CalendarBounds.IsUtcMappable(fromDate))
        {
            return DomainResult<TimeZoneInfo>.Failure(
                FailureCodes.DateRangeInvalid,
                $"The from date must be later than {DateOnly.MinValue:yyyy-MM-dd}.");
        }

        if (!CalendarBounds.IsWalkableTo(toDate))
        {
            return DomainResult<TimeZoneInfo>.Failure(
                FailureCodes.DateRangeInvalid,
                $"The to date must be earlier than {DateOnly.MaxValue:yyyy-MM-dd}.");
        }

        return ResolveZone(settings);
    }

    private async Task<DomainResult<Context>> ResolveAsync(
        Guid resourceId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
    {
        var precondition = ValidatePreconditions(fromDate, toDate);
        if (!precondition.Succeeded)
        {
            return DomainResult<Context>.Failure(precondition.Failures);
        }

        var resource = await resourceStore.GetAsync(resourceId, cancellationToken).ConfigureAwait(false);
        if (resource is null)
        {
            return DomainResult<Context>.Failure(
                FailureCodes.ResourceNotFound, $"No resource exists with id {resourceId}.");
        }

        return await ResolveForAsync(resource, precondition.Value, fromDate, toDate, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<DomainResult<Context>> ResolveForAsync(
        Resource resource, TimeZoneInfo zone, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
    {
        var open = FreeTimeCalculator.OpenIntervals(resource.Availability, zone, fromDate, toDate);

        if (open.Count == 0)
        {
            return Ok(resource.Availability.Constraints, zone, open);
        }

        var claims = await bookingStore
            .GetClaimsAsync(resource.Id, open[0].StartUtc, open[^1].EndUtc, cancellationToken)
            .ConfigureAwait(false);

        return Ok(resource.Availability.Constraints, zone, FreeTimeCalculator.Subtract(open, claims.Where(IsBlocking)));
    }

    private static DomainResult<Context> Ok(
        BookingConstraints constraints, TimeZoneInfo zone, IReadOnlyList<UtcInterval> free)
        => DomainResult<Context>.Success(new Context(constraints, zone, free));

    private sealed record Context(BookingConstraints Constraints, TimeZoneInfo Zone, IReadOnlyList<UtcInterval> Free);
}
