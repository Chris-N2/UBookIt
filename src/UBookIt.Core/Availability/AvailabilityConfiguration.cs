using UBookIt.Core.Common;

namespace UBookIt.Core.Availability;

/// <summary>
/// A resource's complete availability definition: weekly open hours,
/// date-specific exceptions (at most one per date), closed periods, booking
/// constraints, and the site closures that apply to it.
/// </summary>
/// <remarks>
/// <b>Three layers, consulted in one order.</b> A date resolves to an applicable
/// site closure first, then the resource's own rule — its exception or its closed
/// period, which never share a date — then the weekly pattern — see
/// <see cref="EffectiveWindows"/>. That order lives here and nowhere else:
/// every availability route in the package reaches a date's windows through this
/// type, so a second implementation of precedence would be a second answer.
/// <para>
/// <b>Closures are held apart from <see cref="Exceptions"/> deliberately.</b> The
/// resource write path persists <see cref="Exceptions"/>; if closures were merged
/// into it, saving a resource would write every inherited closure back as an
/// exception the resource owns, outliving the closure itself. Keeping them in
/// separate collections makes that impossible rather than merely avoided.
/// </para>
/// <para>
/// <b>Ranges are scanned, never expanded into per-date entries.</b> A closure or a
/// closed period may run for years; expanding it would make every read cost the length
/// of the longest range rather than the dates it asks about. A site holds few enough
/// ranges that a scan is negligible.
/// </para>
/// </remarks>
public sealed class AvailabilityConfiguration
{
    private readonly IReadOnlyDictionary<DateOnly, DateException> _exceptions;

    private readonly IReadOnlyList<SiteClosure> _closures;

    private readonly IReadOnlyList<ClosedPeriod> _closedPeriods;

    private AvailabilityConfiguration(
        WeeklyOpenHours openHours,
        IReadOnlyDictionary<DateOnly, DateException> exceptions,
        BookingConstraints constraints,
        IReadOnlyList<SiteClosure> closures,
        IReadOnlyList<ClosedPeriod> closedPeriods)
    {
        OpenHours = openHours;
        _exceptions = exceptions;
        Constraints = constraints;
        _closures = closures;
        _closedPeriods = closedPeriods;
    }

    public WeeklyOpenHours OpenHours { get; }

    public IReadOnlyCollection<DateException> Exceptions => (IReadOnlyCollection<DateException>)_exceptions.Values;

    public BookingConstraints Constraints { get; }

    /// <summary>
    /// The site closures that apply to this resource — those it has not opted out
    /// of. A resource's opt-outs are resolved before a configuration is built, so
    /// everything here closes a date.
    /// </summary>
    public IReadOnlyCollection<SiteClosure> Closures => (IReadOnlyCollection<SiteClosure>)_closures;

    /// <summary>The resource's own closed periods, ordered by start date.</summary>
    public IReadOnlyCollection<ClosedPeriod> ClosedPeriods => (IReadOnlyCollection<ClosedPeriod>)_closedPeriods;

    /// <summary>A never-bookable configuration (closed every day, default constraints).</summary>
    public static AvailabilityConfiguration Closed { get; } = new(
        WeeklyOpenHours.Empty,
        new Dictionary<DateOnly, DateException>(),
        BookingConstraints.Default,
        [],
        []);

    public DateException? ExceptionFor(DateOnly date)
        => _exceptions.TryGetValue(date, out var exception) ? exception : null;

    /// <summary>
    /// The applicable site closure for a date, if any. Public because the closure a
    /// date carries decides whether the resource's own exception has any effect,
    /// and that determination is made once, here, rather than by each consumer.
    /// </summary>
    public SiteClosure? ClosureFor(DateOnly date)
    {
        foreach (var closure in _closures)
        {
            if (closure.Covers(date))
            {
                return closure;
            }
        }

        return null;
    }

