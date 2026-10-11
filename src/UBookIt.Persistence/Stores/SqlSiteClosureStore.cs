using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using UBookIt.Core.Availability;
using UBookIt.Core.Common;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Entities;

namespace UBookIt.Persistence.Stores;

/// <summary>Maps between <see cref="SiteClosure"/> and its row.</summary>
internal static class SiteClosureRowMapper
{
    /// <summary>
    /// Re-passes stored data through the Core factory, so a corrupted row surfaces as an
    /// exception rather than as a silently-wrong domain object — the same arrangement
    /// <see cref="ResourceRowMapper"/> has.
    /// </summary>
    /// <remarks>
    /// <b>One corruption is tolerated rather than thrown: an end before the start.</b> A site that
    /// goes back to a version from before closures were ranges keeps the <c>EndDate</c> column, and
    /// that version writes closures without it — so a closure it creates or moves gets the column's
    /// default, 0001-01-01. Coming forward again, such a row would otherwise throw here, and
    /// because every resource read loads the closures, take every booking page and the backoffice
    /// down with it. It is read as the single day it was written as. Anything else wrong with a row
    /// still throws.
    /// </remarks>
    internal static SiteClosure ToDomain(SiteClosureRow row)
        => SiteClosure.CreateRange(
            row.Date, row.EndDate < row.Date ? row.Date : row.EndDate, row.Label, row.Id).Value;

    internal static SiteClosureRow ToRow(SiteClosure closure)
        => new() { Id = closure.Id, Date = closure.Date, EndDate = closure.EndDate, Label = closure.Label };
}

/// <summary>
/// SQL Server implementation of <see cref="ISiteClosureStore"/>: the read the
/// availability path takes.
/// </summary>
internal sealed class SqlSiteClosureStore(UBookItDbContext db) : ISiteClosureStore
{
    public async Task<IReadOnlyList<SiteClosure>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.SiteClosures
            .AsNoTracking()
            .OrderBy(c => c.Date)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(SiteClosureRowMapper.ToDomain).ToList();
    }
}

/// <summary>
/// SQL Server implementation of <see cref="ISiteClosureManagementStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>No date is covered twice, and that is guaranteed by a lock, not an index.</b> SQL
/// Server has no exclusion constraint for ranges, so every write that could change
/// coverage — create, update, and the import, which creates — takes one site-wide
/// application lock inside its transaction and checks for an overlapping closure while
/// holding it. A check before writing is otherwise a race; under the lock it is not.
/// </para>
/// <para>
/// <b>The unique index on the start date stays, as a backstop.</b> With overlaps refused,
/// two closures can never share a start, so the index can never reject a write the lock
/// let through — but if it ever did, the violation is translated to the stable
/// <see cref="FailureCodes.DuplicateClosureDate"/> code rather than escaping as a database
/// exception.
/// </para>
/// </remarks>
internal sealed class SqlSiteClosureManagementStore(UBookItDbContext db) : ISiteClosureManagementStore
{
    private const int SqlUniqueIndexViolation = 2601;

    private const int SqlUniqueConstraintViolation = 2627;

    /// <summary>
    /// One name for every closure write: overlap is a property of the whole list, so
    /// writers of different closures must still serialise against each other.
    /// </summary>
    private const string ClosureWriteLock = "ubookit:site-closures";

    public async Task<IReadOnlyList<SiteClosure>> ListAsync(
        DateOnly? from = null, CancellationToken cancellationToken = default)
    {
        var query = db.SiteClosures.AsNoTracking();

        if (from is { } earliest)
        {
            // Filtered here rather than by the client hiding rows, so the view's default
            // does not depend on fetching every closure a site has ever recorded. By END
            // date, so a closure already in progress is still upcoming — the end as READ,
            // corrected for a row an older version wrote without one (see ToDomain), or a
            // future closure written while downgraded would vanish from the default list.
            query = query.Where(c => (c.EndDate < c.Date ? c.Date : c.EndDate) >= earliest);
        }

        var rows = await query
            .OrderBy(c => c.Date)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(SiteClosureRowMapper.ToDomain).ToList();
    }

