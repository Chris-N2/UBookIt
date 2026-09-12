using Microsoft.EntityFrameworkCore;
using UBookIt.Persistence.Entities;

namespace UBookIt.Persistence.Stores;

/// <summary>
/// One-shot markers: operations the package performs at most once per installation.
/// A flag either exists or does not, and carries only when it was applied.
/// </summary>
/// <remarks>
/// The contract every one-shot operation is written against: check, do the work, and
/// write the flag <b>only on full success</b> — so an interrupted operation retries at
/// the next startup, and the idempotence of that retry is the operation's burden, not
/// this store's. Nothing here holds personal data, ever.
/// </remarks>
public interface IFlagStore
{
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    Task SetAsync(string key, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
internal sealed class SqlFlagStore(UBookItDbContext db, TimeProvider clock) : IFlagStore
{
    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => db.Flags.AsNoTracking().AnyAsync(f => f.Key == key, cancellationToken);

    public async Task SetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (await ExistsAsync(key, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        db.Flags.Add(new FlagRow { Key = key, AppliedUtc = clock.GetUtcNow() });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
