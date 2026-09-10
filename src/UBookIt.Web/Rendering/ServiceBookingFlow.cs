using UBookIt.Core;
using UBookIt.Core.Services;
using UBookIt.Core.Stores;

namespace UBookIt.Web.Rendering;

/// <summary>What the service flow decided to render. Exactly one member is non-null.</summary>
public sealed record ServiceFlowOutcome(ServiceFormModel? Form, ServiceUnavailableModel? Unavailable);

/// <summary>
/// Decides what the service booking flow shows for one request, reading the Core
/// service-booking and availability ports in-process — never the anonymous
/// delivery API over HTTP, the same rule the resource flow follows, for the same
/// reason: an in-process read cannot be told apart from the caller's own, and
/// routing a server render through the public API buys a network hop, an auth
/// surface and a second failure mode for nothing.
/// <para>
/// Written for <b>several</b> resources throughout. A single-role service
/// resolves to a collection of one, so a step that took the first of a collection
/// would pass every single-role test and fail only the multi-role case this flow
/// exists to serve.
/// </para>
/// </summary>
public sealed class ServiceBookingFlow(
    IServiceStore serviceStore,
    IServiceBookingService serviceBooking,
    SiteBookingSettings settings,
    TimeProvider timeProvider)
{
    public async Task<ServiceFlowOutcome> BuildAsync(
        Guid serviceId, BookingFlowInput input, CancellationToken cancellationToken = default)
    {
        var service = await serviceStore.GetAsync(serviceId, cancellationToken).ConfigureAwait(false);
        var zoneResolved = BookingForm.TryResolveZone(settings.TimeZoneId, out var zone);

        // The pools the booking path itself acts on. Read once and passed to
        // every question below, so the form, the refusal and placement cannot
        // disagree about which resources a service resolves to.
        var resolved = service is null
            ? null
            : await serviceBooking.ResolveCandidatesAsync(serviceId, cancellationToken).ConfigureAwait(false);

        var pools = resolved is { Succeeded: true } ? resolved.Value : null;

        // Whether this service can ever be fulfilled as configured, decided by a
        // pure function so it can be attacked without a host. No form is offered
        // when it cannot: rendering one that placement will always refuse would
        // invite someone to fill it in and lose their input to a failure that was
        // knowable before they started.
        if (ServiceUnavailableModel.IsUnavailable(service, pools, zoneResolved, out var unavailable))
        {
            return new ServiceFlowOutcome(null, unavailable);
        }

        var today = BookingForm.TodayIn(timeProvider.GetUtcNow(), zone);
        var failed = input.Failed;

        var selectedDate = BookingForm.NotBefore(failed?.Date ?? input.Date ?? today, today);

        // Who, before when. The choice joins this step rather than introducing a
        // page between the times and the contact details, and the start list below
        // is then filtered to it — "I want Jane, when can I have her?" is the
        // question the control exists to answer (design D2).
        //
        // A stale choice — deleted, no longer eligible, or its role no longer
        // selectable — resets to "any" here and the form says it has (design D11).
        // Falling back silently would be quiet substitution; refusing to render the
        // form would punish a visitor for a change they had no part in.
        var choice = ServiceBookingFormBuilder.ResourceChoiceState.Resolve(
            pools, failed?.ChosenResourceId ?? input.ChosenResourceId);

        var options = ServiceBookingFormBuilder.DurationOptions(pools);

        // Never null: an empty option set is one of the deterministic refusals
        // above, so by here the service offers at least one length.
        var duration = ServiceBookingFormBuilder.ResolveDisplayDurationMinutes(
            options, failed?.DurationMinutes ?? input.DurationMinutes)!.Value;

        // One query answers every length: the form filters these starts for the
        // chosen length and reads the longest available off the same result, so
        // an unavailable length can explain itself instead of rendering blank.
        //
        // Pinned to the visitor's choice where they made one, so the times offered
        // are the times that choice can actually be honoured rather than the
        // service's own — offering a start only somebody else could serve would be
        // a promise the pinned placement then refuses.
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
            today, ServiceBookingFormBuilder.HorizonDays(pools), settings.MaxQueryRangeDays);

        var windowDays = windowTo.DayNumber - windowFrom.DayNumber + 1;
        var selectedIsInWindow = selectedDate >= windowFrom && selectedDate <= windowTo;

        var windowResult = await serviceBooking
            .GetBookableStartsAsync(serviceId, windowFrom, windowTo, choice.Chosen, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<ServiceBookableStart> windowStarts =
            windowResult.Succeeded ? windowResult.Value : [];

        IReadOnlyList<ServiceBookableStart> dayStarts;

        if (selectedIsInWindow)
        {
            dayStarts = ServiceBookingFormBuilder.OnDate(windowStarts, zone, selectedDate);
        }
        else
        {
            var dayResult = await serviceBooking
                .GetBookableStartsAsync(
                    serviceId, selectedDate, selectedDate, choice.Chosen, cancellationToken)
                .ConfigureAwait(false);

            dayStarts = dayResult.Succeeded ? dayResult.Value : [];
        }

        return new ServiceFlowOutcome(
            ServiceBookingFormBuilder.Build(
                service, pools, selectedDate, today, windowStarts, dayStarts, duration, zone,
                PrivacyNoticeView.From(settings), windowDays, failed, input.FlowToken, choice),
            null);
    }
}
