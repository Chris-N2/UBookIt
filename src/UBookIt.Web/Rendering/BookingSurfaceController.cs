using System.Globalization;
using System.Text.Json;
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
            var placed = await _bookingService.PlaceAsync(new BookingRequest
            {
                ResourceId = resource.Id,
                Start = startUtc,
                Duration = BookingFormBuilder.BookingDuration(resource),
                Booker = booker.Value,
            });

            if (placed.Succeeded)
            {
                Stash(BookingKeys.Confirmation, BuildConfirmation(placed.Value, resource.DisplayName, zone));
                return SeeOther(RedirectToCurrentUmbracoPage());
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
            SelectedTimeIso = form.SelectedTime,
            Name = form.Name,
            Email = form.Email,
            Phone = form.Phone,
            Errors = [.. BookingMessages.ForFailures(failures)],
        });

        return SeeOther(RedirectToCurrentUmbracoPage());
    }

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

    private static BookingConfirmationModel BuildConfirmation(Booking booking, string resourceName, TimeZoneInfo zone)
    {
        var start = TimeZoneInfo.ConvertTime(booking.Interval.StartUtc, zone);
        var end = TimeZoneInfo.ConvertTime(booking.Interval.EndUtc, zone);

        return new BookingConfirmationModel
        {
            BookingId = booking.Id,
            ResourceName = resourceName,
            LocalStart = start.ToString("dddd d MMMM yyyy, HH:mm", CultureInfo.InvariantCulture),
            LocalEnd = end.ToString("HH:mm", CultureInfo.InvariantCulture),
            BookerName = booking.Booker.Name,
            BookerEmail = booking.Booker.Email,
            BookerPhone = booking.Booker.Phone,
        };
    }

    private void Stash<T>(string key, T value) => TempData[key] = JsonSerializer.Serialize(value);
}

/// <summary>Bound form fields from the booking submission.</summary>
public sealed class BookingSubmission
{
    public Guid ResourceId { get; set; }

    public DateOnly Date { get; set; }

    /// <summary>The chosen slot's exact UTC instant, round-trip ("O") formatted.</summary>
    public string? SelectedTime { get; set; }

    public string? Name { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }
}
