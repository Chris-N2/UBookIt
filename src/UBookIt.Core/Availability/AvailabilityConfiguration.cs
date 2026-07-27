using UBookIt.Core.Common;

namespace UBookIt.Core.Availability;

/// <summary>
/// A resource's complete availability definition: weekly open hours,
/// date-specific exceptions (at most one per date), and booking constraints.
/// </summary>
public sealed class AvailabilityConfiguration
{
    private readonly IReadOnlyDictionary<DateOnly, DateException> _exceptions;

    private AvailabilityConfiguration(
        WeeklyOpenHours openHours,
        IReadOnlyDictionary<DateOnly, DateException> exceptions,
        BookingConstraints constraints)
    {
        OpenHours = openHours;
        _exceptions = exceptions;
        Constraints = constraints;
    }

    public WeeklyOpenHours OpenHours { get; }

    public IReadOnlyCollection<DateException> Exceptions => (IReadOnlyCollection<DateException>)_exceptions.Values;

    public BookingConstraints Constraints { get; }

    /// <summary>A never-bookable configuration (closed every day, default constraints).</summary>
    public static AvailabilityConfiguration Closed { get; } = new(
        WeeklyOpenHours.Empty,
        new Dictionary<DateOnly, DateException>(),
        BookingConstraints.Default);

    public DateException? ExceptionFor(DateOnly date)
        => _exceptions.TryGetValue(date, out var exception) ? exception : null;

    /// <summary>
    /// The windows in effect for a date: the exception's windows when one exists,
    /// otherwise the weekly pattern's windows for that day of week.
    /// </summary>
    public IReadOnlyList<DayWindow> EffectiveWindows(DateOnly date)
        => ExceptionFor(date)?.Windows ?? OpenHours.WindowsFor(date.DayOfWeek);

    public static DomainResult<AvailabilityConfiguration> Create(
        WeeklyOpenHours openHours,
        IEnumerable<DateException>? exceptions = null,
        BookingConstraints? constraints = null)
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

        return DomainResult<AvailabilityConfiguration>.Success(
            new AvailabilityConfiguration(openHours, byDate, constraints ?? BookingConstraints.Default));
    }
}
