using UBookIt.Core.Availability;
using UBookIt.Core.Resources;

namespace UBookIt.Web.Rendering;

/// <summary>
/// Assembles the <em>resource</em> booking form view model from Core data
/// (host-independent, so it is unit-testable without an Umbraco host). Times are
/// shown as site-zone wall-clock; each option's value is the exact UTC instant
/// (round-trip "O" format) so the POST re-selects the precise slot without
/// re-parsing display text. The booking length is the visitor's choice from the
/// lengths the resource permits, defaulting to its minimum duration.
/// <para>
/// What is generic to <em>any</em> booking form now lives in
/// <see cref="BookingForm"/> and is shared with the service flow; the members
/// below that merely forward to it are kept because they are this type's
/// published surface and because the resource flow's tests are the guard that
/// the extraction changed no behaviour — a guard worth nothing if the extraction
/// is allowed to move the assertions.
/// </para>
/// </summary>
public static class BookingFormBuilder
{
    public static BookingTimeOption ToOption(DateTimeOffset startUtc, TimeZoneInfo zone)
        => BookingForm.ToOption(startUtc, zone);

    public static IReadOnlyList<BookingTimeOption> ToOptions(IEnumerable<Slot> slots, TimeZoneInfo zone)
        => BookingForm.ToOptions(slots.Select(slot => slot.StartUtc), zone);

    /// <summary>Today in the site zone — the earliest selectable date and the render default.</summary>
    public static DateOnly TodayIn(DateTimeOffset nowUtc, TimeZoneInfo zone)
        => BookingForm.TodayIn(nowUtc, zone);

    /// <summary>Resolves the site zone; false (with UTC) for an unknown/invalid id.</summary>
    public static bool TryResolveZone(string timeZoneId, out TimeZoneInfo zone)
        => BookingForm.TryResolveZone(timeZoneId, out zone);

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

        return BookingForm.LengthGrid(
            (int)constraints.MinDuration.TotalMinutes,
            (int)constraints.MaxDuration.TotalMinutes,
            (int)constraints.Granularity.TotalMinutes);
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
        PrivacyNoticeView privacyNotice,
        FailedSubmission? failed = null,
        string? flowToken = null)
    {
        var constraints = resource.Availability.Constraints;

        return new BookingFormModel
        {
            PrivacyNotice = privacyNotice,
            FlowToken = flowToken,
            ResourceId = resource.Id,
            ResourceName = resource.DisplayName,
            SelectedDate = selectedDate,
            MinDate = today,
            // Saturating: HorizonDays is only validated as positive, so a large
            // one would otherwise throw while rendering the form.
            MaxDate = CalendarBounds.AddDaysSaturating(today, constraints.HorizonDays),
            DurationMinutes = (int)duration.TotalMinutes,
            DurationOptions = DurationOptions(resource),
            LongestAvailableMinutes = LongestAvailableMinutes(starts),
            Times = BookingForm.ToOptions(starts.Where(s => s.Admits(duration)).Select(s => s.StartUtc), zone),
            SelectedTimeIso = failed?.SelectedTimeIso,
            Name = failed?.Name,
            Email = failed?.Email,
            Phone = failed?.Phone,
            Errors = failed?.Errors ?? [],
        };
    }
}
