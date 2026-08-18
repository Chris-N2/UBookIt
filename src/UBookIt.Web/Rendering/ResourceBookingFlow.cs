using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Stores;

namespace UBookIt.Web.Rendering;

/// <summary>
/// The host-supplied inputs to a booking step: what the URL said, and what a
/// failed submission carried back. Everything else a flow needs it reads from
/// Core itself.
/// </summary>
public sealed record BookingFlowInput
{
    public DateOnly? Date { get; init; }

    public int? DurationMinutes { get; init; }

    public FailedSubmission? Failed { get; init; }

    /// <summary>
    /// The subject token to carry through the step, or null when a site author
    /// named the subject on the component and there is no flow state in the URL.
    /// </summary>
    public string? FlowToken { get; init; }
}

/// <summary>What the resource flow decided to render. Exactly one member is non-null.</summary>
public sealed record ResourceFlowOutcome(BookingFormModel? Form, BookingUnavailableModel? Unavailable);

/// <summary>
/// Decides what the single-resource booking flow shows for one request, reading
/// Core in-process (never the anonymous delivery API over HTTP).
/// <para>
/// Extracted from the ViewComponent so the dispatcher renders the very same flow
/// rather than a second one written to match, and so the decision is testable
/// without an Umbraco host. The ViewComponent keeps only what genuinely needs the
/// host: TempData and the query string.
/// </para>
/// </summary>
public sealed class ResourceBookingFlow(
    IResourceStore resourceStore,
    IAvailabilityQueryService availability,
    SiteBookingSettings settings,
    TimeProvider timeProvider)
{
    public async Task<ResourceFlowOutcome> BuildAsync(
        Guid resourceId, BookingFlowInput input, CancellationToken cancellationToken = default)
    {
        var resource = await resourceStore.GetAsync(resourceId, cancellationToken).ConfigureAwait(false);
        var zoneResolved = BookingForm.TryResolveZone(settings.TimeZoneId, out var zone);

        // Which of the two unavailable answers applies, if either — decided by a
        // pure function so it can be tested without a host. The distinction is
        // the point of it: "not offered on its own" is permanent and "no times
        // available" is temporary, and rendering them alike invites a visitor
        // back tomorrow when the answer will be the same, while telling the site
        // owner their opening hours are wrong when they are not.
        //
        // No form is offered for either. Rendering one that placement will always
        // refuse would invite someone to fill it in and lose their input to a
        // failure that was knowable before they started.
        if (BookingUnavailableModel.IsUnavailable(resource, zoneResolved, out var unavailable))
        {
            return new ResourceFlowOutcome(null, unavailable);
        }

        var today = BookingForm.TodayIn(timeProvider.GetUtcNow(), zone);
        var failed = input.Failed;

        var selectedDate = BookingForm.NotBefore(failed?.Date ?? input.Date ?? today, today);

        var duration = BookingFormBuilder.ResolveDisplayDuration(
            resource, failed?.DurationMinutes ?? input.DurationMinutes);

        // One query answers every length: the form filters these starts for the
        // chosen length and reads the longest available off the same result, so
        // an unavailable length can explain itself instead of rendering blank.
        var startsResult = await availability
            .GetBookableStartsAsync(resourceId, selectedDate, selectedDate, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<BookableStart> starts = startsResult.Succeeded ? startsResult.Value : [];

        return new ResourceFlowOutcome(
            BookingFormBuilder.Build(
                resource, selectedDate, today, starts, duration, zone, failed, input.FlowToken),
            null);
    }
}
