using UBookIt.Core.Common;

namespace UBookIt.Core.Bookings;

public enum BookingStatus
{
    Requested = 0,
    Confirmed = 1,
    Declined = 2,
    Cancelled = 3,
}

/// <summary>
/// Binds one resource to its booking's interval. A booking has 1..N claims;
/// v1 behaviour creates exactly one, but the model is deliberately plural so
/// that "room + person" bookings are an additive change.
/// </summary>
public sealed record ResourceClaim(Guid ResourceId);

/// <summary>
/// The service a booking was placed for, recorded on the booking itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>The display name is a snapshot taken at placement, not a reference resolved later.</b>
/// Resolving it on read would report the service's <i>current</i> name — retitling bookings
/// that were sold under the old one — and would report nothing at all once the service is
/// deleted, losing an attribution the booking definitely had. What this records is what was
/// booked at the time. The id is carried alongside for a caller that needs the service as
/// it stands now, and that caller has to accept it may no longer exist.
/// </para>
/// <para>
/// This is deliberately the opposite conclusion from <c>BookedResource</c>, which reads a
/// resource's name live. A resource claim is a live association; a service attribution is
/// history.
/// </para>
/// </remarks>
public sealed record ServiceAttribution(Guid ServiceId, string DisplayName);

/// <summary>
/// A booking: one continuous interval, one booker, 1..N resource claims, and a
/// status. Transitions are enforced here; see the bookings spec status machine.
/// </summary>
public sealed class Booking
{
    private readonly List<ResourceClaim> _claims;

    private Booking(
        Guid id,
        BookingReference reference,
        BookingInterval interval,
        Booker booker,
        List<ResourceClaim> claims,
        BookingStatus status,
        DateTimeOffset createdUtc,
        ServiceAttribution? service)
    {
        Id = id;
        Reference = reference;
        Interval = interval;
        Booker = booker;
        _claims = claims;
        Status = status;
        CreatedUtc = createdUtc;
        Service = service;
    }

    public Guid Id { get; }

    /// <summary>
    /// The identifier a person quotes. Assigned at placement and never changed afterwards —
    /// not by confirming, declining or cancelling, and not by any later amendment of the
    /// booking's time.
    /// <para>
    /// That immutability is the whole value of it. The customer is holding the reference they
    /// were given; a system that reassigns it denies all knowledge of the booking the person
    /// is asking about. It has no setter for the same reason <see cref="Id"/> has none.
    /// </para>
    /// <para>
    /// It is also deliberately independent of the booker: a booking whose personal details are
    /// later removed still occupies its interval and still has to be discussable, so nothing
    /// about the reference is derived from the person.
    /// </para>
    /// </summary>
    public BookingReference Reference { get; }

    public BookingInterval Interval { get; }

    /// <summary>
    /// Who the booking is for. Always present — erasure removes the person's details, not
    /// the booker — and mutable only through <see cref="EraseBooker"/>.
    /// </summary>
    /// <remarks>
    /// <c>private set</c> for the same reason <see cref="Status"/> has one: there is exactly
    /// one way a booking's booker may change after placement, it is named after what it does,
    /// and it is on the aggregate rather than available to any caller holding a reference.
    /// </remarks>
    public Booker Booker { get; private set; }

    public IReadOnlyList<ResourceClaim> Claims => _claims;

    public BookingStatus Status { get; private set; }

    public DateTimeOffset CreatedUtc { get; }

    /// <summary>
    /// The service this booking was placed for, or <c>null</c> for one placed directly.
    /// </summary>
    /// <remarks>
    /// <b><c>null</c> means placed directly — it does not mean "not recorded".</b> Whether
    /// a resource may be booked on its own is a per-resource permission that defaults to
    /// withheld, and a resource withholding it stays fully usable as part of a service, so
    /// the two kinds of booking coexist permanently on any site. A booking placed directly
    /// has no service and never will, which is why this is nullable in the domain rather
    /// than nullable as a concession to storage.
    /// </remarks>
    public ServiceAttribution? Service { get; }

    /// <summary>True when this booking's claims block other bookings and reduce free time.</summary>
    public bool IsBlocking => Status is BookingStatus.Requested or BookingStatus.Confirmed;

    internal static Booking Create(
        Guid id,
        BookingReference reference,
        BookingInterval interval,
        Booker booker,
        IEnumerable<ResourceClaim> claims,
        BookingStatus status,
        DateTimeOffset createdUtc,
        // Required, not defaulted: this is internal with one call site, so making the
        // caller state "no service" costs nothing and removes the silent-omission case
        // inside Core entirely. `Rehydrate` keeps a default because it is the persistence
        // boundary with many legitimate callers that have no service to give.
        ServiceAttribution? service)
    {
        var claimList = claims.ToList();

        if (claimList.Count == 0)
        {
            throw new ArgumentException("A booking requires at least one resource claim.", nameof(claims));
        }

        if (claimList.Select(c => c.ResourceId).Distinct().Count() != claimList.Count)
        {
            throw new ArgumentException("A booking cannot claim the same resource twice.", nameof(claims));
        }

        return new Booking(id, reference, interval, booker, claimList, status, createdUtc.ToUniversalTime(), service);
    }

