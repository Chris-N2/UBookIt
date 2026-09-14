using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Core;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Services;
using UBookIt.Core.Stores;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Website.Controllers;

namespace UBookIt.Web.Rendering;

/// <summary>
/// Handles the default <em>service</em> booking form submission. Same-origin,
/// anti-forgery protected (the form is rendered with
/// <c>Html.BeginUmbracoForm</c>, which emits the token); places via the Core
/// service-booking port in-process — the anonymous delivery API is not used here.
/// Post-Redirect-Get both ways: success carries a confirmation, failure carries
/// the input and messages, both via TempData, and the flow redraws.
/// <para>
/// A sibling of <see cref="BookingSurfaceController"/> rather than a branch inside
/// it: the two differ by what is being booked and, in a follow-up, by the control
/// choosing who fulfils it, and a controller parameterised over both would be
/// written in the language of neither. What must not drift between them — the
/// markup, the error model, the length rules — is shared below the view instead.
/// </para>
/// </summary>
public sealed class ServiceBookingSurfaceController : SurfaceController
{
    private readonly IServiceStore _serviceStore;
    private readonly IResourceStore _resourceStore;
    private readonly IServiceBookingService _serviceBooking;
    private readonly SiteBookingSettings _settings;
    private readonly FrontendSettings _frontendSettings;

