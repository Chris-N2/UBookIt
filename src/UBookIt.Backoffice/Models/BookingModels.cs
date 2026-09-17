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
/// Why a booking's row does or does not carry contact details, by name.
/// </summary>
/// <remarks>
/// <para>
/// <b>Stated, never inferred.</b> There are two reasons a caller may receive no contact
/// details — they may not see them, or nobody can — and they demand different actions:
/// <i>ask a colleague who has access</i> versus <i>this is gone permanently</i>. A client
/// made to tell those apart by observing which fields are absent would be deducing something
/// the server already knows.
/// </para>
/// <para>
/// <b>Constants rather than an enum</b>, on the same terms as
/// <see cref="BookingModel.Status"/>: an enum's members are a versioning commitment, and
/// putting one on the wire couples this published contract to the order and spelling of a
/// declaration elsewhere.
/// </para>
/// </remarks>
public static class BookerConditions
{
    /// <summary>The details are present in <see cref="BookerModel.Contact"/>.</summary>
    public const string Shown = "Shown";

    /// <summary>The details exist, and this caller is not permitted to see them.</summary>
    public const string Withheld = "Withheld";

    /// <summary>The details were erased. Nobody can retrieve them.</summary>
    public const string Erased = "Erased";
}

/// <summary>The booker's contact details, where the row carries them.</summary>
/// <remarks>
/// <para>
/// Held together as one object so that name and email cannot be observed half-populated:
/// they are supplied, withheld and erased together, and a client meeting one without the
/// other would have no correct way to read it.
/// </para>
/// <para>
/// It carries <b>only</b> what the management read port supplies. The booker's phone number
/// and member key are stored and rehydrated by the domain, and have never reached this port;
/// adding either here would be adding personal data at the HTTP layer that the port cannot
/// fill, which the endpoint's contract forbids for the ordinary reason and the sensitive-data
/// capability forbids for a second one.
/// </para>
/// </remarks>
public class BookerContactModel
{
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}

/// <summary>The person a booking was made for, as the list row shows them.</summary>
/// <remarks>
/// <para>
/// <b>Always present, and it states its own condition.</b> Every booking has a booker — the
/// domain requires one, and erasure removes the person's details rather than the booker — so
/// this member is never null. What varies is whether <see cref="Contact"/> accompanies it,
/// and <see cref="Condition"/> says why.
/// </para>
/// <para>
/// <b>BREAKING, made before the first release reached a feed:</b> this replaces a nullable booker member whose null meant
/// "withheld from you". That null was spent the moment erasure existed: absence acquired a
/// second cause, and a member whose absence is already meaningful must not be overloaded to
/// carry withholding as well. Stating the condition removes the ambiguity rather than
/// documenting it.
/// </para>
/// </remarks>
public class BookerModel
{
    /// <summary>
    /// One of <see cref="BookerConditions"/>: <c>Shown</c>, <c>Withheld</c> or <c>Erased</c>.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="BookerConditions.Withheld"/> so that a model built without
    /// stating a condition discloses nothing, on the same terms as <c>BookerVisibility</c>
    /// making withholding its zero value.
    /// </remarks>
    public string Condition { get; set; } = BookerConditions.Withheld;

    /// <summary>
    /// The contact details, present only when <see cref="Condition"/> is <c>Shown</c>.
    /// </summary>
    /// <remarks>
    /// Absent for both of the other conditions, and in neither case blanked: a default value
    /// in a response is not evidence of the underlying state, and a client cannot tell one
    /// from the other. Which of the two applies is read from <see cref="Condition"/>, not
    /// from this being null.
    /// </remarks>
    public BookerContactModel? Contact { get; set; }

    /// <summary>
    /// When the details were erased, present only when <see cref="Condition"/> is
    /// <c>Erased</c>.
    /// </summary>
    public DateTimeOffset? ErasedUtc { get; set; }
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

    /// <summary>
    /// The reference a caller reads out, in canonical form.
    /// <para>
    /// Carried alongside <see cref="BookingId"/> rather than instead of it, because they have
    /// different readers: the id is what the client sends back to cancel a booking, and this
    /// is what an operator matches against what the person on the telephone is holding.
    /// </para>
    /// </summary>
    public string Reference { get; set; } = string.Empty;

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

