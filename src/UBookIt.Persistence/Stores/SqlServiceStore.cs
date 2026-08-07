using Microsoft.EntityFrameworkCore;
using UBookIt.Core.Services;
using UBookIt.Core.Stores;

namespace UBookIt.Persistence.Stores;

/// <summary>
/// SQL Server implementation of the read port <see cref="IServiceStore"/>
/// (service loads and paged listing). Service writes live on the separate
/// management store.
/// </summary>
internal sealed class SqlServiceStore(UBookItDbContext db) : IServiceStore
{
    public async Task<Service?> GetAsync(Guid serviceId, CancellationToken cancellationToken = default)
    {
        var row = await db.Services
            .AsNoTracking()
            .Include(s => s.Roles)
            .FirstOrDefaultAsync(s => s.Id == serviceId, cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : ServiceRowMapper.ToDomain(row);
    }

    public async Task<ServicePage> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 0, 500);

        var total = await db.Services.CountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await db.Services
            .AsNoTracking()
            .Include(s => s.Roles)
            .AsSplitQuery()
            .OrderBy(s => s.Name).ThenBy(s => s.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ServicePage(rows.Select(ServiceRowMapper.ToDomain).ToList(), total);
    }
}