    /// <summary>The resource's own closed period covering a date, if any.</summary>
    public ClosedPeriod? ClosedPeriodFor(DateOnly date)
    {
        foreach (var period in _closedPeriods)
        {
            if (period.Covers(date))
            {
                return period;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether the resource's own exception for a date is currently superseded by a
    /// site closure — that is, whether the exception would change the date's
    /// outcome if the closure were not there.
    /// </summary>
    /// <remarks>
    /// <b>False when the exception is itself a closure.</b> Such a date is closed
    /// either way, so reporting it as superseded would describe a difference that
    /// does not exist. The backoffice states the superseded condition to an editor,
    /// and a statement about a difference must be true of a difference.
    /// </remarks>
    public bool IsExceptionSuperseded(DateOnly date)
        => ClosureFor(date) is not null && ExceptionFor(date) is { IsClosure: false };

    /// <summary>
    /// The windows in effect for a date: none when an applicable site closure covers
    /// it, otherwise the exception's windows when one exists, otherwise none when one of
    /// the resource's closed periods covers it, otherwise the weekly pattern's windows
    /// for that day of week.
    /// </summary>
    /// <remarks>
    /// The exception is consulted before the closed period only so the existing branch
    /// stays as it was. The order cannot change an answer: <see cref="Create"/> refuses a
    /// closed period covering a date the resource carries an exception for.
    /// </remarks>
    public IReadOnlyList<DayWindow> EffectiveWindows(DateOnly date)
    {
        if (ClosureFor(date) is not null)
        {
            return [];
        }

        if (ExceptionFor(date) is { } exception)
        {
            return exception.Windows;
        }

        return ClosedPeriodFor(date) is not null ? [] : OpenHours.WindowsFor(date.DayOfWeek);
    }

    public static DomainResult<AvailabilityConfiguration> Create(
        WeeklyOpenHours openHours,
        IEnumerable<DateException>? exceptions = null,
        BookingConstraints? constraints = null,
        IEnumerable<SiteClosure>? closures = null,
        IEnumerable<ClosedPeriod>? closedPeriods = null)
    {
        var byDate = new Dictionary<DateOnly, DateException>();

        foreach (var exception in exceptions ?? [])
        {
            if (!byDate.TryAdd(exception.Date, exception))
            {
                return DomainResult<AvailabilityConfiguration>.Failure(
                    FailureCodes.DuplicateExceptionDate,
                    $"More than one exception is defined for {exception.Date:yyyy-MM-dd}.");
            }
        }

        var orderedClosures = (closures ?? []).OrderBy(c => c.Date).ToArray();
        var furthestEnd = DateOnly.MinValue;

        foreach (var closure in orderedClosures)
        {
            // Rejected rather than deduplicated: two closures covering one date can only
            // repeat or contradict each other, and silently keeping one would hide
            // whichever the caller did not mean. Sorted by start, a closure overlaps an
            // earlier one exactly when it starts on or before the furthest end seen so far.
            if (closure.Date <= furthestEnd)
            {
                return DomainResult<AvailabilityConfiguration>.Failure(
                    FailureCodes.DuplicateClosureDate,
                    $"More than one site closure covers {closure.Date:yyyy-MM-dd}.");
            }

            furthestEnd = closure.EndDate > furthestEnd ? closure.EndDate : furthestEnd;
        }

        var orderedPeriods = (closedPeriods ?? []).OrderBy(p => p.StartDate).ToArray();
        furthestEnd = DateOnly.MinValue;

        foreach (var period in orderedPeriods)
        {
            if (period.StartDate <= furthestEnd)
            {
                return DomainResult<AvailabilityConfiguration>.Failure(
                    FailureCodes.ClosedPeriodOverlap,
                    $"More than one closed period covers {period.StartDate:yyyy-MM-dd}.",
                    "ClosedPeriods");
            }

            furthestEnd = period.EndDate;

            // Judged against the exceptions by date rather than by expanding the period:
            // the exception set is per date and bounded by what an editor typed.
            var conflicts = byDate.Keys.Where(period.Covers).Order().ToArray();

            if (conflicts.Length > 0)
            {
                return DomainResult<AvailabilityConfiguration>.Failure(
                    FailureCodes.ClosedPeriodConflictsWithException,
                    $"The closed period from {period.StartDate:yyyy-MM-dd} to {period.EndDate:yyyy-MM-dd} covers "
                    + $"{conflicts[0]:yyyy-MM-dd}, which has an exception of its own.",
                    "ClosedPeriods");
            }
        }

        return DomainResult<AvailabilityConfiguration>.Success(
            new AvailabilityConfiguration(
                openHours, byDate, constraints ?? BookingConstraints.Default, orderedClosures, orderedPeriods));
    }
}
