using UBookIt.Backoffice.Models;
using UBookIt.Core.Stores;

namespace UBookIt.Backoffice.Mapping;

/// <summary>
/// Booking read models, one-for-one from what the management read port supplies — less
/// what the caller may not see.
/// <para>
/// Nothing is computed here. A mapper that derives a field is a second place the
/// package answers a question about a booking, free to answer it differently from the
/// port — which is the fault the containment requirement exists to prevent, arriving by
/// a quieter route than a raw store reference.
/// </para>
/// <para>
/// <b>Withholding is not computing.</b> Declining to carry a value the port supplied
/// answers no question about a booking; it answers a question about the caller, which
/// arrives already decided as <see cref="BookerVisibility"/>. The no-derivation rule
/// above holds unchanged — this mapper still invents nothing.
/// </para>
/// <para>
/// <b>The visibility is a required argument, not a step applied afterwards.</b> Umbraco's
/// own equivalent composes the response and then clears it, which is correct only for as
/// long as every present and future endpoint remembers to call the clearing step. A
/// required argument leaves nothing to remember, and the cost of forgetting here is
/// personal data reaching somebody the site excluded.
/// </para>
/// </summary>
internal static class BookingModelMapper
{
    public static BookingModel ToModel(BookingSummary summary, BookerVisibility bookerVisibility) => new()
    {
        BookingId = summary.BookingId,
        Reference = summary.Reference.Value,
        StartUtc = summary.Interval.StartUtc,
        EndUtc = summary.Interval.EndUtc,
        TimeZoneId = summary.Interval.TimeZoneId,
        Status = summary.Status.ToString(),
        CreatedUtc = summary.CreatedUtc,
        Booker = bookerVisibility is BookerVisibility.Shown
            ? new BookerModel
            {
                Name = summary.BookerName,
                Email = summary.BookerEmail,
            }
            : null,
        Resources = [.. summary.Resources.Select(resource => new BookedResourceModel
        {
            ResourceId = resource.ResourceId,
            DisplayName = resource.DisplayName,
        })],
        Service = summary.Service is { } service
            ? new BookedServiceModel
            {
                ServiceId = service.ServiceId,
                DisplayName = service.DisplayName,
            }
            : null,
    };
}