    /// <summary>
    /// Materializes a booking from stored state. Persistence-boundary API:
    /// enforces structural invariants (at least one claim, no duplicate
    /// resources) but accepts any status without transition rules — the
    /// stored status is historical fact, not a transition. Placement via
    /// <see cref="IBookingService"/> remains the only pathway that creates
    /// new bookings.
    /// <para>
    /// <b>The recorded service is not revalidated.</b> It is historical fact on
    /// the same terms as the stored status: the service may since have been
    /// renamed, retired or deleted, and none of that changes what the booking
    /// was placed for. Rehydration that failed on a service which no longer
    /// exists would make old bookings unreadable for having been successful.
    /// </para>
    /// </summary>
    public static DomainResult<Booking> Rehydrate(
        Guid id,
        BookingReference reference,
        BookingInterval interval,
        Booker booker,
        IEnumerable<ResourceClaim> claims,
        BookingStatus status,
        DateTimeOffset createdUtc,
        ServiceAttribution? service = null)
    {
        var claimList = claims.ToList();

        if (claimList.Count == 0)
        {
            return DomainResult<Booking>.Failure(
                FailureCodes.ClaimsInvalid, "A booking requires at least one resource claim.");
        }

        if (claimList.Select(c => c.ResourceId).Distinct().Count() != claimList.Count)
        {
            return DomainResult<Booking>.Failure(
                FailureCodes.ClaimsInvalid, "A booking cannot claim the same resource twice.");
        }

        return DomainResult<Booking>.Success(
            new Booking(id, reference, interval, booker, claimList, status, createdUtc.ToUniversalTime(), service));
    }

    /// <summary>
    /// Removes the booker's contact details and member key, recording when.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing else about the booking changes.</b> Its id, reference, interval, time zone,
    /// status, creation time, claims and service attribution are untouched — in particular it
    /// goes on blocking exactly the time it blocked before. An erased booking still occupies
    /// its slot, which is the whole reason erasure is anonymisation rather than deletion:
    /// deleting the row would silently return time the site had sold.
    /// </para>
    /// <para>
    /// <b>Erasing an already-erased booking is a no-op, and keeps the first instant.</b>
    /// Deliberately the opposite conclusion from <see cref="Cancel"/>, which refuses a second
    /// attempt. Cancelling is a transition whose starting state matters, so a caller told
    /// "cancelled" when nothing changed cannot tell a completed action from a rejected one.
    /// Erasure has no starting state to be wrong about: it is absorbing, the observable
    /// outcome is identical either way — the details are gone — and it is invoked by
    /// machinery that must be safe to retry. Overwriting the instant would also be a lie
    /// about when the data actually left.
    /// </para>
    /// <para>
    /// Returns nothing, because there is no way for it to fail. An erasure a caller could
    /// have refused is an erasure a caller might skip.
    /// </para>
    /// <para>
    /// <b>This changes the aggregate and NOTHING ELSE — it does not persist.</b> Handing the
    /// result to <see cref="Stores.IBookingStore.UpdateAsync"/> will not erase anything: that
    /// method writes the status and deliberately not the booker. To erase a stored booking,
    /// call <c>IBookingService.EraseBookerAsync</c>, or the store's own
    /// <c>EraseBookerAsync</c> — either of which reaches the one write that touches the
    /// booker columns.
    /// </para>
    /// <para>
    /// Stated here because this is the method a caller reaches for first, and because the
    /// combination "erase the aggregate, then update it" is a silent no-op that returns
    /// success and satisfies any assertion made against the returned booking. That exact
    /// mistake is what the storage round-trip tests exist to catch.
    /// </para>
    /// </remarks>
    /// <param name="erasedUtc">When the erasure happened.</param>
    public void EraseBooker(DateTimeOffset erasedUtc)
    {
        if (Booker.IsErased)
        {
            return;
        }

        Booker = Booker.Erased(erasedUtc);
    }

    public DomainResult Confirm() => Transition(BookingStatus.Confirmed, BookingStatus.Requested);

    public DomainResult Decline() => Transition(BookingStatus.Declined, BookingStatus.Requested);

    public DomainResult Cancel() => Transition(BookingStatus.Cancelled, BookingStatus.Requested, BookingStatus.Confirmed);

    private DomainResult Transition(BookingStatus target, params IReadOnlyList<BookingStatus> allowedFrom)
    {
        if (!allowedFrom.Contains(Status))
        {
            return DomainResult.Failure(
                FailureCodes.InvalidStatusTransition,
                $"A booking cannot move from {Status} to {target}.");
        }

        Status = target;
        return DomainResult.Success();
    }
}
