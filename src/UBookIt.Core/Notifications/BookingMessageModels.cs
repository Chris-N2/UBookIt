using UBookIt.Core.Bookings;

namespace UBookIt.Core.Notifications;

/// <summary>
/// What every message's content is given about the booking it describes, whoever is reading.
/// </summary>
/// <remarks>
/// <para>
/// <b>Public API, frozen by the compatibility promise at the first full release</b> — which is
/// why these members are named for somebody writing content rather than for the package's own
/// convenience. They are also the vocabulary a later editor-facing feature would expose as the
/// values an editor may reference, so a name that reads awkwardly here reads awkwardly in a
/// backoffice field list too.
/// </para>
/// <para>
/// <b>Structure, not composed text.</b> What was booked arrives as a name and a collection
/// rather than as a joined sentence, and the interval arrives as instants rather than as
/// formatted strings. Being able to loop and to choose a format is the whole reason content is
/// Razor rather than a string with numbered placeholders; handing over a pre-joined
/// "Treatment Room, Ada" would take that back.
/// </para>
/// <para>
/// <b>The package converts, the content formats.</b> The instants are already expressed in the
/// zone the booking was placed against, because that rule is a guarantee the package makes and
/// getting it wrong is a real defect. How they are written is the author's.
/// </para>
/// </remarks>
public abstract record BookingMessageModel
{
    /// <summary>Which message this is — and therefore which file rendered it.</summary>
    public required BookingMessageKind Kind { get; init; }

    /// <summary>
    /// The booking's status at the moment the message was composed.
    /// </summary>
    /// <remarks>
    /// Carried separately from <see cref="Kind"/> because the two answer different questions and
    /// can disagree in ways content cares about: a <see cref="BookingMessageKind.BookerPlaced"/>
    /// message describes a <see cref="BookingStatus.Confirmed"/> booking on an auto-confirming
    /// site and a <see cref="BookingStatus.Requested"/> one where the site requires approval.
    /// Content that must distinguish those reads this.
    /// </remarks>
    public required BookingStatus Status { get; init; }

    /// <summary>
    /// The booking's reference **in the form a person is expected to quote** — grouped, as the
    /// confirmation screen shows it.
    /// </summary>
    /// <remarks>
    /// The display form rather than the canonical one because this is content a person reads,
    /// and the message and the screen must not disagree about what the customer is holding. A
    /// consumer that needs to compare or store a reference gets the canonical form from the
    /// delivery API instead.
    /// </remarks>
    public required string Reference { get; init; }

    /// <summary>
    /// The service the booking was placed for, as its name was recorded at placement — or
    /// <c>null</c> where the booking was placed directly against a resource, or where what was
    /// booked could not be established.
    /// </summary>
    /// <remarks>
    /// The recorded name, never a name read back now: renaming a service does not retitle the
    /// bookings already sold under the old name.
    /// </remarks>
    public string? ServiceName { get; init; }

    /// <summary>
    /// Every resource the booking claimed, by name, in the booking's own claim order — empty
    /// where none could be established.
    /// </summary>
    /// <remarks>
    /// <b>A collection, never a sentence.</b> A service resolves one resource per role, and a
    /// booker who booked a room and a therapist was given both; content that named one would
    /// describe a different booking from the one that exists.
    /// </remarks>
    public required IReadOnlyList<string> ResourceNames { get; init; }

    /// <summary>The booking's start, in the time zone it was placed against.</summary>
    public required DateTimeOffset LocalStart { get; init; }

    /// <summary>The booking's end, in the time zone it was placed against.</summary>
    public required DateTimeOffset LocalEnd { get; init; }

    /// <summary>
    /// The IANA id of the zone the two instants above are expressed in, so content can say which
    /// clock it is quoting.
    /// </summary>
    /// <remarks>
    /// Worth stating in a message rather than assuming: a site that changes its own time zone
    /// does not restate when its existing bookings are, so two messages from one site can
    /// legitimately quote two different zones.
    /// </remarks>
    public required string TimeZoneId { get; init; }
}

/// <summary>
/// What a message to the person who booked is given.
/// </summary>
/// <remarks>
/// This is the model that carries contact details, and it carries them because the message is
/// addressed to the person they belong to.
/// </remarks>
public sealed record BookerMessageModel : BookingMessageModel
{
    /// <summary>The name the booker gave.</summary>
    public required string BookerName { get; init; }