    public ServiceBookingSurfaceController(
        IUmbracoContextAccessor umbracoContextAccessor,
        IUmbracoDatabaseFactory databaseFactory,
        ServiceContext services,
        AppCaches appCaches,
        IProfilingLogger profilingLogger,
        IPublishedUrlProvider publishedUrlProvider,
        IServiceStore serviceStore,
        IResourceStore resourceStore,
        IServiceBookingService serviceBooking,
        SiteBookingSettings settings,
        FrontendSettings frontendSettings)
        : base(umbracoContextAccessor, databaseFactory, services, appCaches, profilingLogger, publishedUrlProvider)
    {
        _serviceStore = serviceStore;
        _resourceStore = resourceStore;
        _serviceBooking = serviceBooking;
        _settings = settings;
        _frontendSettings = frontendSettings;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(ServiceBookingSubmission form)
    {
        var service = await _serviceStore.GetAsync(form.ServiceId);
        if (service is null || !BookingForm.TryResolveZone(_settings.TimeZoneId, out var zone))
        {
            return await FailAsync(form, [new DomainFailure(FailureCodes.ServiceNotFound, "Service unavailable.")]);
        }

        var failures = new List<DomainFailure>();

        // A POST that omits the field binds DurationMinutes to 0, which Core can
        // only report as interval-invalid ("please choose a valid time") —
        // pointing the visitor at the time list when the length is what is
        // missing. Distinguishing an absent field from a submitted length is a
        // model-binding concern, not a domain rule, so it belongs here.
        //
        // It applies to a fixed-duration service too. The control is removed for
        // one, not the field: the delivery contract requires a length for a fixed
        // service precisely so a permitted one is never silently substituted, and
        // the same reasoning applies in-process (design D5).
        if (form.DurationMinutes <= 0)
        {
            failures.Add(new DomainFailure(
                FailureCodes.DurationTooShort,
                "A booking length is required.",
                nameof(ServiceBookingSubmission.DurationMinutes)));
        }

        // The selected time round-trips the exact UTC instant, so we never
        // re-parse a display string.
        if (!DateTimeOffset.TryParse(
                form.SelectedTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var startUtc))
        {
            failures.Add(new DomainFailure(FailureCodes.IntervalInvalid, "Please choose a time.", "SelectedTime"));
        }

        // Member key is always null — the default form never asserts identity.
        var booker = Booker.Create(memberKey: null, form.Name, form.Email, form.Phone);
        if (!booker.Succeeded)
        {
            failures.AddRange(booker.Failures);
        }

        if (failures.Count == 0)
        {
            var placed = await _serviceBooking.PlaceAsync(new ServiceBookingRequest
            {
                ServiceId = service.Id,
                Start = startUtc,

                // The visitor's choice, carried through to placement unchanged, so
                // the booking that is placed is the booking that was offered. A
                // pin, never a preference: when it cannot be honoured Core refuses
                // with `pinned-resource-unavailable` rather than booking somebody
                // else, and the page below says so by name.
                //
                // Submitted only where a choice was offered: the field is rendered
                // inside the who control, which exists only for a service with a
                // visitor-selectable role. Passed through rather than re-validated
                // here — a resource that cannot fulfil the service is
                // `resource-not-eligible` from Core, which is a refusal, where
                // dropping it here would absorb the visitor's choice into a
                // booking they did not ask for.
                PinnedResourceId = form.PinnedResourceId,

                // The submitted length goes to Core exactly as submitted. It must
                // never be silently substituted: placing a booking of a length the
                // visitor did not choose is worse than refusing the submission.
                Duration = TimeSpan.FromMinutes(form.DurationMinutes),
                Booker = booker.Value,
            });

            if (placed.Succeeded)
            {
                Stash(
                    BookingKeys.ServiceConfirmation,
                    await BuildConfirmationAsync(placed.Value, service.Name, zone));

                return SeeOther(BackToFlow(form));
            }

            failures.AddRange(placed.Failures);
        }

        return await FailAsync(form, failures);
    }

    /// <summary>
    /// The display name of the resource the visitor chose, so a refused pin can be
    /// reported by name — or null when they chose nobody or it could not be read.
    /// <para>
    /// Looking the name up is this controller's job, because it needs the store.
    /// <b>Deciding what the visitor is told is not</b>, and lives in
    /// <see cref="BookingMessages.ForFailures(IEnumerable{DomainFailure}, string?)"/>
    /// where it can be attacked without an Umbraco host. QA proved why: with the
    /// decision here, mutating it to always use the generic wording left the whole
    /// suite green.
    /// </para>
    /// <para>
    /// Read only when a pin was actually refused, so an ordinary failed submission
    /// costs no extra store read.
    /// </para>
    /// </summary>
    private async Task<string?> ChosenResourceNameAsync(
        ServiceBookingSubmission form, IReadOnlyList<DomainFailure> failures)
    {
        if (form.PinnedResourceId is not { } pinned
            || !failures.Any(f => f.Code == FailureCodes.PinnedResourceUnavailable))
        {
            return null;
        }

        return (await _resourceStore.GetAsync(pinned))?.DisplayName;
    }

    private async Task<IActionResult> FailAsync(
        ServiceBookingSubmission form, IReadOnlyList<DomainFailure> failures)
    {
        // Mapped from the stable codes and never from the domain's message text.
        // That is what keeps the pool-sufficiency diagnostic out of the visitor's
        // page: a `service-unavailable` failure carries a message naming roles,
        // counts and capabilities for the backoffice, and piping it through would
        // read plausibly while telling a visitor how many therapists the business
        // employs.
        Stash(BookingKeys.ServiceFailedSubmission, new FailedSubmission
        {
            Date = form.Date,
            DurationMinutes = form.DurationMinutes,
            SelectedTimeIso = form.SelectedTime,

            // Carried back so the redraw shows the visitor's own choice. Dropping
            // it would quietly turn their next submission into a booking for
            // anyone, on a page that had said otherwise.
            ChosenResourceId = form.PinnedResourceId,
            Name = form.Name,
            Email = form.Email,
            Phone = form.Phone,
            Errors = [.. BookingMessages.ForFailures(
                failures, await ChosenResourceNameAsync(form, failures))],
        });

        return SeeOther(BackToFlow(form));
    }

    /// <summary>
    /// The Post-Redirect-Get target: the current page, carrying the flow's
    /// subject, date and length when the flow was entered through the
    /// dispatcher's query string, so the redirect lands back on this service at
    /// the step the visitor was on rather than at the catalogue or at a URL
    /// disagreeing with the page it draws. Every value is re-serialised from
    /// parsed input rather than echoed. Contact details are never among the
    /// parameters — they arrived in the POST body and stay there (design D3).
    /// </summary>
    /// <remarks>
    /// The subject must also <em>agree</em> with what was booked — see the twin
    /// on <see cref="BookingSurfaceController"/>. A submitted subject naming a
    /// different thing redirects into the other flow, whose TempData key differs,
    /// losing the confirmation for a booking that was actually placed.
    /// </remarks>
    private IActionResult BackToFlow(ServiceBookingSubmission form)
        // See BookingSurfaceController.BackToFlow: the decision lives in
        // AfterSubmission, unit-tested; this adds only the host's two facts.
        => BookingFlowLink.AfterSubmission(
                BookingSubject.Agreeing(form.Subject, BookingSubject.Service(form.ServiceId)),
                form.Date,
                form.DurationMinutes,
                form.PinnedResourceId,
                PreservedQuery.Compute(Request.Query, _frontendSettings.PreservedQueryParameters))
            is { } query
            ? RedirectToCurrentUmbracoPage(query)
            : RedirectToCurrentUmbracoPage();

    /// <summary>
    /// Post-Redirect-Get with a literal 303 See Other (the spec's required code).
    /// The Umbraco redirect result resolves the current page URL and sets the
    /// Location header and a 302; the response has not started, so we flip the
    /// status to 303 (browsers GET the target either way — this makes the code
    /// match the semantics).
    /// </summary>
    private static IActionResult SeeOther(IActionResult redirect) => new SeeOtherResult(redirect);

    private sealed class SeeOtherResult(IActionResult inner) : IActionResult
    {
        public async Task ExecuteResultAsync(ActionContext context)
        {
            await inner.ExecuteResultAsync(context);
            if (context.HttpContext.Response.StatusCode == StatusCodes.Status302Found)
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status303SeeOther;
            }
        }
    }