    public async Task<SiteClosure?> GetAsync(Guid closureId, CancellationToken cancellationToken = default)
    {
        var row = await db.SiteClosures
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == closureId, cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : SiteClosureRowMapper.ToDomain(row);
    }

    public async Task<DomainResult<SiteClosure>> CreateAsync(
        SiteClosure closure, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await AppLock.AcquireAsync(db, ClosureWriteLock, cancellationToken).ConfigureAwait(false);

        if (await OverlapsAnotherAsync(closure, cancellationToken).ConfigureAwait(false) is { } clash)
        {
            return clash;
        }

        db.SiteClosures.Add(SiteClosureRowMapper.ToRow(closure));

        return await SaveTranslatingDuplicateAsync(closure, transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DomainResult<SiteClosure>> UpdateAsync(
        SiteClosure closure, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await AppLock.AcquireAsync(db, ClosureWriteLock, cancellationToken).ConfigureAwait(false);

        var row = await db.SiteClosures
            .FirstOrDefaultAsync(c => c.Id == closure.Id, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return DomainResult<SiteClosure>.Failure(
                FailureCodes.ClosureNotFound, $"No closure exists with id {closure.Id}.");
        }

        // Its own previous range is excluded: extending a closure is not an overlap with itself.
        if (await OverlapsAnotherAsync(closure, cancellationToken).ConfigureAwait(false) is { } clash)
        {
            return clash;
        }

        // The id is kept, which is what carries a resource's exemption with the closure
        // when its dates move.
        row.Date = closure.Date;
        row.EndDate = closure.EndDate;
        row.Label = closure.Label;

        return await SaveTranslatingDuplicateAsync(closure, transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DomainResult> DeleteAsync(Guid closureId, CancellationToken cancellationToken = default)
    {
        // The opt-out rows go with it by cascade, declared in the model rather than deleted
        // here: an exemption cannot outlive the thing it exempts from, and a rule enforced by
        // the schema cannot be forgotten by a second delete path.
        var deleted = await db.SiteClosures
            .Where(c => c.Id == closureId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return deleted == 0
            ? DomainResult.Failure(FailureCodes.ClosureNotFound, $"No closure exists with id {closureId}.")
            : DomainResult.Success();
    }

    /// <summary>
    /// The refusal for a closure sharing any date with another, or null when none does.
    /// Must run under <see cref="ClosureWriteLock"/>.
    /// </summary>
    private async Task<DomainResult<SiteClosure>?> OverlapsAnotherAsync(
        SiteClosure closure, CancellationToken cancellationToken)
    {
        var other = await db.SiteClosures
            .AsNoTracking()
            // The other closure's end as READ (see ToDomain): a row an older version wrote
            // without an end still covers its start date, and must still refuse an overlap.
            .Where(c => c.Id != closure.Id
                && c.Date <= closure.EndDate
                && (c.EndDate < c.Date ? c.Date : c.EndDate) >= closure.Date)
            .OrderBy(c => c.Date)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (other is null)
        {
            return null;
        }

        var shared = other.Date > closure.Date ? other.Date : closure.Date;

        return DomainResult<SiteClosure>.Failure(
            FailureCodes.DuplicateClosureDate,
            $"Another closure (\"{other.Label}\") already covers {shared:yyyy-MM-dd}.",
            nameof(SiteClosure.Date));
    }

    private static DomainResult<SiteClosure> DuplicateDate(DateOnly date)
        => DomainResult<SiteClosure>.Failure(
            FailureCodes.DuplicateClosureDate,
            $"A closure already exists for {date:yyyy-MM-dd}.",
            nameof(SiteClosure.Date));

    private async Task<DomainResult<SiteClosure>> SaveTranslatingDuplicateAsync(
        SiteClosure closure,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return DomainResult<SiteClosure>.Success(closure);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql
            && sql.Number is SqlUniqueIndexViolation or SqlUniqueConstraintViolation)
        {
            // The race the pre-check above cannot close. Reported with the same code the
            // ordinary case reports, so a caller never has to tell one from the other.
            db.ChangeTracker.Clear();
            return DuplicateDate(closure.Date);
        }
    }
}