    /// <summary>The address this message is being sent to.</summary>
    public required string BookerEmail { get; init; }

    /// <summary>The telephone number the booker gave, or <c>null</c> where they gave none.</summary>
    public string? BookerPhone { get; init; }

    /// <summary>
    /// For the <see cref="BookingMessageKind.BookerMoved"/> message: the start the booking held
    /// before it was moved, in the time zone the booking is expressed in. <c>null</c> for every
    /// other message.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>null</c> means "this message is not about a move"</b>, not "not recorded". Added in
    /// 17.1.0; a view written before it existed renders unchanged, because nothing it read has
    /// moved.
    /// </para>
    /// <para>
    /// Held to the same terms as <see cref="BookingMessageModel.LocalStart"/>: an instant already
    /// converted to the booking's zone, never a formatted string.
    /// </para>
    /// </remarks>
    public DateTimeOffset? PreviousLocalStart { get; init; }

    /// <summary>
    /// For the <see cref="BookingMessageKind.BookerMoved"/> message: the end the booking held
    /// before it was moved, in the time zone the booking is expressed in. <c>null</c> for every
    /// other message.
    /// </summary>
    public DateTimeOffset? PreviousLocalEnd { get; init; }

    /// <summary>
    /// Where the booker may cancel this booking themselves, or <c>null</c> where they may not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>null</c> means "there is no self-service cancellation for this booking"</b>, not "a
    /// link was expected and could not be built". It is absent on a site with the feature off, on
    /// a site that cannot establish its own address, and on every message about a booking that was
    /// not issued a link. Added in 17.1.0; a view written before it existed renders unchanged.
    /// </para>
    /// <para>
    /// <b>A complete address, not parts to assemble</b> — the stated exception to "the package
    /// converts, the view formats". There is no presentational choice worth leaving to an author
    /// here, and a view that built its own link could build a wrong one, which is
    /// indistinguishable from a working one until a customer needs it.
    /// </para>
    /// <para>
    /// <b>Only the message that issued a link carries one.</b> A later message about the same
    /// booking has this absent, because restating a single-use credential would put it in a second
    /// mailbox copy with nothing to tell the reader which is live.
    /// </para>
    /// </remarks>
    public Uri? CancellationUrl { get; init; }
}

/// <summary>
/// What a message to the site's own recipients is given — the configured list and the
/// booking's resolved responsible parties alike; one message serves the whole audience.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is deliberately no member for the booker's name, email address or telephone
/// number, and that absence is a guarantee rather than an omission.</b> Who may see a booker's
/// contact details is decided by the backoffice's <c>Sensitive data</c> group; a list of
/// addresses in a configuration file is a different population, gated by who can edit
/// configuration. Routing personal data there would bypass a control this package built
/// deliberately.
/// </para>
/// <para>
/// <b>Why a separate type rather than one model with the booker left null.</b> The promise is
/// currently about what the package does. Shared with a nullable booker, it would quietly become
/// a promise about what every site's content author does — and nothing would report the
/// difference. Here there is nothing to render, so the compiler enforces what a reviewer would
/// otherwise have to, which is the same reasoning <see cref="Booker.Contact"/>'s nullability
/// already records.
/// </para>
/// <para>
/// A site that genuinely wants contact details in its internal mail can still send such a
/// message itself, by handling the booking notifications. What it cannot do is have this package
/// send one.
/// </para>
/// </remarks>
public sealed record InternalMessageModel : BookingMessageModel
{
    /// <summary>
    /// Whether this booking is waiting for somebody to confirm or decline it.
    /// </summary>
    /// <remarks>
    /// Derived from the booking's status, never from the site's <c>AutoConfirm</c> setting: the
    /// message describes the booking it announces, and reading the setting instead would let the
    /// two drift the day anything else decides a placement's status.
    /// </remarks>
    public required bool AwaitsApproval { get; init; }

    /// <summary>
    /// Where the booking can be seen in the backoffice, or <c>null</c> where no such link could
    /// be built.
    /// </summary>
    /// <remarks>
    /// This is what the message carries <i>instead of</i> the booker: it takes a reader to where
    /// the Sensitive data control still applies, rather than carrying the details past it. It is
    /// nullable because a site with no configured application URL has no link to give — Umbraco
    /// declares that URL non-nullable and it is not, which this package learned the hard way.
    /// </remarks>
    public Uri? BackofficeUrl { get; init; }
}
