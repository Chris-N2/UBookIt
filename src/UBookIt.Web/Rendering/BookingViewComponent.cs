using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Stores;

namespace UBookIt.Web.Rendering;

/// <summary>
/// Renders the default booking flow for one resource (invoked as
/// <c>@await Component.InvokeAsync("Booking", new { resourceId })</c>). Reads
/// availability from Core in-process (never the delivery API). After a PRG
/// redirect it renders either the confirmation (on success) or the redrawn
/// form with preserved input and errors (on failure), both carried in TempData.
/// </summary>
public sealed class BookingViewComponent(
    IResourceStore resourceStore,
    IAvailabilityQueryService availability,
    SiteBookingSettings settings,
    TimeProvider timeProvider) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(Guid resourceId)
    {
        // A completed booking (PRG success) takes precedence over the form.
        if (ReadTempData<BookingConfirmationModel>(BookingKeys.Confirmation) is { } confirmation)
        {
            return View("Confirmation", confirmation);
        }

        var resource = await resourceStore.GetAsync(resourceId);
        if (resource is null || !BookingFormBuilder.TryResolveZone(settings.TimeZoneId, out var zone))
        {
            return View("Unavailable");
        }

        var today = BookingFormBuilder.TodayIn(timeProvider.GetUtcNow(), zone);
        var failed = ReadTempData<FailedSubmission>(BookingKeys.FailedSubmission);

        var selectedDate = failed?.Date ?? ReadDateQuery() ?? today;
        if (selectedDate < today)
        {
            selectedDate = today;
        }

        var duration = BookingFormBuilder.ResolveDisplayDuration(
            resource, failed?.DurationMinutes ?? ReadDurationQuery());

        // One query answers every length: the form filters these starts for the
        // chosen length and reads the longest available off the same result, so
        // an unavailable length can explain itself instead of rendering blank.
        var startsResult = await availability.GetBookableStartsAsync(resourceId, selectedDate, selectedDate);
        IReadOnlyList<BookableStart> starts = startsResult.Succeeded ? startsResult.Value : [];

        return View(BookingFormBuilder.Build(resource, selectedDate, today, starts, duration, zone, failed));
    }

    private DateOnly? ReadDateQuery()
        => DateOnly.TryParse(Request.Query[BookingKeys.DateQuery], out var date) ? date : null;

    private int? ReadDurationQuery()
        => int.TryParse(Request.Query[BookingKeys.DurationQuery], out var minutes) ? minutes : null;

    private T? ReadTempData<T>(string key)
    {
        if (TempData[key] is not string json || string.IsNullOrEmpty(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
