using Microsoft.EntityFrameworkCore;
using UBookIt.Core.Resources;
using UBookIt.Core.Stores;

namespace UBookIt.Persistence.Stores;

/// <summary>
/// SQL Server implementation of <see cref="IResourceStore"/>. Read-only in
/// this change; write surface arrives with the management API change.
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
}
