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

    /// <summary>
    /// A page of resources plus the unpaged total, for public read/discovery.
    /// A read-only projection over the same aggregates as the management store,
    /// kept on the read port so anonymous callers never depend on the
    /// management (write) surface.
    /// </summary>
    Task<ResourcePage> ListAsync(int skip, int take, CancellationToken cancellationToken = default);
}

/// <summary>One page of resources plus the unpaged total.</summary>
public sealed record ResourcePage(IReadOnlyList<Resource> Items, int Total);

/// <summary>
/// Management writes for resources. Implemented by UBookIt.Persistence.
/// Accepts only <see cref="Resource"/> aggregates — which are constructible
/// solely via the validating Core factories — so the store persists only
/// validated state. Availability updates replace the resource's configuration
/// wholesale within one transaction (write-path exception-date uniqueness).
/// </summary>
public interface IResourceManagementStore
{
    Task<DomainResult<Resource>> CreateAsync(Resource resource, CancellationToken cancellationToken = default);

    /// <summary>Full update; fails with <see cref="FailureCodes.ResourceNotFound"/> for unknown ids.</summary>
    Task<DomainResult<Resource>> UpdateAsync(Resource resource, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fails with <see cref="FailureCodes.ResourceInUse"/> when the resource
    /// has any booking claims (including a claim placed concurrently with the
    /// delete), and <see cref="FailureCodes.ResourceNotFound"/> for unknown ids.
    /// </summary>
    Task<DomainResult> DeleteAsync(Guid resourceId, CancellationToken cancellationToken = default);

    Task<ResourcePage> ListAsync(int skip, int take, CancellationToken cancellationToken = default);
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
