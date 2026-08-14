using Microsoft.EntityFrameworkCore;
using UBookIt.Core.Common;
using UBookIt.Core.Services;
using UBookIt.Core.Stores;

namespace UBookIt.Persistence.Stores;

/// <summary>
/// SQL Server implementation of <see cref="IServiceManagementStore"/>. Updates
/// replace the service's role rows wholesale within a single transaction, under
/// a per-service application lock: full-replace alone is not sufficient at READ
/// COMMITTED, where deleting zero/one child rows takes no range lock, so two
/// concurrent updates could both insert and commit a merged role set — breaking
/// the single-role invariant. This is the same race QA caught for resource
/// configuration in the management-api change.
/// </summary>
internal sealed class SqlServiceManagementStore(UBookItDbContext db) : IServiceManagementStore
{
    public async Task<DomainResult<Service>> CreateAsync(Service service, CancellationToken cancellationToken = default)
    {
        db.Services.Add(ServiceRowMapper.ToRow(service));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return DomainResult<Service>.Success(service);
    }

    public async Task<DomainResult<Service>> UpdateAsync(Service service, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await AppLock.AcquireAsync(db, AppLock.ForServiceConfig(service.Id), cancellationToken)
            .ConfigureAwait(false);

        var row = await db.Services
            .FirstOrDefaultAsync(s => s.Id == service.Id, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return DomainResult<Service>.Failure(
                FailureCodes.ServiceNotFound, $"No service exists with id {service.Id}.");
        }

        ServiceRowMapper.ApplyScalars(service, row);

        // Full replace of role rows (never a merge).
        await db.ServiceRoles.Where(r => r.ServiceId == service.Id)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        db.ServiceRoles.AddRange(ServiceRowMapper.ToRoleRows(service));

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return DomainResult<Service>.Success(service);
    }

    public async Task<DomainResult> DeleteAsync(Guid serviceId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Serialize against configuration updates so a racing update cannot
        // reinsert role rows mid-delete.
        await AppLock.AcquireAsync(db, AppLock.ForServiceConfig(serviceId), cancellationToken)
            .ConfigureAwait(false);

        var exists = await db.Services
            .AnyAsync(s => s.Id == serviceId, cancellationToken).ConfigureAwait(false);

        if (!exists)
        {
            return DomainResult.Failure(
                FailureCodes.ServiceNotFound, $"No service exists with id {serviceId}.");
        }

        // Roles cascade with the service, but delete explicitly within the
        // locked transaction for clarity and determinism.
        await db.ServiceRoles.Where(r => r.ServiceId == serviceId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.Services.Where(s => s.Id == serviceId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return DomainResult.Success();
    }

    public async Task<ServicePage> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 0, 500);

        var total = await db.Services.CountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await db.Services
            .AsNoTracking()
            .Include(s => s.Roles)
                .ThenInclude(r => r.Capabilities)
            .AsSplitQuery()
            .OrderBy(s => s.Name).ThenBy(s => s.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ServicePage(rows.Select(ServiceRowMapper.ToDomain).ToList(), total);
    }
}
