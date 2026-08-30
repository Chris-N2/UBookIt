using UBookIt.Core.Bookings;
using UBookIt.Persistence.Entities;

namespace UBookIt.Persistence.Stores;

/// <summary>
/// What a booking row's service columns mean, in one place.
/// </summary>
/// <remarks>
/// <para>
/// This rule defines what <c>NULL</c> is: <b>placed directly</b>, never "not recorded".
/// It was written twice — once in the booking store and once in the management projection —
/// and two copies of the sentence that gives a value its meaning have to be kept in step by
/// hand. If they ever drift, the same row reports one thing when it is read as an aggregate
/// and another when it is read as a list row, and nothing about that looks like a bug from
/// either side.
/// </para>
/// </remarks>
internal static class BookingAttributionMapper
{
    /// <summary>The stored attribution, or none.</summary>
    /// <remarks>
    /// Keyed on the id. A row carrying an id and no name has no producer — <see
    /// cref="ToColumns"/> writes both from the same source — but if one ever appeared,
    /// keeping the id with an empty name is the better failure: it shows up as a blank name
    /// on a row that still knows which service it was, which is recoverable, rather than as
    /// a booking claiming it was placed directly, which is a fabricated fact.
    /// </remarks>
    public static ServiceAttribution? ToAttribution(Guid? serviceId, string? serviceName)
        => serviceId is { } id ? new ServiceAttribution(id, serviceName ?? string.Empty) : null;

    /// <summary>Both columns, written from one source so they cannot disagree.</summary>
    public static (Guid? ServiceId, string? ServiceName) ToColumns(ServiceAttribution? service)
        => (service?.ServiceId, service?.DisplayName);

    /// <summary>The attribution a stored booking row carries.</summary>
    public static ServiceAttribution? ToAttribution(BookingRow row)
        => ToAttribution(row.ServiceId, row.ServiceName);
}
