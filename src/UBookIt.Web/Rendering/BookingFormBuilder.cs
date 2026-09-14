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

    /// <summary>
    /// The starts falling on one date, in the site zone.
    /// </summary>
    /// <remarks>
    /// <b>The selected day's times are a FILTER of the window, never a second read.</b> Two reads
    /// would let a booking land between them and produce a page whose date list says a day is
    /// free while its times say it is not — an ordinary outcome rather than a bug. One reading
    /// cannot contradict itself, and this is where that property is actually delivered.
    /// <para>
    /// Compared in the SITE ZONE. A start at 23:30 UTC belongs to the next day in
    /// <c>Europe/London</c> in summer, and filtering on the UTC date would show it under the
    /// wrong heading — for every site that is not itself UTC.
    /// </para>
    /// </remarks>
    internal static List<BookableStart> OnDate(
        IReadOnlyList<BookableStart> windowStarts, TimeZoneInfo zone, DateOnly date)
        => [.. windowStarts.Where(start =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(start.StartUtc, zone).DateTime) == date)];

    public static BookingFormModel Build(
        Resource resource,
        DateOnly selectedDate,
        DateOnly today,
        IReadOnlyList<BookableStart> windowStarts,
        IReadOnlyList<BookableStart> dayStarts,
        TimeSpan duration,
        TimeZoneInfo zone,
        PrivacyNoticeView privacyNotice,
        int windowDays,
        FailedSubmission? failed = null,
        string? flowToken = null,
        IReadOnlyList<PreservedQueryPair>? preservedQuery = null)
    {
        var constraints = resource.Availability.Constraints;

        // The day's starts are the WINDOW filtered, whenever the selected date is in the
        // window — the caller does that filtering so the "one read" property is visible where the
        // reads happen. Where the selected date is outside the window they come from a second,
        // DISJOINT read; see the flows for why that is safe and why one read cannot serve both.
        var starts = dayStarts;

        var dates = AvailableDateProjection.Dates(
            windowStarts.Select(start => (start.StartUtc, start.Admits(duration))),
            zone,
            selectedDate);

        return new BookingFormModel
        {
            AvailableDates = dates,
            SelectedDateIsListed = dates.Any(date => date.IsSelected),
            WindowDays = windowDays,
            LongestAvailableInWindowMinutes = LongestAvailableMinutes(windowStarts),
            PrivacyNotice = privacyNotice,
            FlowToken = flowToken,
            PreservedQuery = preservedQuery ?? [],
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
