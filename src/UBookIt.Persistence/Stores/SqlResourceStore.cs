using Microsoft.EntityFrameworkCore;
using UBookIt.Core.Resources;
using UBookIt.Core.Stores;

namespace UBookIt.Persistence.Stores;

/// <summary>
/// SQL Server implementation of the read port <see cref="IResourceStore"/>
/// (resource loads and public paged discovery). Resource writes live on the
/// separate management store.
/// </summary>
/// <remarks>
/// <b>Every read here hydrates the site closures that apply to the resource</b>, so a
/// resource handed to any availability computation already carries them — including the
/// pure projection, which takes no closure argument and could not be given one without
/// widening a signature frozen at 17.0.0.
/// <para>
/// The closure set is read <b>once per call</b>, never once per resource: the consumer of
/// <see cref="ListByTypeAsync"/> is a service's candidate pool, and a per-resource read
/// there turns one availability question into an N+1.
/// </para>
/// </remarks>
internal sealed class SqlResourceStore(UBookItDbContext db, ISiteClosureStore closures) : IResourceStore
{
    public async Task<Resource?> GetAsync(Guid resourceId, CancellationToken cancellationToken = default)
    {
        var row = await db.Resources
            .AsNoTracking()
            .Include(r => r.OpenHours)
            .Include(r => r.Exceptions)
            .Include(r => r.Capabilities)
            .Include(r => r.ClosureOptOuts)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == resourceId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        var siteClosures = await closures.ListAsync(cancellationToken).ConfigureAwait(false);

        return ResourceRowMapper.ToDomain(row, siteClosures);
    }

    public async Task<ResourcePage> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 0, 500);

        var total = await db.Resources.CountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await db.Resources
            .AsNoTracking()
            .Include(r => r.OpenHours)
            .Include(r => r.Exceptions)
            .Include(r => r.Capabilities)
            .Include(r => r.ClosureOptOuts)
            .AsSplitQuery()
            .OrderBy(r => r.DisplayName).ThenBy(r => r.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var siteClosures = await closures.ListAsync(cancellationToken).ConfigureAwait(false);

        return new ResourcePage(rows.Select(r => ResourceRowMapper.ToDomain(r, siteClosures)).ToList(), total);
    }

    public async Task<IReadOnlyList<Resource>> ListByTypeAsync(
        string type, CancellationToken cancellationToken = default)
    {
        // Deliberately unpaged (book-via-service design D2): the consumer is a
        // service's candidate pool, and truncating one produces a wrong answer
        // silently rather than an error. Child collections are included so each
        // aggregate is complete enough to compute availability from without a
        // second load per candidate.
        var rows = await db.Resources
            .AsNoTracking()
            .Include(r => r.OpenHours)
            .Include(r => r.Exceptions)
            .Include(r => r.Capabilities)
            .Include(r => r.ClosureOptOuts)
            .AsSplitQuery()
            .Where(r => r.Type == type)
            .OrderBy(r => r.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // One closure read for the whole pool, outside the projection below, so that the
        // cost of this query does not scale with the number of candidates.
        var siteClosures = await closures.ListAsync(cancellationToken).ConfigureAwait(false);

        return rows.Select(r => ResourceRowMapper.ToDomain(r, siteClosures)).ToList();
    }
}
