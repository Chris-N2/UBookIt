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

        row.Status = (int)booking.Status;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Transaction-owned exclusive app lock scoped to one resource's calendar.
    /// Released automatically at commit/rollback.
    /// </summary>
    private Task AcquireResourceLockAsync(Guid resourceId, CancellationToken cancellationToken)
        => AppLock.AcquireAsync(db, AppLock.ForResourcePlacement(resourceId), cancellationToken);

    private static BookingRow ToRow(Booking booking) => new()
    {
        Id = booking.Id,
        StartUtc = booking.Interval.StartUtc,
        EndUtc = booking.Interval.EndUtc,
        TimeZoneId = booking.Interval.TimeZoneId,
        Status = (int)booking.Status,
        CreatedUtc = booking.CreatedUtc,
        MemberKey = booking.Booker.MemberKey,
        BookerName = booking.Booker.Name,
        BookerEmail = booking.Booker.Email,
        BookerPhone = booking.Booker.Phone,
        Claims = booking.Claims.Select(c => new ClaimRow { BookingId = booking.Id, ResourceId = c.ResourceId }).ToList(),
    };

    private static Booking ToDomain(BookingRow row)
        => Booking.Rehydrate(
            row.Id,
            BookingInterval.Create(row.StartUtc, row.EndUtc, row.TimeZoneId).Value,
            Booker.Create(row.MemberKey, row.BookerName, row.BookerEmail, row.BookerPhone).Value,
            row.Claims.Select(c => new ResourceClaim(c.ResourceId)),
            (BookingStatus)row.Status,
            row.CreatedUtc).Value;
}
