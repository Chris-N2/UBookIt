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
        DateTimeOffset createdUtc)
    {
        Id = id;
        Interval = interval;
        Booker = booker;
        _claims = claims;
        Status = status;
        CreatedUtc = createdUtc;
    }

    public Guid Id { get; }

    public BookingInterval Interval { get; }

    public Booker Booker { get; }

    public IReadOnlyList<ResourceClaim> Claims => _claims;

    public BookingStatus Status { get; private set; }

    public DateTimeOffset CreatedUtc { get; }

    /// <summary>True when this booking's claims block other bookings and reduce free time.</summary>
    public bool IsBlocking => Status is BookingStatus.Requested or BookingStatus.Confirmed;

    internal static Booking Create(
        Guid id,
        BookingInterval interval,
        Booker booker,
        IEnumerable<ResourceClaim> claims,
        BookingStatus status,
        DateTimeOffset createdUtc)
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

        return new Booking(id, interval, booker, claimList, status, createdUtc.ToUniversalTime());
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
