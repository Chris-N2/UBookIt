namespace UBookIt.Core.Stores;

/// <summary>
/// An outstanding cancellation secret, as the store holds it.
/// </summary>
/// <remarks>
/// <b>Nothing here is a credential.</b> A booking's identifier, an instant and a flag name nobody
/// and unlock nothing: the hash a row is found by is not on this type, because a caller that already
/// holds the hash has no use for being told it again, and a type that carried it would invite being
/// logged. Presenting everything on this record to the cancellation flow cancels no booking.
/// </remarks>
public sealed record CancellationSecretRecord(Guid BookingId, DateTimeOffset ExpiresUtc, bool Redeemed);

/// <summary>
/// Stores the hashes of outstanding cancellation secrets. Implemented by UBookIt.Persistence.
/// </summary>
/// <remarks>
/// <para>
/// <b>No member of this port accepts or returns a secret</b> — only its hash, which is what the
/// package stores and what a redemption is looked up by. The plaintext lives in the message sent to
/// the booker and nowhere the package writes, so this port is the boundary that keeps it that way:
/// an implementation cannot persist what it is never given.
/// </para>
/// <para>
/// <b>Reading and redeeming are separate operations on purpose.</b> Retrieving the cancellation page
/// must change nothing — mail scanners fetch every link in a message unattended, and a retrieval
/// that consumed the booker's one use would spend it before they saw it. So the page reads through
/// <see cref="FindAsync"/>, and only the visitor's deliberate submission calls
/// <see cref="TryRedeemAsync"/>.
/// </para>
/// </remarks>
public interface ICancellationSecretStore
{
    /// <summary>Records a newly issued secret by its hash.</summary>
    /// <remarks>
    /// <paramref name="expiresUtc"/> is computed by the caller from the booking it belongs to and
    /// stored as an instant, so that later changes to the booking or to a site's configuration
    /// cannot silently extend or revoke a link already in somebody's inbox.
    /// </remarks>
    Task IssueAsync(
        Guid bookingId, string hash, DateTimeOffset expiresUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// The record for a hash, or <c>null</c> where there is none. <b>Changes nothing.</b>
    /// </summary>
    /// <remarks>
    /// Returns expired and already-redeemed records rather than hiding them as absent. The caller
    /// decides, and in this package every one of those conditions produces the same answer to the
    /// visitor — but that is a decision about what to *say*, taken in one place, not something to
    /// scatter across a store by returning null for some causes and a record for others.
    /// </remarks>
    Task<CancellationSecretRecord?> FindAsync(string hash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Redeems a secret, returning the booking it authorises, or <c>null</c> if it cannot be
    /// redeemed — expired, already redeemed, or never issued.
    /// </summary>
    /// <remarks>
    /// <b>This SHALL be atomic.</b> The redemption is recorded durably before the cancellation it
    /// authorises is reported as done, so two submissions arriving together cannot both win. A
    /// check followed by a separate write would be exactly the race that lets a single-use
    /// credential be used twice.
    /// </remarks>
    Task<Guid?> TryRedeemAsync(string hash, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
}
