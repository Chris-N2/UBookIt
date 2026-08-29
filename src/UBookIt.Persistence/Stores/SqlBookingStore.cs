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
            StartUtc = booking.Interval.StartUtc,
            EndUtc = booking.Interval.EndUtc,
            TimeZoneId = booking.Interval.TimeZoneId,
            Status = (int)booking.Status,
            CreatedUtc = booking.CreatedUtc,
            MemberKey = booking.Booker.MemberKey,
            BookerName = booking.Booker.Name,
            BookerEmail = booking.Booker.Email,
            BookerPhone = booking.Booker.Phone,
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
            BookingInterval.Create(row.StartUtc, row.EndUtc, row.TimeZoneId).Value,
            Booker.Create(row.MemberKey, row.BookerName, row.BookerEmail, row.BookerPhone).Value,
            row.Claims.Select(c => new ResourceClaim(c.ResourceId)),
            (BookingStatus)row.Status,
            row.CreatedUtc,
            BookingAttributionMapper.ToAttribution(row)).Value;
}
