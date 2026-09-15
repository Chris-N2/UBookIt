using Microsoft.EntityFrameworkCore;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Entities;

namespace UBookIt.Persistence.Stores;

/// <inheritdoc />
internal sealed class SqlSettingsStore(UBookItDbContext db, TimeProvider clock) : ISettingsStore
{
    public IReadOnlyDictionary<string, string> GetAll()
    {
        // A genuinely synchronous query rather than blocking on an async one: this runs during
        // service resolution (see ISettingsStore.GetAll), and sync-over-async there would occupy
        // a thread-pool thread on every request. The table holds one row per overridden setting —
        // fewer than ten — and is keyed.
        var rows = db.Settings.AsNoTracking().ToList();

        // Ordinal, matching how configuration keys are compared everywhere else in the package:
        // these are the package's own constants, so a casing difference is a bug to surface
        // rather than tolerate.
        return rows.ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var existing = await db.Settings
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            db.Settings.Add(new SettingRow
            {
                Key = key,
                Value = value,
                UpdatedUtc = clock.GetUtcNow(),
            });
        }
        else
        {
            existing.Value = value;
            existing.UpdatedUtc = clock.GetUtcNow();
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var existing = await db.Settings
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken)
            .ConfigureAwait(false);

        // Removing what is not there is not an error: the caller asked for the setting to be
        // unstored, and it is unstored. Reporting a failure would make "reset" fail on a setting
        // that had never been overridden, which is the ordinary case for most of them.
        if (existing is null)
        {
            return;
        }

        db.Settings.Remove(existing);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
