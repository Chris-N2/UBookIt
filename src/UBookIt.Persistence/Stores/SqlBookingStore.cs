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
        var status = (int)booking.Status;
        var memberKey = booking.Booker.MemberKey;
        var name = booking.Booker.Contact?.Name;
        var email = booking.Booker.Contact?.Email;
        var phone = booking.Booker.Contact?.Phone;
        var erasedUtc = booking.Booker.ErasedUtc;

        // Every part of a booking the domain permits to change after placement is written
        // here — the status AND the booker. Writing only the status was correct while a
        // status change was the only mutation there was, and it silently stopped being
        // correct the moment erasure existed: the aggregate carried the erasure, the call
        // reported success, and the row kept the person's details.
        //
        // THE STORED ERASURE WINS, ALWAYS — AND THE TEST FOR IT IS PART OF THE WRITE.
        //
        // Callers do read-modify-write with no re-read and the row has no concurrency token,
        // so an aggregate can be older than the row it is about to overwrite. Harmless for
        // the status — a lost transition is refused on the next attempt — and catastrophic
        // for the booker, because the stale value is a person's name and the fresh one is
        // their absence: an operator who opened a cancellation before a colleague erased the
        // booking would, on completing it, write the name back over the NULLs.
        //
        // A previous version of this method tested `row.BookerErasedUtc` after a SELECT and
        // applied the decision in a later UPDATE. That is check-then-act: the erasure can
        // land in the window between the two statements, and the UPDATE then restores the
        // person having already decided it would not. It narrowed the race from "however long
        // an operator spends on a confirmation dialog" to "one round trip" and left it open —
        // and, worse, it argued in a comment that absorption is a property of the ROW while
        // implementing it in application code, which cannot deliver a row-level property.
        //
        // So the predicate lives INSIDE the statement. Every booker column is set to a CASE
        // over the row's own pre-update `BookerErasedUtc`, evaluated by the server against the
        // row it is locking. There is no window, and no interleaving of any two callers can
        // put a person back into an erased row. The status is set unconditionally alongside
        // them, in the same statement, so cancelling an erased booking still cancels it.
        //
        // Still exactly one write path, which the persistence capability requires: one
        // method, and now one statement. A separate erase-only method would be a second route
        // to the same row, free to disagree with this one.
        //
        // Re-erasing an already-erased booking therefore leaves the booker columns exactly as
        // they are, including the FIRST erasure's instant — the one that must survive.
        var affected = await db.Bookings
            .Where(b => b.Id == booking.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(b => b.Status, status)
                    .SetProperty(b => b.MemberKey, b => b.BookerErasedUtc == null ? memberKey : b.MemberKey)
                    .SetProperty(b => b.BookerName, b => b.BookerErasedUtc == null ? name : b.BookerName)
                    .SetProperty(b => b.BookerEmail, b => b.BookerErasedUtc == null ? email : b.BookerEmail)
                    .SetProperty(b => b.BookerPhone, b => b.BookerErasedUtc == null ? phone : b.BookerPhone)
                    .SetProperty(
                        b => b.BookerErasedUtc,
                        b => b.BookerErasedUtc == null ? erasedUtc : b.BookerErasedUtc),
                cancellationToken)
            .ConfigureAwait(false);

        // The previous implementation used `FirstAsync`, which threw when no row matched.
        // `ExecuteUpdateAsync` reports a count instead, so the same contract is kept
        // explicitly rather than lost in the change of mechanism: a caller that updates a
        // booking which is not there has made a mistake and should hear about it, not receive
        // a silent success.
        if (affected == 0)
        {
            throw new InvalidOperationException(
                $"No booking exists with id {booking.Id}; nothing was updated.");
        }
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
