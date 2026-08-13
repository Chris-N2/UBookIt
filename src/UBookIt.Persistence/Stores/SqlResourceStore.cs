using Microsoft.EntityFrameworkCore;
using UBookIt.Core.Resources;
using UBookIt.Core.Stores;

namespace UBookIt.Persistence.Stores;

/// <summary>
/// SQL Server implementation of the read port <see cref="IResourceStore"/>
/// (resource loads and public paged discovery). Resource writes live on the
/// separate management store.
/// </summary>
internal sealed class SqlResourceStore(UBookItDbContext db) : IResourceStore
{
    public async Task<Resource?> GetAsync(Guid resourceId, CancellationToken cancellationToken = default)
    {
        var row = await db.Resources
            .AsNoTracking()
            .Include(r => r.OpenHours)
            .Include(r => r.Exceptions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == resourceId, cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : ResourceRowMapper.ToDomain(row);
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
            .AsSplitQuery()
            .OrderBy(r => r.DisplayName).ThenBy(r => r.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ResourcePage(rows.Select(ResourceRowMapper.ToDomain).ToList(), total);
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
            .AsSplitQuery()
            .Where(r => r.Type == type)
            .OrderBy(r => r.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(ResourceRowMapper.ToDomain).ToList();
    }
}
