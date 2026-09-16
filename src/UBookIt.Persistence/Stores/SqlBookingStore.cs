using Microsoft.EntityFrameworkCore;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Entities;

namespace UBookIt.Persistence.Stores;

/// <summary>
/// SQL Server implementation of <see cref="IBookingStore"/>. Placement honours
/// the atomic-placement contract via a per-resource application lock inside
/// the placement transaction (design D5).
/// </summary>
internal sealed class SqlBookingStore(UBookItDbContext db) : IBookingStore
{
    private static readonly int[] BlockingStatuses =
    [
        (int)BookingStatus.Requested,
        (int)BookingStatus.Confirmed,
    ];

    public async Task<IReadOnlyList<ClaimInfo>> GetClaimsAsync(
        Guid resourceId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        var rows = await (
            from claim in db.Claims.AsNoTracking()
            join booking in db.Bookings.AsNoTracking() on claim.BookingId equals booking.Id
            where claim.ResourceId == resourceId
                && booking.StartUtc < toUtc
                && fromUtc < booking.EndUtc
            select new { booking.Id, booking.StartUtc, booking.EndUtc, booking.TimeZoneId, booking.Status })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(r => new ClaimInfo(
                resourceId,
                r.Id,
                BookingInterval.Create(r.StartUtc, r.EndUtc, r.TimeZoneId).Value,
                (BookingStatus)r.Status))
            .ToList();
    }

    public async Task<IReadOnlyList<ClaimInfo>> GetClaimsAsync(
        IReadOnlyCollection<Guid> resourceIds, DateTimeOffset fromUtc, DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        if (resourceIds.Count == 0)
        {
            return [];
        }

        // One query over the same interval index as the single-resource read,
        // not a loop over it (book-via-service design D5). The claim's own
        // resource id is projected because a single booking may claim several
        // of the queried resources.
        var ids = resourceIds.ToArray();

        var rows = await (
            from claim in db.Claims.AsNoTracking()
            join booking in db.Bookings.AsNoTracking() on claim.BookingId equals booking.Id
            where ids.Contains(claim.ResourceId)
                && booking.StartUtc < toUtc
                && fromUtc < booking.EndUtc
            select new
            {
                claim.ResourceId,
                booking.Id,
                booking.StartUtc,
                booking.EndUtc,
                booking.TimeZoneId,
                booking.Status,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(r => new ClaimInfo(
                r.ResourceId,
                r.Id,
                BookingInterval.Create(r.StartUtc, r.EndUtc, r.TimeZoneId).Value,
                (BookingStatus)r.Status))
            .ToList();
    }

    public async Task<DomainResult<Booking>> PlaceAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        // Ascending lock order is deadlock-proof for future multi-claim bookings.
        var resourceIds = booking.Claims.Select(c => c.ResourceId).Order().ToArray();

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        foreach (var resourceId in resourceIds)
        {
            await AcquireResourceLockAsync(resourceId, cancellationToken).ConfigureAwait(false);
        }

        var hasConflict = await (
            from claim in db.Claims
            join existing in db.Bookings on claim.BookingId equals existing.Id
            where resourceIds.Contains(claim.ResourceId)
                && BlockingStatuses.Contains(existing.Status)
                && existing.StartUtc < booking.Interval.EndUtc
                && booking.Interval.StartUtc < existing.EndUtc
            select claim.Id)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);

        if (hasConflict)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return DomainResult<Booking>.Failure(
                FailureCodes.Conflict, "The requested interval conflicts with an existing booking.");
        }

        // The unique index is what GUARANTEES reference uniqueness; this check is what makes
        // the ordinary case reportable, so the service can generate another reference instead
        // of the whole placement dying on a database exception. Inside the same transaction as
        // the insert, so it cannot be defeated by anything except a genuine race — and a race
        // still loses to the index, which is the point of having it.
        var referenceTaken = await db.Bookings
            .AnyAsync(b => b.Reference == booking.Reference.Value, cancellationToken)
            .ConfigureAwait(false);

