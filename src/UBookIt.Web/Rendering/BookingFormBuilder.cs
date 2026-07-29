using UBookIt.Core.Availability;
using UBookIt.Core.Resources;

namespace UBookIt.Web.Rendering;

/// <summary>
/// Assembles the booking form view model from Core data (host-independent, so
/// it is unit-testable without an Umbraco host). Times are shown as site-zone
/// wall-clock; each option's value is the exact UTC instant (round-trip "O"
/// format) so the POST re-selects the precise slot without re-parsing display
/// text. v1 books a fixed duration — the resource's minimum duration.
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

    /// <summary>The booking duration for v1: the resource's minimum duration.</summary>
    public static TimeSpan BookingDuration(Resource resource) => resource.Availability.Constraints.MinDuration;

    public static BookingFormModel Build(
        Resource resource,
        DateOnly selectedDate,
        DateOnly today,
        IReadOnlyList<Slot> slots,
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
            DurationMinutes = (int)constraints.MinDuration.TotalMinutes,
            Times = ToOptions(slots, zone),
            SelectedTimeIso = failed?.SelectedTimeIso,
            Name = failed?.Name,
            Email = failed?.Email,
            Phone = failed?.Phone,
            Errors = failed?.Errors ?? [],
        };
    }
}
