using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Core;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
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
/// Handles the default booking form submission. Same-origin, anti-forgery
/// protected (the form is rendered with <c>Html.BeginUmbracoForm</c>, which
/// emits the token); places via the Core booking service in-process — the
/// anonymous delivery API is not used here. Post-Redirect-Get both ways
/// (design D3): success carries a confirmation, failure carries the input and
/// messages, both via TempData, and the ViewComponent redraws.
/// </summary>
public sealed class BookingSurfaceController : SurfaceController
{
    private readonly IResourceStore _resourceStore;
    private readonly IBookingService _bookingService;
    private readonly SiteBookingSettings _settings;

    public BookingSurfaceController(
        IUmbracoContextAccessor umbracoContextAccessor,
        IUmbracoDatabaseFactory databaseFactory,
        ServiceContext services,
        AppCaches appCaches,
        IProfilingLogger profilingLogger,
        IPublishedUrlProvider publishedUrlProvider,
        IResourceStore resourceStore,
        IBookingService bookingService,
        SiteBookingSettings settings)
        : base(umbracoContextAccessor, databaseFactory, services, appCaches, profilingLogger, publishedUrlProvider)
    {
        _resourceStore = resourceStore;
        _bookingService = bookingService;
        _settings = settings;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(BookingSubmission form)
    {
        var resource = await _resourceStore.GetAsync(form.ResourceId);
        if (resource is null || !BookingFormBuilder.TryResolveZone(_settings.TimeZoneId, out var zone))
        {
            return Fail(form, [new DomainFailure(FailureCodes.ResourceNotFound, "Resource unavailable.")]);
        }

        var failures = new List<DomainFailure>();

        // A POST that omits the field binds DurationMinutes to 0, which Core
        // can only report as interval-invalid ("please choose a valid time") —
        // pointing the visitor at the time list when the length is what is
        // missing. Distinguishing an absent field from a submitted length is a
        // model-binding concern, not a domain rule, so it belongs here; the
        // permitted-length rule itself stays solely in Core.
        if (form.DurationMinutes <= 0)
        {
            failures.Add(new DomainFailure(
                FailureCodes.DurationTooShort,
                "A booking length is required.",
                nameof(BookingSubmission.DurationMinutes)));
        }

        // The selected time round-trips the exact UTC instant (design D6), so we
        // never re-parse a display string.
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
            // The submitted length goes to Core exactly as submitted. It must
            // never be silently substituted: placing a booking of a length the
            // visitor did not choose is worse than refusing the submission, and
            // the default-frontend spec requires a rejection here. Core already
            // validates it against the resource (granularity, duration bounds)
            // and returns stable codes, so there is no second rule to keep in
            // step with the option list.
            var placed = await _bookingService.PlaceAsync(new BookingRequest
            {
                ResourceId = resource.Id,
                Start = startUtc,
                Duration = TimeSpan.FromMinutes(form.DurationMinutes),
                Booker = booker.Value,
            });

            if (placed.Succeeded)
            {
                Stash(BookingKeys.Confirmation, BuildConfirmation(placed.Value, resource.DisplayName, zone));
                return SeeOther(BackToFlow(form));
            }

            failures.AddRange(placed.Failures);
        }

        return Fail(form, failures);
    }

    private IActionResult Fail(BookingSubmission form, IReadOnlyList<DomainFailure> failures)
    {
        Stash(BookingKeys.FailedSubmission, new FailedSubmission
        {
            Date = form.Date,
            DurationMinutes = form.DurationMinutes,
            SelectedTimeIso = form.SelectedTime,
            Name = form.Name,
            Email = form.Email,
            Phone = form.Phone,
            Errors = [.. BookingMessages.ForFailures(failures)],
        });

        return SeeOther(BackToFlow(form));
    }

    /// <summary>
    /// The Post-Redirect-Get target: the current page, carrying the flow's
    /// subject when the flow was entered through the dispatcher's query string.
    /// <para>
    /// Without it the redirect would drop the subject and the dispatcher would
    /// land the visitor back at the catalogue, losing the confirmation with it.
    /// <b>Only</b> when a subject was submitted: a form rendered by the
    /// <c>Booking</c> component names the resource on the component instead, and
    /// its redirect is byte-for-byte what it was before the service flow existed.
    /// </para>
    /// <para>
    /// Every value is re-serialised from parsed input rather than echoed, so
    /// nothing a caller typed reaches the Location header. Contact details are
    /// never among the parameters — they arrived in the POST body and stay there
    /// (design D3).
    /// </para>
    /// <para>
    /// The date and length travel with the subject. Carrying the subject alone
    /// would redraw the step at a URL that disagreed with the page — the visitor
    /// would see the date they submitted while the address bar named another —
    /// which is precisely the inconsistency the URL-state rule exists to prevent.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The subject must also <em>agree</em> with what was booked. It is a POST
    /// field, so a hand-made submission can name a service while booking a
    /// resource — placing the booking and then redirecting into the other flow,
    /// which reads a different TempData key and drops the visitor's confirmation
    /// on the floor. Only self-inflicted, but the check is one comparison.
    /// </remarks>
    private IActionResult BackToFlow(BookingSubmission form)
        => BookingSubject.Agreeing(form.Subject, BookingSubject.Resource(form.ResourceId)) is { } subject
            ? RedirectToCurrentUmbracoPage(
                BookingFlowLink.For(subject, form.Date, form.DurationMinutes))
            : RedirectToCurrentUmbracoPage();

    /// <summary>
    /// Post-Redirect-Get with a literal 303 See Other (the spec's required
    /// code). The Umbraco redirect result resolves the current page URL and sets
    /// the Location header and a 302; the response has not started, so we flip
    /// the status to 303 (browsers GET the target either way — this makes the
    /// code match the semantics).
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
    /// Projects a placed booking into what the confirmation page shows.
    /// </summary>
    /// <remarks>
    /// <c>internal</c> rather than <c>private</c> so it can be asserted directly. The
    /// surface controllers have no behavioural coverage — they need Umbraco's
    /// <c>SurfaceController</c> plumbing — so this method was the only reference-carrying
    /// projection in the package with nothing watching it at all, and substituting a constant
    /// for the reference passed the entire suite.
    /// </remarks>
    internal static BookingConfirmationModel BuildConfirmation(Booking booking, string resourceName, TimeZoneInfo zone)
    {
        var start = TimeZoneInfo.ConvertTime(booking.Interval.StartUtc, zone);
        var end = TimeZoneInfo.ConvertTime(booking.Interval.EndUtc, zone);
        var contact = PlacedBooking.Contact(booking);

        return new BookingConfirmationModel
        {
            BookingId = booking.Id,
            Reference = booking.Reference.Display,
            // From the stored booking's status, never from the setting: the page describes
            // the booking it announces, exactly as the email wording does.
            IsPending = booking.Status == BookingStatus.Requested,
            ResourceName = resourceName,
            LocalStart = start.ToString("dddd d MMMM yyyy, HH:mm", CultureInfo.InvariantCulture),
            LocalEnd = end.ToString("HH:mm", CultureInfo.InvariantCulture),
            BookerName = contact.Name,
            BookerEmail = contact.Email,
            BookerPhone = contact.Phone,
        };
    }

    private void Stash<T>(string key, T value) => TempData.Stash(key, value);
}

/// <summary>Bound form fields from the booking submission.</summary>
public sealed class BookingSubmission
{
    public Guid ResourceId { get; set; }

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

    public string? Name { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }
}