    /// <summary>
    /// The person the booking was made for, and whether this caller may see their details.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Never null.</b> It states one of three conditions — shown, withheld, or erased — and
    /// carries the contact details only for the first. A client reads the condition; it never
    /// infers one from the absence of a field.
    /// </para>
    /// <para>
    /// The decision is made server-side, before this model is composed. It is never a matter of
    /// a client receiving the details and declining to render them: the values would be in the
    /// payload, readable by exactly the user the site meant to exclude.
    /// </para>
    /// <para>
    /// <b>Erasure outranks withholding.</b> Where details have been erased there is nothing to
    /// withhold, so a caller without sensitive-data access is told the booking is erased rather
    /// than that its details are hidden. That a record was erased is a fact about the record,
    /// not about the person, and it discloses nothing about who they were — while telling an
    /// operator the difference between "ask a colleague who has access" and "there is nobody
    /// to ask".
    /// </para>
    /// </remarks>
    public BookerModel Booker { get; set; } = new();

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

/// <summary>
/// What a confirmation returns: identity and new status, on
/// <see cref="CancelledBookingModel"/>'s terms — this path reaches the booking through the
/// domain and cannot honestly fill a list row.
/// </summary>
public class ConfirmedBookingModel
{
    public Guid BookingId { get; set; }

    /// <summary>The booking's status by name — <c>Confirmed</c>, on success.</summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// What a decline returns, on the same terms as <see cref="ConfirmedBookingModel"/>.
/// </summary>
public class DeclinedBookingModel
{
    public Guid BookingId { get; set; }

    /// <summary>The booking's status by name — <c>Declined</c>, on success.</summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// What a move asks for: where the booking should now be.
/// </summary>
/// <remarks>
/// <para>
/// <b>The start is the site's own wall-clock time, with no offset</b> — <c>2026-09-20T09:00</c>
/// — on the same convention as the list's window dates: the operator is looking at a screen in
/// the site's zone, and a time typed there means that time in that zone. The server converts,
/// once, because the site's zone is a server setting and a headless client is not one client.
/// </para>
/// <para>
/// The length is in minutes rather than an ISO duration, because the client is a number input
/// and the domain is minute-granular. Both are validated before the domain is asked: a missing
/// start or a non-positive length is answered with the domain's <c>interval-invalid</c> code
/// against the offending field, so a caller learns which of the two to fix.
/// </para>
/// </remarks>
public class MoveBookingRequestModel
{
    /// <summary>The new start, as a wall-clock time in the site's zone. No offset.</summary>
    public DateTime Start { get; set; }

    /// <summary>The new length, in minutes. Must be positive.</summary>
    public int LengthMinutes { get; set; }
}

/// <summary>
/// What a move returns: the booking's identity, its status, and the interval it now holds.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately not a whole <see cref="BookingModel"/></b>, on <see cref="CancelledBookingModel"/>'s
/// terms: this path reaches the booking through the domain, which knows resource ids and not
/// their names. It carries what the operation knows — and the interval is what a move is about,
/// so the caller can see where the booking landed without reading it back.
/// </para>
/// <para>
/// The status is carried because it is <i>unchanged</i> by a move, and a caller that assumed
/// otherwise — "moved means confirmed" — would be wrong on an approval site. Instants are UTC
/// with the zone id alongside, as the list carries them.
/// </para>
/// </remarks>
public class MovedBookingModel
{
    public Guid BookingId { get; set; }

    /// <summary>The booking's status by name — unchanged by the move.</summary>
    public string Status { get; set; } = string.Empty;

    public DateTimeOffset StartUtc { get; set; }

    public DateTimeOffset EndUtc { get; set; }

