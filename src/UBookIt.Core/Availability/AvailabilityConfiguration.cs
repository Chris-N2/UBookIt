using UBookIt.Core.Common;

namespace UBookIt.Core.Availability;

/// <summary>
/// A resource's complete availability definition: weekly open hours,
/// date-specific exceptions (at most one per date), booking constraints, and the
/// site closures that apply to it.
/// </summary>
/// <remarks>
/// <b>Three layers, consulted in one order.</b> A date resolves to an applicable
/// site closure first, then the resource's own exception, then the weekly pattern —
/// see <see cref="EffectiveWindows"/>. That order lives here and nowhere else:
/// every availability route in the package reaches a date's windows through this
/// type, so a second implementation of precedence would be a second answer.
/// <para>
/// <b>Closures are held apart from <see cref="Exceptions"/> deliberately.</b> The
/// resource write path persists <see cref="Exceptions"/>; if closures were merged
/// into it, saving a resource would write every inherited closure back as an
/// exception the resource owns, outliving the closure itself. Keeping them in
/// separate collections makes that impossible rather than merely avoided.
/// </para>
/// </remarks>
public sealed class AvailabilityConfiguration
{
    private readonly IReadOnlyDictionary<DateOnly, DateException> _exceptions;

    private readonly IReadOnlyDictionary<DateOnly, SiteClosure> _closures;

    private AvailabilityConfiguration(
        WeeklyOpenHours openHours,
        IReadOnlyDictionary<DateOnly, DateException> exceptions,
        BookingConstraints constraints,
        IReadOnlyDictionary<DateOnly, SiteClosure> closures)
    {
        OpenHours = openHours;
        _exceptions = exceptions;
        Constraints = constraints;
        _closures = closures;
    }

    public WeeklyOpenHours OpenHours { get; }

    public IReadOnlyCollection<DateException> Exceptions => (IReadOnlyCollection<DateException>)_exceptions.Values;

    public BookingConstraints Constraints { get; }

    /// <summary>
    /// The site closures that apply to this resource — those it has not opted out
    /// of. A resource's opt-outs are resolved before a configuration is built, so
    /// everything here closes a date.
    /// </summary>
    public IReadOnlyCollection<SiteClosure> Closures => (IReadOnlyCollection<SiteClosure>)_closures.Values;

    /// <summary>A never-bookable configuration (closed every day, default constraints).</summary>
    public static AvailabilityConfiguration Closed { get; } = new(
        WeeklyOpenHours.Empty,
        new Dictionary<DateOnly, DateException>(),
        BookingConstraints.Default,
        new Dictionary<DateOnly, SiteClosure>());

    public DateException? ExceptionFor(DateOnly date)
        => _exceptions.TryGetValue(date, out var exception) ? exception : null;

    /// <summary>
    /// The applicable site closure for a date, if any. Public because the closure a
    /// date carries decides whether the resource's own exception has any effect,
    /// and that determination is made once, here, rather than by each consumer.
    /// </summary>
    public SiteClosure? ClosureFor(DateOnly date)
        => _closures.TryGetValue(date, out var closure) ? closure : null;

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
    /// it, otherwise the exception's windows when one exists, otherwise the weekly
    /// pattern's windows for that day of week.
    /// </summary>
    public IReadOnlyList<DayWindow> EffectiveWindows(DateOnly date)
        => ClosureFor(date) is not null
            ? []
            : ExceptionFor(date)?.Windows ?? OpenHours.WindowsFor(date.DayOfWeek);

    public static DomainResult<AvailabilityConfiguration> Create(
        WeeklyOpenHours openHours,
        IEnumerable<DateException>? exceptions = null,
        BookingConstraints? constraints = null,
        IEnumerable<SiteClosure>? closures = null)
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

        var closuresByDate = new Dictionary<DateOnly, SiteClosure>();

        foreach (var closure in closures ?? [])
        {
            // Rejected rather than deduplicated: two closures on one date can only
            // repeat or contradict each other, and silently keeping one would hide
            // whichever the caller did not mean.
            if (!closuresByDate.TryAdd(closure.Date, closure))
            {
                return DomainResult<AvailabilityConfiguration>.Failure(
                    FailureCodes.DuplicateClosureDate,
                    $"More than one site closure is defined for {closure.Date:yyyy-MM-dd}.");
            }
        }

        return DomainResult<AvailabilityConfiguration>.Success(
            new AvailabilityConfiguration(
                openHours, byDate, constraints ?? BookingConstraints.Default, closuresByDate));
    }
}
