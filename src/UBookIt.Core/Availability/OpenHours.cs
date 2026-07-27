using UBookIt.Core.Common;

namespace UBookIt.Core.Availability;

/// <summary>
/// A wall-clock window within a single day, half-open [Start, End).
/// Windows never span midnight.
/// </summary>
public readonly record struct DayWindow
{
    private DayWindow(TimeOnly start, TimeOnly end)
    {
        Start = start;
        End = end;
    }

    public TimeOnly Start { get; }

    public TimeOnly End { get; }

    public static DomainResult<DayWindow> Create(TimeOnly start, TimeOnly end)
        => start < end
            ? DomainResult<DayWindow>.Success(new DayWindow(start, end))
            : DomainResult<DayWindow>.Failure(
                FailureCodes.WindowInvalid,
                $"Window start ({start:HH\\:mm}) must be before its end ({end:HH\\:mm}).");
}

/// <summary>
/// Weekly open-hours pattern: zero or more non-overlapping windows per day of week.
/// A day with no windows is closed.
/// </summary>
public sealed class WeeklyOpenHours
{
    private static readonly IReadOnlyList<DayWindow> NoWindows = [];

    private readonly IReadOnlyDictionary<DayOfWeek, IReadOnlyList<DayWindow>> _days;

    private WeeklyOpenHours(IReadOnlyDictionary<DayOfWeek, IReadOnlyList<DayWindow>> days) => _days = days;

    /// <summary>A pattern with every day closed.</summary>
    public static WeeklyOpenHours Empty { get; } =
        new(new Dictionary<DayOfWeek, IReadOnlyList<DayWindow>>());

    public IReadOnlyList<DayWindow> WindowsFor(DayOfWeek day)
        => _days.TryGetValue(day, out var windows) ? windows : NoWindows;

    public static DomainResult<WeeklyOpenHours> Create(IEnumerable<(DayOfWeek Day, DayWindow Window)> windows)
    {
        var days = new Dictionary<DayOfWeek, IReadOnlyList<DayWindow>>();

        foreach (var group in windows.GroupBy(w => w.Day))
        {
            var ordered = group.Select(w => w.Window).OrderBy(w => w.Start).ToArray();

            for (var i = 1; i < ordered.Length; i++)
            {
                if (ordered[i].Start < ordered[i - 1].End)
                {
                    return DomainResult<WeeklyOpenHours>.Failure(
                        FailureCodes.WindowsOverlap,
                        $"Windows on {group.Key} overlap: " +
                        $"{ordered[i - 1].Start:HH\\:mm}–{ordered[i - 1].End:HH\\:mm} and {ordered[i].Start:HH\\:mm}–{ordered[i].End:HH\\:mm}.");
                }
            }

            days[group.Key] = ordered;
        }

        return DomainResult<WeeklyOpenHours>.Success(new WeeklyOpenHours(days));
    }
}

/// <summary>
/// A date-specific exception to the weekly pattern: either a closure (no windows)
/// or a full replacement of that date's windows. Takes precedence over the weekly
/// pattern for its date.
/// </summary>
public sealed class DateException
{
    private DateException(DateOnly date, IReadOnlyList<DayWindow> windows)
    {
        Date = date;
        Windows = windows;
    }

    public DateOnly Date { get; }

    /// <summary>The windows in effect for the date. Empty means closed.</summary>
    public IReadOnlyList<DayWindow> Windows { get; }

    public bool IsClosure => Windows.Count == 0;

    public static DateException Closure(DateOnly date) => new(date, []);

    public static DomainResult<DateException> Override(DateOnly date, IEnumerable<DayWindow> windows)
    {
        var ordered = windows.OrderBy(w => w.Start).ToArray();

        if (ordered.Length == 0)
        {
            return DomainResult<DateException>.Failure(
                FailureCodes.WindowInvalid,
                "An override exception requires at least one window; use a closure to close the date.");
        }

        for (var i = 1; i < ordered.Length; i++)
        {
            if (ordered[i].Start < ordered[i - 1].End)
            {
                return DomainResult<DateException>.Failure(
                    FailureCodes.WindowsOverlap,
                    $"Override windows for {date:yyyy-MM-dd} overlap.");
            }
        }

        return DomainResult<DateException>.Success(new DateException(date, ordered));
    }
}
