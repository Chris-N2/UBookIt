namespace UBookIt.Backoffice.Models;

/// <summary>One resource a booking claims, as the list row shows it.</summary>
public class BookedResourceModel
{
    public Guid ResourceId { get; set; }

    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>The service a booking was placed for, as the list row shows it.</summary>
/// <remarks>
/// The name is the one recorded when the booking was placed, so a booking sold under an
/// older name keeps showing that name, and a booking whose service has since been deleted
/// still says what it was. The id is carried for a caller that wants the service as it
/// stands now — which may be not at all.
/// </remarks>
public class BookedServiceModel
{
    public Guid ServiceId { get; set; }

    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>
/// A booking as a management list row.
/// </summary>
/// <remarks>
/// <para>
/// A purpose-built model, not a domain type: the HTTP contract is versioned and public
/// while <c>BookingSummary</c> is free to change with the port.
/// </para>
/// <para>
/// It carries <b>exactly</b> what the read port supplies and nothing more. A field added
/// here that the port cannot fill is a field the screen would have to fetch some other
/// way, which is how a second read path into bookings begins.
/// </para>
/// <para>
/// <c>TimeZoneId</c> travels with the interval so a client can render local time without
/// having to know, or ask for, the site's zone.
/// </para>
/// </remarks>
public class BookingModel
{
    public Guid BookingId { get; set; }

    public DateTimeOffset StartUtc { get; set; }

    public DateTimeOffset EndUtc { get; set; }

    /// <summary>The zone the booking was made in — its own, not the site's current setting.</summary>
    public string TimeZoneId { get; set; } = string.Empty;

    /// <summary>
    /// The booking's status by name.
    /// <para>
    /// A string rather than the domain enum, matching how <c>ServiceModels</c> carries a
    /// duration kind. Domain types do not appear in the HTTP contract — an enum is a
    /// domain type whose members are a versioning commitment, and putting it on the wire
    /// couples the published contract to the order and spelling of a Core declaration.
    /// </para>
    /// </summary>
    public string Status { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }

    public string BookerName { get; set; } = string.Empty;

    public string BookerEmail { get; set; } = string.Empty;

    public IReadOnlyList<BookedResourceModel> Resources { get; set; } = [];

    /// <summary>
    /// The service this booking was placed for, or <c>null</c> for one placed directly.
    /// </summary>
    /// <remarks>
    /// <b>One nullable object rather than two parallel nullable fields</b>, so that "a
    /// booking has a service, or it does not" is expressed in the shape. Two nullable
    /// scalars can be observed half-populated, and a client that meets that state has no
    /// correct way to interpret it.
    /// <para>
    /// <c>null</c> means placed directly. It does not mean the service is unknown.
    /// </para>
    /// </remarks>
    public BookedServiceModel? Service { get; set; }
}

/// <summary>
/// What a cancellation returns: the booking's identity and its new status.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately not a whole <see cref="BookingModel"/>.</b> That model carries each claimed
/// resource's <i>name</i>, which the management port supplies by joining — and cancellation
/// goes through the Core booking service, which knows a booking's resource <i>ids</i> and
/// nothing more. Returning a <c>BookingModel</c> here would mean either reading the booking
/// back through a port that has no by-id read, or filling those names with blanks: a response
/// shaped like the list's and quietly less true than it.
/// </para>
/// <para>
/// This is what the operation actually knows, and it is enough for what a caller does next.
/// The row's consequences — whether it still belongs in the current filter, what the total is
/// now — are a property of the query rather than of the booking, so a caller reloads the list
/// to see them.
/// </para>
/// </remarks>
public class CancelledBookingModel
{
    public Guid BookingId { get; set; }

    /// <summary>The booking's status by name — <c>Cancelled</c>, on success.</summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>A page of bookings plus the unpaged total, matching the other list endpoints.</summary>
public class PagedBookingsModel
{
    public int Total { get; set; }

    public IReadOnlyList<BookingModel> Items { get; set; } = [];
}