    /// <summary>The IANA zone the new interval was validated against.</summary>
    public string TimeZoneId { get; set; } = string.Empty;
}

/// <summary>
/// What an operator may book on somebody's behalf: the site's services and its resources, by
/// name, for the dialog's picker.
/// </summary>
/// <remarks>
/// <para>
/// <b>It exists because the configuration listings are gated on a verb an operator need not
/// hold.</b> <c>GET resources</c> and <c>GET services</c> require <c>UBookIt.Configure</c> — the
/// verb for adding a meeting room — and the person taking a telephone booking holds
/// <c>UBookIt.Bookings.Manage</c>. Without this read their picker is empty and the feature does
/// not work; with it, the verb that may take a booking may also see what there is to book, which
/// is the smallest grant that makes the act possible.
/// </para>
/// <para>
/// <b>It is a name and an id and nothing else.</b> Not a projection of the configuration models:
/// open hours, constraints, capabilities and role structure are <c>Configure</c>'s business, and
/// a picker needs none of them. Keeping it this thin is what stops it becoming a way to read the
/// configuration without the verb for it.
/// </para>
/// <para>
/// <b>It carries no booker and no booking</b>, so it discloses nothing about anybody: a resource
/// is a room and a service is something the site sells, both of which a visitor can already see
/// on the public site.
/// </para>
/// </remarks>
public class BookableSubjectsModel
{
    public IReadOnlyList<BookableSubjectModel> Services { get; set; } = [];

    public IReadOnlyList<BookableSubjectModel> Resources { get; set; } = [];
}

/// <summary>One thing an operator can book, as the picker needs it.</summary>
public class BookableSubjectModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// What an operator supplies to record a booking somebody made by telephone or at a desk.
/// </summary>
/// <remarks>
/// <para>
/// <b>Exactly one of <see cref="ServiceId"/> and <see cref="ResourceId"/>.</b> Both, or
/// neither, is refused as a malformed request rather than resolved by a precedence rule: a
/// caller that supplied both did not mean one of them, and choosing for them would commit the
/// site's time to a booking nobody asked for.
/// </para>
/// <para>
/// The start and the length carry <see cref="MoveBookingRequestModel"/>'s convention exactly —
/// wall-clock time in the site's zone with no offset, and minutes — because an operator typing
/// into this modal and into the move modal is doing the same thing, and two conventions would
/// be a trap on whichever screen they used second.
/// </para>
/// <para>
/// <b>The booker's details are carried here and reported nowhere.</b> The endpoint accepts them
/// to store them; <see cref="PlacedBookingModel"/> has no member for any of them, so a write
/// cannot become a read of personal data wearing a write's authorization.
/// </para>
/// </remarks>
public class PlaceBookingOnBehalfRequestModel
{
    /// <summary>The service to book, when booking one. Mutually exclusive with <see cref="ResourceId"/>.</summary>
    public Guid? ServiceId { get; set; }

    /// <summary>The resource to book, when booking one directly. Mutually exclusive with <see cref="ServiceId"/>.</summary>
    public Guid? ResourceId { get; set; }

    /// <summary>The start, as a wall-clock time in the site's zone. No offset.</summary>
    public DateTime Start { get; set; }

    /// <summary>The length, in minutes. Must be positive.</summary>
    public int LengthMinutes { get; set; }

    /// <summary>The booker's name. Required, as for any placement.</summary>
    public string BookerName { get; set; } = string.Empty;

    /// <summary>The booker's email address. Required and validated exactly as a visitor's is.</summary>
    public string BookerEmail { get; set; } = string.Empty;

    /// <summary>The booker's telephone number, where they gave one.</summary>
    public string? BookerPhone { get; set; }
}

/// <summary>
/// What a placement on a booker's behalf returns: the booking's identity, the reference an
/// operator will read out, its status, and the interval it holds.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately not a whole <see cref="BookingModel"/></b>, on <see cref="MovedBookingModel"/>'s
/// terms: this path reaches the booking through the domain, which knows resource ids and not
/// their names.
/// </para>
/// <para>
/// <b>It carries the reference, and that is the one thing it adds over a move's response.</b> An
/// operator is holding somebody on the telephone; the reference is what they say next, and
/// making them read the booking back to find it would be a worse screen for no reason.
/// </para>
/// <para>
/// <b>It carries no booker.</b> Not "the booker with the details omitted" — no member for a
/// name, an address or a telephone number at all, so content cannot render what this response
/// has promised not to route back. The guarantee is structural rather than dependent on the
/// caller happening to hold sensitive-data access, because the gate on the endpoint is under
/// review and the guarantee is not.
/// </para>
/// <para>
/// The status is carried because an operator's placement is <c>Confirmed</c> whatever the site's
/// approval setting says, and a caller that assumed otherwise would be wrong on an approval
/// site — the same reason a move carries its unchanged status.
/// </para>
/// </remarks>
public class PlacedBookingModel
{
    public Guid BookingId { get; set; }

