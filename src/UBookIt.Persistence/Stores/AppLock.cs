using Microsoft.EntityFrameworkCore;

namespace UBookIt.Persistence.Stores;

/// <summary>
/// Transaction-owned exclusive SQL Server application locks. Placement and
/// configuration writes use distinct lock names so they serialize within
/// their own concern without contending with each other.
/// </summary>
internal static class AppLock
{
    internal static string ForResourcePlacement(Guid resourceId) => $"ubookit:resource:{resourceId:N}";

    internal static string ForResourceConfig(Guid resourceId) => $"ubookit:resource-config:{resourceId:N}";

    internal static string ForServiceConfig(Guid serviceId) => $"ubookit:service-config:{serviceId:N}";

    /// <summary>Must be called inside an open transaction; released at commit/rollback.</summary>
    internal static Task AcquireAsync(UBookItDbContext db, string lockResource, CancellationToken cancellationToken)
        => db.Database.ExecuteSqlAsync($"""
            DECLARE @result int;
            EXEC @result = sp_getapplock
                @Resource = {lockResource},
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 15000;
            IF @result < 0 THROW 51000, 'uBookIt: failed to acquire an application lock.', 1;
            """, cancellationToken);
}
