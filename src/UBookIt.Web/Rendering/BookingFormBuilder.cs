using UBookIt.Core.Availability;
using UBookIt.Core.Resources;

namespace UBookIt.Web.Rendering;

/// <summary>
/// Assembles the booking form view model from Core data (host-independent, so
/// it is unit-testable without an Umbraco host). Times are shown as site-zone
/// wall-clock; each option's value is the exact UTC instant (round-trip "O"
/// format) so the POST re-selects the precise slot without re-parsing display
/// text. The booking length is the visitor's choice from the lengths the
/// resource permits, defaulting to its minimum duration.
/// </summary>
public static class BookingFormBuilder
{
    public static BookingTimeOption ToOption(DateTimeOffset startUtc, TimeZoneInfo zone)
        => new(startUtc.ToString("O"), TimeZoneInfo.ConvertTime(startUtc, zone).ToString("HH:mm"));

    public static IReadOnlyList<BookingTimeOption> ToOptions(IEnumerable<Slot> slots, TimeZoneInfo zone)
        => slots.Select(slot => ToOption(slot.StartUtc, zone)).ToList();

    /// <summary>Today in the site zone — the earliest selectable date and the render default.</summary>
    public static DateOnly TodayIn(DateTimeOffset nowUtc, TimeZoneInfo zone)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, zone).DateTime);

    /// <summary>Resolves the site zone; false (with UTC) for an unknown/invalid id.</summary>
    public static bool TryResolveZone(string timeZoneId, out TimeZoneInfo zone)
    {
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            zone = TimeZoneInfo.Utc;
            return false;
        }
    }

    /// <summary>
    /// The default booking length: the resource's minimum duration. A visitor
    /// who never touches the length control books exactly what they booked
    /// before the control existed.
    /// </summary>
    public static TimeSpan DefaultDuration(Resource resource) => resource.Availability.Constraints.MinDuration;

    /// <summary>
    /// Every length the resource permits, in whole minutes ascending: the
    /// granularity multiples from its minimum to its maximum duration. Both
    /// bounds are guaranteed multiples of the granularity by
    /// <c>BookingConstraints</c>, so the sequence lands exactly on the maximum.
    /// </summary>
    public static IReadOnlyList<int> DurationOptions(Resource resource)
    {
        var constraints = resource.Availability.Constraints;
        var step = (int)constraints.Granularity.TotalMinutes;
        var max = (int)constraints.MaxDuration.TotalMinutes;

        // A sub-minute granularity truncates to a zero step, which would loop
        // forever. BookingConstraints permits it (it only requires the bounds
        // to be exact multiples), and this is a public method, so guard rather
        // than assume the caller's constraints came from the minute-based
        // management API.
        if (step <= 0)
        {
            return [];
        }

        var options = new List<int>();
        for (var minutes = (int)constraints.MinDuration.TotalMinutes; minutes <= max; minutes += step)
        {
            options.Add(minutes);
        }

        return options;
    }

    /// <summary>
    /// The length to <em>render</em>: the requested one when the resource
    /// permits it, otherwise the default. Defaulting is correct here — an
    /// absent or hand-edited query parameter should draw a usable form rather
    /// than an error page.
    /// <para>
    /// This is deliberately NOT used when placing a booking. Substituting a
    /// length on the write path would confirm a booking the visitor never
    /// chose; submissions pass their length to Core unchanged and are rejected
    /// if it is not permitted (default-frontend spec, "Visitor-chosen booking
    /// length").
    /// </para>
    /// </summary>
    public static TimeSpan ResolveDisplayDuration(Resource resource, int? requestedMinutes)
    {
        if (requestedMinutes is not { } minutes)
        {
            return DefaultDuration(resource);
        }

        return DurationOptions(resource).Contains(minutes)
            ? TimeSpan.FromMinutes(minutes)
            : DefaultDuration(resource);
    }

    /// <summary>
    /// The longest length bookable anywhere among these starts, or null when
    /// there are none. Composed here rather than in Core so no presentation
    /// concern reaches the domain (design D9).
    /// </summary>
    public static int? LongestAvailableMinutes(IReadOnlyList<BookableStart> starts)
        => starts.Count == 0 ? null : (int)starts.Max(s => s.MaxDuration).TotalMinutes;

    public static BookingFormModel Build(
        Resource resource,
        DateOnly selectedDate,
        DateOnly today,
        IReadOnlyList<BookableStart> starts,
        TimeSpan duration,
        TimeZoneInfo zone,
        FailedSubmission? failed = null)
    {
        var constraints = resource.Availability.Constraints;

        return new BookingFormModel
        {
            ResourceId = resource.Id,
            ResourceName = resource.DisplayName,
            SelectedDate = selectedDate,
            MinDate = today,
            MaxDate = today.AddDays(constraints.HorizonDays),
            DurationMinutes = (int)duration.TotalMinutes,
            DurationOptions = DurationOptions(resource),
            LongestAvailableMinutes = LongestAvailableMinutes(starts),
            Times = ToOptions(starts.Where(s => s.Admits(duration)).Select(s => new Slot(s.StartUtc, duration)), zone),
            SelectedTimeIso = failed?.SelectedTimeIso,
            Name = failed?.Name,
            Email = failed?.Email,
            Phone = failed?.Phone,
            Errors = failed?.Errors ?? [],
        };
    }
}