    /// <summary>The quotable reference, as a person would read it out.</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>The booking's status by name.</summary>
    public string Status { get; set; } = string.Empty;

    public DateTimeOffset StartUtc { get; set; }

    public DateTimeOffset EndUtc { get; set; }

    /// <summary>The IANA zone the interval was validated against.</summary>
    public string TimeZoneId { get; set; } = string.Empty;
}

/// <summary>
/// What an erasure returns: the booking's identity and when its booker was erased.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately not a whole <see cref="BookingModel"/></b>, for the same reason
/// <see cref="CancelledBookingModel"/> is not: this path reaches the booking through the
/// domain, which knows a booking's resource <i>ids</i> and not their names, so a response
/// shaped like the list's would be quietly less true than it.
/// </para>
/// <para>
/// <b>It carries nothing that was erased.</b> Returning the removed name or address as
/// confirmation would hand back the data the operation exists to remove — and would put it
/// in a response body, a browser's network log and any proxy in between, at the exact moment
/// the site was told to stop holding it.
/// </para>
/// <para>
/// The instant is returned because it is the one fact the caller did not already have, and
/// because it is what makes a repeated call legible: an erasure that reports the original
/// instant is telling the caller this was already done, without failing.
/// </para>
/// </remarks>
public class ErasedBookerModel
{
    public Guid BookingId { get; set; }

    /// <summary>When the booker's details were erased — the <b>first</b> erasure's instant.</summary>
    public DateTimeOffset ErasedUtc { get; set; }
}

/// <summary>
/// What a search for a subject's bookings asks for: one address, and a page.
/// </summary>
/// <remarks>
/// <para>
/// <b>A request body rather than a query string, and the endpoint is a POST for a read.</b> An
/// email address in a URL is written to the web server's log, to every proxy's log in between,
/// and to the operator's browser history — none of which the endpoint's own authorization
/// controls. This capability's whole point is that a booker's address reaches only somebody
/// Umbraco permits to see it, and a gate that holds for the response while the request scatters
/// the value through the infrastructure is not much of a gate. POST keeps it in a body that
/// nothing logs by default.
/// </para>
/// <para>
/// The cost is that a read is not a GET: it is not cacheable and not bookmarkable. Neither is
/// wanted here — a search for a person is a thing to do once and not to leave lying in a URL bar
/// — so the trade is one-sided.
/// </para>
/// <para>
/// <b>No status or resource filter.</b> Somebody asking to be forgotten is asking about every
/// booking they made; a filtered search would answer a narrower question than the one they asked
/// and report fewer of their records than exist.
/// </para>
/// </remarks>
public class FindBookingsByBookerModel
{
    /// <summary>
    /// The address to find, matched <b>exactly</b>.
    /// </summary>
    /// <remarks>
    /// No prefix, substring or wildcard form is offered, here or anywhere. An exact match
    /// answers whether a given person is in the records; a partial one answers which people
    /// match a fragment, which is an enumeration facility and is forbidden by the
    /// <c>sensitive-data</c> capability rather than merely unimplemented.
    /// </remarks>
    public string Email { get; set; } = string.Empty;

    public int? Skip { get; set; }

    public int? Take { get; set; }
}

/// <summary>A page of bookings plus the unpaged total, matching the other list endpoints.</summary>
public class PagedBookingsModel
{
    public int Total { get; set; }

    public IReadOnlyList<BookingModel> Items { get; set; } = [];
}
