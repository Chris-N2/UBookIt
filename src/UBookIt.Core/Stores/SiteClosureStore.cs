using UBookIt.Core.Availability;
using UBookIt.Core.Common;

namespace UBookIt.Core.Stores;

/// <summary>
/// Read access to the site's closure dates. Implemented by UBookIt.Persistence.
/// </summary>
/// <remarks>
/// <b>A port of its own rather than members on <see cref="IResourceStore"/>.</b> That
/// interface is published and frozen from 17.0.0, and a host implementing it would be
/// broken by a new member; closures being a separate port means an existing host keeps
/// compiling and keeps working, with the one consequence stated below.
/// <para>
/// <b>What a host implementing <see cref="IResourceStore"/> owns.</b> Closures reach a
/// resource when it is hydrated, so a host supplying its own resource store is
/// responsible for applying them to the configurations it builds — exactly as it is
/// already responsible for that resource's open hours and exceptions. The shipped store
/// does it; nothing in the domain can do it on a host's behalf, because the domain never
/// loads a resource.
/// </para>
/// <para>
/// <b>Nothing here is personal data.</b> A closure is a date and the site's own name for
/// it; an opt-out is two identifiers. No booker's details reach either, so erasure has no
/// business with this port.
/// </para>
/// </remarks>
public interface ISiteClosureStore
{
    /// <summary>
    /// Every closure the site has defined, in a deterministic order.
    /// </summary>
    /// <remarks>
    /// Unpaged and unfiltered, for the same reason
    /// <see cref="IResourceStore.ListByTypeAsync"/> is: its consumer is availability
    /// hydration, and a truncated set does not error — it opens a date the site said was
    /// closed. The management surface does its own filtering for display.
    /// </remarks>
    Task<IReadOnlyList<SiteClosure>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Management writes for the site's closure dates. Implemented by UBookIt.Persistence.
/// </summary>
/// <remarks>
/// Separate from <see cref="ISiteClosureStore"/> on the same grounds that separate every
/// other read port here from its management counterpart: the booking path's reads must not
/// depend on the surface that writes.
/// </remarks>
public interface ISiteClosureManagementStore
{
    /// <summary>
    /// Closures in a deterministic order, optionally limited to those on or after
    /// <paramref name="from"/>.
    /// </summary>
    /// <remarks>
    /// The filter is served here rather than by the client hiding rows, so that the view's
    /// default does not depend on fetching every closure a site has ever recorded.
    /// </remarks>
    Task<IReadOnlyList<SiteClosure>> ListAsync(DateOnly? from = null, CancellationToken cancellationToken = default);

    Task<SiteClosure?> GetAsync(Guid closureId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a new closure. Fails with <see cref="FailureCodes.DuplicateClosureDate"/> when
    /// the date already carries one — reported from the unique index rather than from a check
    /// before writing, which is a race.
    /// </summary>
    Task<DomainResult<SiteClosure>> CreateAsync(SiteClosure closure, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces a closure's date and label, keeping its id — so that resources exempted from
    /// it stay exempted from it. Fails with <see cref="FailureCodes.ClosureNotFound"/> when no
    /// closure carries the id, and <see cref="FailureCodes.DuplicateClosureDate"/> when the new
    /// date is already taken by another.
    /// </summary>
    Task<DomainResult<SiteClosure>> UpdateAsync(SiteClosure closure, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a closure and, with it, every opt-out naming it. Fails with
    /// <see cref="FailureCodes.ClosureNotFound"/> when no closure carries the id.
    /// </summary>
    Task<DomainResult> DeleteAsync(Guid closureId, CancellationToken cancellationToken = default);
}
