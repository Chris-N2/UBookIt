using UBookIt.Backoffice.Models;
using UBookIt.Core.Stores;

namespace UBookIt.Backoffice.Mapping;

/// <summary>
/// Booking read models, one-for-one from what the management read port supplies.
/// <para>
/// Nothing is computed here. A mapper that derives a field is a second place the
/// package answers a question about a booking, free to answer it differently from the
/// port — which is the fault the containment requirement exists to prevent, arriving by
/// a quieter route than a raw store reference.
/// </para>
/// </summary>
internal static class BookingModelMapper
{
    public static BookingModel ToModel(BookingSummary summary) => new()
    {
        BookingId = summary.BookingId,
        StartUtc = summary.Interval.StartUtc,
        EndUtc = summary.Interval.EndUtc,
        TimeZoneId = summary.Interval.TimeZoneId,
        Status = summary.Status.ToString(),
        CreatedUtc = summary.CreatedUtc,
        BookerName = summary.BookerName,
        BookerEmail = summary.BookerEmail,
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
