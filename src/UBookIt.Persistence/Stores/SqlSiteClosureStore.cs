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
    internal static SiteClosure ToDomain(SiteClosureRow row)
        => SiteClosure.Create(row.Date, row.Label, row.Id).Value;

    internal static SiteClosureRow ToRow(SiteClosure closure)
        => new() { Id = closure.Id, Date = closure.Date, Label = closure.Label };
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
/// <b>Uniqueness is the index's job; the pre-check is for reportability.</b> Both are
/// present for the reason the booking reference has both: a check before writing is a
/// race, so the index is what guarantees one closure per date — but an unguarded race
/// would then surface as a database exception rather than as the stable
/// <see cref="FailureCodes.DuplicateClosureDate"/> code, so the violation is translated
/// rather than left to escape.
/// </remarks>
internal sealed class SqlSiteClosureManagementStore(UBookItDbContext db) : ISiteClosureManagementStore
{
    private const int SqlUniqueIndexViolation = 2601;

    private const int SqlUniqueConstraintViolation = 2627;

    public async Task<IReadOnlyList<SiteClosure>> ListAsync(
        DateOnly? from = null, CancellationToken cancellationToken = default)
    {
        var query = db.SiteClosures.AsNoTracking();

        if (from is { } earliest)
        {
            // Filtered here rather than by the client hiding rows, so the view's default
            // does not depend on fetching every closure a site has ever recorded.
            query = query.Where(c => c.Date >= earliest);
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
        var taken = await db.SiteClosures
            .AnyAsync(c => c.Date == closure.Date, cancellationToken)
            .ConfigureAwait(false);

        if (taken)
        {
            return DuplicateDate(closure.Date);
        }

        db.SiteClosures.Add(SiteClosureRowMapper.ToRow(closure));

        return await SaveTranslatingDuplicateAsync(closure, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DomainResult<SiteClosure>> UpdateAsync(
        SiteClosure closure, CancellationToken cancellationToken = default)
    {
        var row = await db.SiteClosures
            .FirstOrDefaultAsync(c => c.Id == closure.Id, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return DomainResult<SiteClosure>.Failure(
                FailureCodes.ClosureNotFound, $"No closure exists with id {closure.Id}.");
        }

        var taken = await db.SiteClosures
            .AnyAsync(c => c.Date == closure.Date && c.Id != closure.Id, cancellationToken)
            .ConfigureAwait(false);

        if (taken)
        {
            return DuplicateDate(closure.Date);
        }

        // The id is kept, which is what carries a resource's exemption with the closure
        // when its date moves.
        row.Date = closure.Date;
        row.Label = closure.Label;

        return await SaveTranslatingDuplicateAsync(closure, cancellationToken).ConfigureAwait(false);
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

    private static DomainResult<SiteClosure> DuplicateDate(DateOnly date)
        => DomainResult<SiteClosure>.Failure(
            FailureCodes.DuplicateClosureDate,
            $"A closure already exists for {date:yyyy-MM-dd}.",
            nameof(SiteClosure.Date));

    private async Task<DomainResult<SiteClosure>> SaveTranslatingDuplicateAsync(
        SiteClosure closure, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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