        if (referenceTaken)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return DomainResult<Booking>.Failure(
                FailureCodes.ReferenceTaken, "That booking reference is already in use.");
        }

        db.Bookings.Add(ToRow(booking));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return DomainResult<Booking>.Success(booking);
    }

    public async Task<Booking?> GetBookingAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var row = await db.Bookings
            .AsNoTracking()
            .Include(b => b.Claims)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : ToDomain(row);
    }

    public async Task UpdateAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        var row = await db.Bookings
            .FirstAsync(b => b.Id == booking.Id, cancellationToken)
            .ConfigureAwait(false);

        // The STATUS, and nothing else — see IBookingStore.UpdateAsync.
        //
        // This method briefly wrote the booker too, to stop erasure being a silent no-op
        // through it. That fix produced two further defects in as many reviews: first a
        // cancellation restoring a person somebody had erased in between, then, once the
        // booker columns were guarded, an ERASURE reverting a committed cancellation and
        // re-blocking a slot that had been released. Both came from one method writing
        // columns its caller had not changed.
        //
        // Erasure now has its own write over its own columns, so the two cannot collide and
        // neither needs to defend against the other.
        row.Status = (int)booking.Status;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<DomainResult> MoveAsync(
        Guid bookingId,
        BookingInterval newInterval,
        IReadOnlyCollection<BookingStatus> permittedFrom,
        CancellationToken cancellationToken = default)
    {
        // THE FOURTH NARROW WRITE. Placement's transaction shape, with three differences that
        // are the whole of the move contract (persistence spec, "Atomic move on SQL Server"):
        // the lock set is read from the STORED claims, the conflict check excludes this
        // booking's own claims, and the status is a predicate of the update statement itself.
        var permitted = permittedFrom.Select(status => (int)status).ToArray();

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // The stored claims, not the caller's aggregate: the lock set must be what the booking
        // actually holds. Claims are immutable after placement, so there is no race on this read.
        var resourceIds = await db.Claims
            .AsNoTracking()
            .Where(c => c.BookingId == bookingId)
            .Select(c => c.ResourceId)
            .OrderBy(id => id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        if (resourceIds.Length == 0)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return DomainResult.Failure(FailureCodes.BookingNotFound, $"No booking exists with id {bookingId}.");
        }

        // Ascending, exactly as placement takes them, so a move and a placement sharing a
        // resource — or two moves — cannot deadlock.
        foreach (var resourceId in resourceIds)
        {
            await AcquireResourceLockAsync(resourceId, cancellationToken).ConfigureAwait(false);
        }

        // Placement's conflict query plus ONE predicate: not this booking. Its own claim rows
        // overlap its own new interval whenever the intervals overlap, and without this a
        // thirty-minute shift would refuse itself.
        var hasConflict = await (
            from claim in db.Claims
            join existing in db.Bookings on claim.BookingId equals existing.Id
            where resourceIds.Contains(claim.ResourceId)
                && existing.Id != bookingId
                && BlockingStatuses.Contains(existing.Status)
                && existing.StartUtc < newInterval.EndUtc
                && newInterval.StartUtc < existing.EndUtc
            select claim.Id)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);

        if (hasConflict)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return DomainResult.Failure(
                FailureCodes.Conflict, "The requested interval conflicts with an existing booking.");
        }

        // ONE STATEMENT, AND THE STATUS TEST IS INSIDE IT. The service read this booking some
        // time ago and found it movable; a cancellation may have committed since. A SELECT here
        // followed by an UPDATE would leave that window open — the same shape erasure rejected
        // — so the permitted statuses are the WHERE clause, and zero rows affected means the
        // status moved on. The interval columns and nothing else: not the status, not the
        // booker.
        var affected = await db.Bookings
            .Where(b => b.Id == bookingId && permitted.Contains(b.Status))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(b => b.StartUtc, newInterval.StartUtc)
                    .SetProperty(b => b.EndUtc, newInterval.EndUtc)
                    .SetProperty(b => b.TimeZoneId, newInterval.TimeZoneId),
                cancellationToken)
            .ConfigureAwait(false);

        if (affected == 0)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);

            // The claims existed, so the booking does; the status is what refused it.
            return DomainResult.Failure(
                FailureCodes.InvalidStatusTransition, "The booking's status no longer permits a move.");
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return DomainResult.Success();
    }

    public async Task<bool> EraseBookerAsync(
        Guid bookingId, DateTimeOffset erasedUtc, CancellationToken cancellationToken = default)
    {
        // ONE STATEMENT, AND THE TEST FOR "ALREADY ERASED" IS INSIDE IT.
        //
        // Each booker column is set to a CASE over the row's own pre-update BookerErasedUtc,
        // which SQL Server evaluates against the row as it stands when it takes the lock. A
        // row that already records an erasure keeps every value it has, including the FIRST
        // instant — the one a data subject was told.
        //
        // Not a SELECT-then-decide-then-UPDATE. That shape was tried and rejected in review:
        // the erasure can commit in the window between the two statements, and the UPDATE
        // then restores the person having already decided it would not. It does not make the
        // race smaller in any way that matters, and it argues for a row-level guarantee while
        // implementing it in application code, which cannot deliver one.
        //
        // Takes an id and an instant rather than an aggregate, so there is no stale copy of
        // anything to write back and no column outside the booker can be touched. That is why
        // this does not disturb a concurrent cancellation.
        var affected = await db.Bookings
            .Where(b => b.Id == bookingId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(b => b.MemberKey, b => b.BookerErasedUtc == null ? null : b.MemberKey)
                    .SetProperty(b => b.BookerName, b => b.BookerErasedUtc == null ? null : b.BookerName)
                    .SetProperty(b => b.BookerEmail, b => b.BookerErasedUtc == null ? null : b.BookerEmail)
                    .SetProperty(b => b.BookerPhone, b => b.BookerErasedUtc == null ? null : b.BookerPhone)
                    .SetProperty(
                        b => b.BookerErasedUtc,
                        b => b.BookerErasedUtc == null ? erasedUtc : b.BookerErasedUtc),
                cancellationToken)
            .ConfigureAwait(false);

        // Reports existence, not change. Erasing an already-erased booking matches a row and
        // writes nothing, and that is a success — the caller asked for the booking to be
        // erased and it is.
        return affected > 0;
    }

    public async Task<IReadOnlyList<Guid>> GetBookingIdsDueForErasureAsync(
        DateTimeOffset cutoffUtc, int take, CancellationToken cancellationToken = default)
    {
        // Projects to the id BEFORE anything materialises, so no booker column is ever read out
        // of the database on this path — see IBookingStore for why that is the requirement and
        // not an optimisation.
        //
        // The predicate is the filtered index's definition, deliberately: EndUtc keyed, and
        // BookerErasedUtc IS NULL as the filter. Written in the same order so that a change to one
        // is visibly a change to the other.
        //
        // No status filter. Status-blindness is the requirement.
        return await db.Bookings
            .AsNoTracking()
            .Where(b => b.EndUtc < cutoffUtc && b.BookerErasedUtc == null)
            .OrderBy(b => b.EndUtc)
            .ThenBy(b => b.Id)
            .Take(take)
            .Select(b => b.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Transaction-owned exclusive app lock scoped to one resource's calendar.
    /// Released automatically at commit/rollback.
    /// </summary>
    private Task AcquireResourceLockAsync(Guid resourceId, CancellationToken cancellationToken)
        => AppLock.AcquireAsync(db, AppLock.ForResourcePlacement(resourceId), cancellationToken);

    private static BookingRow ToRow(Booking booking)
    {
        // Destructured once, so "written from one source" is what the code does rather
        // than what its comment says. Calling the mapper per column was harmless — it is
        // pure over the same value — but it read as two independent derivations of a pair
        // that must agree.
        var (serviceId, serviceName) = BookingAttributionMapper.ToColumns(booking.Service);

        return new BookingRow
        {
            Id = booking.Id,
            Reference = booking.Reference.Value,
            StartUtc = booking.Interval.StartUtc,
            EndUtc = booking.Interval.EndUtc,
            TimeZoneId = booking.Interval.TimeZoneId,
            Status = (int)booking.Status,
            CreatedUtc = booking.CreatedUtc,
            MemberKey = booking.Booker.MemberKey,
            BookerName = booking.Booker.Contact?.Name,
            BookerEmail = booking.Booker.Contact?.Email,
            BookerPhone = booking.Booker.Contact?.Phone,
            BookerErasedUtc = booking.Booker.ErasedUtc,
            ServiceId = serviceId,
            ServiceName = serviceName,
            Claims = booking.Claims
                .Select(c => new ClaimRow { BookingId = booking.Id, ResourceId = c.ResourceId })
                .ToList(),
        };
    }

    private static Booking ToDomain(BookingRow row)
        => Booking.Rehydrate(
            row.Id,
            BookingReference.FromCanonical(row.Reference),
            BookingInterval.Create(row.StartUtc, row.EndUtc, row.TimeZoneId).Value,
            ToBooker(row),
            row.Claims.Select(c => new ResourceClaim(c.ResourceId)),
            (BookingStatus)row.Status,
            row.CreatedUtc,
            BookingAttributionMapper.ToAttribution(row)).Value;

    /// <summary>
    /// The stored booker, in whichever of its two states the row holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The erasure column decides, not the absence of a name.</b> Reading "erased" off a
    /// NULL name would be inferring the state from missing data — which is the reading the
    /// schema stores <c>BookerErasedUtc</c> precisely to avoid, and which would silently
    /// convert a row corrupted by some other means into a lawful erasure.
    /// </para>
    /// <para>
    /// <c>Booker.Create</c> is used for the unerased case exactly as before, and its result
    /// is taken directly: what is stored is historical fact, on the same terms as the stored
    /// status, and a row that failed validation here would make a booking unreadable for
    /// having once been valid.
    /// </para>
    /// </remarks>
    private static Booker ToBooker(BookingRow row)
        => row.BookerErasedUtc is { } erasedUtc
            ? Booker.Erased(erasedUtc)
            : Booker.Create(row.MemberKey, row.BookerName, row.BookerEmail, row.BookerPhone).Value;
}
