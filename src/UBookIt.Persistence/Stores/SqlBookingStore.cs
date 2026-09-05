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

        // Every part of a booking the domain permits to change after placement is written
        // here — the status AND the booker. Writing only the status was correct while a
        // status change was the only mutation there was, and it silently stopped being
        // correct the moment erasure existed: the aggregate would carry the erasure, the
        // call would report success, and the row would keep the person's details. Nothing
        // asserting against the returned Booking could see it, which is why the covering
        // test re-reads from storage.
        row.Status = (int)booking.Status;

        // THE STORED ERASURE WINS, ALWAYS.
        //
        // Every caller here does read-modify-write with no re-read and no concurrency token,
        // so an aggregate can be older than the row it is about to overwrite. That is
        // harmless for the status — a stale status write loses a transition, which the
        // status machine already refuses on the next attempt — and it is catastrophic for
        // the booker, because the stale value is a person's name and the fresh one is their
        // absence:
        //
        //   1. an operator opens cancel; CancelAsync loads the booking, details and all
        //   2. a second operator erases it; the columns go NULL and the instant is set
        //   3. the cancel completes, writing "Ada Lovelace" back over the NULLs
        //
        // Two ordinary requests, both reporting success, and the data subject who was told
        // their details were gone is back in the database. `docs/backoffice.md` says "It
        // cannot be undone. There is no restore" — this is what makes that true rather than
        // aspirational.
        //
        // Enforced HERE rather than by a concurrency token, because the guarantee is not
        // "detect a conflicting write" but "erasure is absorbing": once the row records an
        // erasure, no later write may put a person back into it, stale or not. A rowversion
        // would turn this into an error for the cancelling operator to retry; absorbing it
        // needs nobody to do anything. And it is one write path, which the persistence
        // capability requires — a separate erase-only method would be a second route to the
        // same row, free to disagree with this one.
        //
        // Re-erasing an already-erased booking therefore skips these columns entirely: the
        // row already holds the right values, including the FIRST erasure's instant, which
        // is the one that must survive.
        if (row.BookerErasedUtc is null)
        {
            row.MemberKey = booking.Booker.MemberKey;
            row.BookerName = booking.Booker.Contact?.Name;
            row.BookerEmail = booking.Booker.Contact?.Email;
            row.BookerPhone = booking.Booker.Contact?.Phone;
            row.BookerErasedUtc = booking.Booker.ErasedUtc;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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
