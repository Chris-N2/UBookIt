using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Stores;
using Umbraco.Cms.Core.Mail;

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
    /// The resource the URL asked for, where a service offers a choice of who
    /// fulfils it. Null when none was named — and ignored entirely by the resource
    /// flow, which books the resource the visitor already chose.
    /// </summary>
    public Guid? ChosenResourceId { get; init; }

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
    TimeProvider timeProvider,
    IEmailSender emailSender)
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
        // THE WINDOW, derived rather than assumed. `MaxQueryRangeDays` is a guardrail the
        // availability read ENFORCES: a span wider than it is refused outright.
        //
        // TWO READS WHEN — AND ONLY WHEN — THE SELECTED DATE LIES OUTSIDE THE WINDOW, and the
        // reason it is safe is that they cannot overlap.
        //
        // This first widened one read to span both, which was a regression QA measured: a
        // guardrail of 31 cannot contain a 30-day window AND a date 89 days out, so the read was
        // refused and a visitor typing any date more than 30 days ahead got "no times available"
        // for an empty diary — on a stock install, using the very field that exists to reach
        // those dates. A single contiguous read simply cannot cover both; something had to give.
        //
        // D1 forbids two reads because "the day's times would come from one query and the list
        // from another, so a booking landing between them could produce a page whose list says
        // Tuesday is free and whose times say it is not". That reasoning is about ONE DAY
        // appearing in both. Here the ranges are disjoint by construction — the second read is
        // for a date the window does not contain — so no day is in both and there is nothing
        // for them to disagree about. Inside the window, where the risk is real, it stays one
        // read exactly as D1 requires.
        var (windowFrom, windowTo) = AvailableDateWindow.Compute(
            today, resource.Availability.Constraints.HorizonDays, settings.MaxQueryRangeDays);

        var windowDays = windowTo.DayNumber - windowFrom.DayNumber + 1;
        var selectedIsInWindow = selectedDate >= windowFrom && selectedDate <= windowTo;

        var windowResult = await availability
            .GetBookableStartsAsync(resourceId, windowFrom, windowTo, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<BookableStart> windowStarts = windowResult.Succeeded ? windowResult.Value : [];

        IReadOnlyList<BookableStart> dayStarts;

        if (selectedIsInWindow)
        {
            dayStarts = BookingFormBuilder.OnDate(windowStarts, zone, selectedDate);
        }
        else
        {
            var dayResult = await availability
                .GetBookableStartsAsync(resourceId, selectedDate, selectedDate, cancellationToken)
                .ConfigureAwait(false);

            dayStarts = dayResult.Succeeded ? dayResult.Value : [];
        }

        return new ResourceFlowOutcome(
            BookingFormBuilder.Build(
                resource, selectedDate, today, windowStarts, dayStarts, duration, zone,
                PrivacyNoticeView.From(settings, HostMailAvailability.CanSend(emailSender)), windowDays, failed, input.FlowToken),
            null);
    }
}
