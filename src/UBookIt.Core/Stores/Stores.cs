using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;

namespace UBookIt.Core.Stores;

/// <summary>A claim as seen by availability queries: which resource, which booking, when, and its status.</summary>
public sealed record ClaimInfo(Guid ResourceId, Guid BookingId, BookingInterval Interval, BookingStatus Status);

/// <summary>Read access to resources. Implemented by UBookIt.Persistence.</summary>
public interface IResourceStore
{
    Task<Resource?> GetAsync(Guid resourceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Booking storage. Implemented by UBookIt.Persistence.
/// </summary>
public interface IBookingStore
{
    /// <summary>
    /// All claims (any status) for the resource whose intervals overlap
    /// [fromUtc, toUtc). Callers filter by blocking status; the store does not
    /// interpret statuses.
    /// </summary>
    Task<IReadOnlyList<ClaimInfo>> GetClaimsAsync(
        Guid resourceId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a validated booking, atomically with respect to conflict
    /// detection. Contract (bookings spec, "Atomic placement contract"):
    /// between the conflict check and the persistence of a new booking's
    /// claims, no other placement for an overlapping interval on the same
    /// resource may succeed. Under concurrent placement of conflicting
    /// requests, exactly one succeeds and the others fail with code
    /// <see cref="FailureCodes.Conflict"/>. Conflicts are evaluated against
    /// claims of blocking bookings only (status Requested or Confirmed) using
    /// half-open interval overlap.
    /// </summary>
    Task<DomainResult<Booking>> PlaceAsync(Booking booking, CancellationToken cancellationToken = default);

    Task<Booking?> GetBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);

    /// <summary>Persists a status change to an existing booking.</summary>
    Task UpdateAsync(Booking booking, CancellationToken cancellationToken = default);
}