    /// <summary>
    /// Reads the display name of every resource the booking claims, then hands
    /// the whole claim list to the builder. Looking the names up is this
    /// controller's job; deciding what the confirmation reports is not, and lives
    /// in <see cref="ServiceBookingFormBuilder.BuildConfirmation"/> where it can
    /// be attacked without a host.
    /// </summary>
    private async Task<ServiceConfirmationModel> BuildConfirmationAsync(
        Booking booking, string serviceName, TimeZoneInfo zone)
    {
        var names = new Dictionary<Guid, string>();

        foreach (var claim in booking.Claims)
        {
            var resource = await _resourceStore.GetAsync(claim.ResourceId);
            if (resource is not null)
            {
                names[claim.ResourceId] = resource.DisplayName;
            }
        }

        return ServiceBookingFormBuilder.BuildConfirmation(booking, serviceName, names, zone);
    }

    private void Stash<T>(string key, T value) => TempData.Stash(key, value);
}

/// <summary>Bound form fields from the service booking submission.</summary>
public sealed class ServiceBookingSubmission
{
    public Guid ServiceId { get; set; }

    /// <summary>
    /// The flow subject token to restore on the Post-Redirect-Get, present only
    /// when the flow was entered through the dispatcher's query string. Parsed
    /// strictly and re-serialised; never echoed.
    /// </summary>
    public string? Subject { get; set; }

    public DateOnly Date { get; set; }

    /// <summary>The chosen booking length in whole minutes; re-validated server-side.</summary>
    public int DurationMinutes { get; set; }

    /// <summary>The chosen slot's exact UTC instant, round-trip ("O") formatted.</summary>
    public string? SelectedTime { get; set; }

    /// <summary>
    /// The resource the visitor chose to fulfil the service's visitor-selectable
    /// role, where one was offered. Null is "any", which is what every service
    /// offering no choice submits — and what the field's absence binds to.
    /// <para>
    /// A pin rather than a preference: no other resource is substituted for it.
    /// It is passed to Core unchanged; a resource that cannot fulfil the service
    /// is refused there, never absorbed here.
    /// </para>
    /// </summary>
    public Guid? PinnedResourceId { get; set; }

    public string? Name { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }
}
