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

        var options = ServiceBookingFormBuilder.DurationOptions(pools);

        // Never null: an empty option set is one of the deterministic refusals
        // above, so by here the service offers at least one length.
        var duration = ServiceBookingFormBuilder.ResolveDisplayDurationMinutes(
            options, failed?.DurationMinutes ?? input.DurationMinutes)!.Value;

        // One query answers every length: the form filters these starts for the
        // chosen length and reads the longest available off the same result, so
        // an unavailable length can explain itself instead of rendering blank.
        var startsResult = await serviceBooking
            .GetBookableStartsAsync(serviceId, selectedDate, selectedDate, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<ServiceBookableStart> starts = startsResult.Succeeded ? startsResult.Value : [];

        return new ServiceFlowOutcome(
            ServiceBookingFormBuilder.Build(
                service, pools, selectedDate, today, starts, duration, zone, failed, input.FlowToken),
            null);
    }
}
