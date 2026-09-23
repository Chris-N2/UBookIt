using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Entities;

namespace UBookIt.Persistence.Stores;

/// <summary>
/// SQL Server implementation of <see cref="IResourceManagementStore"/>.
/// Updates replace the resource's availability child rows wholesale within a
/// single transaction — the write-path exception-date uniqueness enforcement
/// (design D2): every committed write is one writer's complete validated set.
/// </summary>
internal sealed class SqlResourceManagementStore(UBookItDbContext db, ISiteClosureStore closures)
    : IResourceManagementStore
{
    private const int SqlForeignKeyViolation = 547;

    /// <summary>
    /// Test seam: invoked between the delete pre-checks and the delete
    /// statements so integration tests can deterministically exercise the
    /// FK-backstop path (a claim placed concurrently with the delete).
    /// Always null in production.
    /// </summary>
    internal Func<CancellationToken, Task>? TestHookAfterDeletePreCheck { get; set; }

    public async Task<DomainResult<Resource>> CreateAsync(Resource resource, CancellationToken cancellationToken = default)
    {
        db.Resources.Add(ResourceRowMapper.ToRow(resource));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return DomainResult<Resource>.Success(resource);
    }

    public async Task<DomainResult<Resource>> UpdateAsync(Resource resource, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Serialize configuration writers per resource. Full-replace alone is
        // NOT sufficient: at READ COMMITTED, deleting zero child rows takes no
        // range locks, so two concurrent updates against an empty child set
        // would both insert and commit a merged union (QA finding, first
        // management-api review).
        await AppLock.AcquireAsync(db, AppLock.ForResourceConfig(resource.Id), cancellationToken)
            .ConfigureAwait(false);

        var row = await db.Resources
            .FirstOrDefaultAsync(r => r.Id == resource.Id, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return DomainResult<Resource>.Failure(
                FailureCodes.ResourceNotFound, $"No resource exists with id {resource.Id}.");
        }

        ResourceRowMapper.ApplyScalars(resource, row);

        // Full replace of availability child rows (never a merge).
        await db.OpenHours.Where(w => w.ResourceId == resource.Id)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.Exceptions.Where(e => e.ResourceId == resource.Id)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.ResourceCapabilities.Where(c => c.ResourceId == resource.Id)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.ResourceClosureOptOuts.Where(o => o.ResourceId == resource.Id)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        db.OpenHours.AddRange(ResourceRowMapper.ToOpenHoursRows(resource));
        db.Exceptions.AddRange(ResourceRowMapper.ToExceptionRows(resource));
        db.ResourceCapabilities.AddRange(ResourceRowMapper.ToCapabilityRows(resource));

        // Exemptions are replaced wholesale with the rest of the resource's availability,
        // inside the same transaction and under the same per-resource lock, so a write can
        // neither merge two writers' sets nor leave a resource half-exempted.
        db.ResourceClosureOptOuts.AddRange(ResourceRowMapper.ToClosureOptOutRows(resource));

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return DomainResult<Resource>.Success(resource);
    }

    public async Task<DomainResult> DeleteAsync(Guid resourceId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Serialize against configuration updates so a racing update cannot
        // reinsert child rows mid-delete.
        await AppLock.AcquireAsync(db, AppLock.ForResourceConfig(resourceId), cancellationToken)
            .ConfigureAwait(false);

        var exists = await db.Resources
            .AnyAsync(r => r.Id == resourceId, cancellationToken).ConfigureAwait(false);

        if (!exists)
        {
            return DomainResult.Failure(
                FailureCodes.ResourceNotFound, $"No resource exists with id {resourceId}.");
        }

        var hasClaims = await db.Claims
            .AnyAsync(c => c.ResourceId == resourceId, cancellationToken).ConfigureAwait(false);

        if (hasClaims)
        {
            return ResourceInUse();
        }

        if (TestHookAfterDeletePreCheck is not null)
        {
            await TestHookAfterDeletePreCheck(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await db.OpenHours.Where(w => w.ResourceId == resourceId)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await db.Exceptions.Where(e => e.ResourceId == resourceId)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await db.ResourceCapabilities.Where(c => c.ResourceId == resourceId)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

            // Assignments go with their subject, inside the same locked transaction —
            // the responsibility table has no FK to enforce it, and the shared config
            // lock is what keeps a concurrent assignment write from re-inserting rows
            // for a subject that is going away. A later resource given a recycled id
            // must inherit nobody.
            await db.Responsibilities
                .Where(a => a.SubjectType == ResponsibilitySubjectTypes.Resource && a.SubjectId == resourceId)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await db.Resources.Where(r => r.Id == resourceId)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return DomainResult.Success();
        }
        catch (Exception ex) when (IsForeignKeyViolation(ex))
        {
            // A claim was placed concurrently with the delete; the restrictive
            // FK is the backstop (persistence spec, "Delete semantics at the store").
            return ResourceInUse();
        }

        static DomainResult ResourceInUse() => DomainResult.Failure(
            FailureCodes.ResourceInUse,
            "The resource has booking claims and cannot be deleted. Cancel its bookings first.");
    }

    /// <summary>Matches SQL error 547 whether the provider exception is raw or wrapped.</summary>
    private static bool IsForeignKeyViolation(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is SqlException { Number: SqlForeignKeyViolation })
            {
                return true;
            }

            exception = exception.InnerException;
        }

        return false;
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

        // One closure read for the page, so the backoffice list reports the same
        // inheritance the booking path computes, at the same cost whatever its size.
        var siteClosures = await closures.ListAsync(cancellationToken).ConfigureAwait(false);

        return new ResourcePage(
            rows.Select(r => ResourceRowMapper.ToDomain(r, siteClosures)).ToList(), total);
    }

    public async Task<IReadOnlyList<ResourceTypeUsage>> ListTypesAsync(CancellationToken cancellationToken = default)
    {
        // A grouped projection over the resources table only — no child-row
        // includes, and it scales with the number of distinct types rather than
        // the number of resources. Ordered by type key so the backoffice picker
        // does not reshuffle between loads.
        //
        // The GROUP BY projects to an anonymous type, not straight to
        // ResourceTypeUsage: EF cannot translate a positional record's
        // constructor inside a grouping projection. The record is constructed
        // after materialization, so the aggregation still runs server-side.
        var grouped = await db.Resources
            .AsNoTracking()
            .GroupBy(r => r.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .OrderBy(t => t.Type)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return grouped.Select(g => new ResourceTypeUsage(g.Type, g.Count)).ToList();
    }

    public async Task<IReadOnlyList<CapabilityUsage>> ListCapabilitiesAsync(
        CancellationToken cancellationToken = default)
    {
        // Same shape as ListTypesAsync, including the reason for the anonymous
        // type: EF cannot translate a positional record's constructor inside a
        // grouping projection, so the record is built after materialization and
        // the aggregation still runs server-side.
        var grouped = await db.ResourceCapabilities
            .AsNoTracking()
            .GroupBy(c => c.Key)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .OrderBy(c => c.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return grouped.Select(g => new CapabilityUsage(g.Key, g.Count)).ToList();
    }
}
