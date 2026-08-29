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
        BookingInterval interval,
        Booker booker,
        List<ResourceClaim> claims,
        BookingStatus status,
        DateTimeOffset createdUtc,
        ServiceAttribution? service)
    {
        Id = id;
        Interval = interval;
        Booker = booker;
        _claims = claims;
        Status = status;
        CreatedUtc = createdUtc;
        Service = service;
    }

    public Guid Id { get; }

    public BookingInterval Interval { get; }

    public Booker Booker { get; }

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
            throw new ArgumentException("A booking requires at least one resource claim.", nameof(claims));
        }

        if (claimList.Select(c => c.ResourceId).Distinct().Count() != claimList.Count)
        {
            throw new ArgumentException("A booking cannot claim the same resource twice.", nameof(claims));
        }

        return new Booking(id, interval, booker, claimList, status, createdUtc.ToUniversalTime(), service);
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
            new Booking(id, interval, booker, claimList, status, createdUtc.ToUniversalTime(), service));
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
