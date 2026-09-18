using Microsoft.EntityFrameworkCore;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Entities;

namespace UBookIt.Persistence.Stores;

/// <inheritdoc />
internal sealed class SqlCancellationSecretStore(UBookItDbContext db, TimeProvider clock) : ICancellationSecretStore
{
    public async Task IssueAsync(
        Guid bookingId, string hash, DateTimeOffset expiresUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        db.CancellationSecrets.Add(new CancellationSecretRow
        {
            Hash = hash,
            BookingId = bookingId,
            ExpiresUtc = expiresUtc,
            IssuedUtc = clock.GetUtcNow(),
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<CancellationSecretRecord?> FindAsync(
        string hash, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        // A SEEK on the primary key, which matters because this runs on an anonymous request.
        // AsNoTracking because this read changes nothing — retrieving the cancellation page must
        // not consume the booker's one use, since mail scanners fetch every link in a message.
        var row = await db.CancellationSecrets
            .AsNoTracking()
            .FirstOrDefaultAsync(secret => secret.Hash == hash, cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? null
            : new CancellationSecretRecord(row.BookingId, row.ExpiresUtc, row.RedeemedUtc is not null);
    }

    public async Task<Guid?> TryRedeemAsync(
        string hash, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        // A COMPARE-AND-SWAP IN ONE STATEMENT, and the single statement is the whole point. A read
        // followed by a separate write is exactly the race that lets one secret be redeemed twice:
        // two submissions arriving together both see RedeemedUtc null, both proceed, and a
        // single-use credential has been used twice. Here the database decides, and only one
        // UPDATE can match.
        //
        // Expiry is part of the WHERE rather than a check around it, so a secret that expires
        // between the page being rendered and the form being submitted is refused by the same
        // statement that would have redeemed it.
        var affected = await db.CancellationSecrets
            .Where(secret => secret.Hash == hash
                && secret.RedeemedUtc == null
                && secret.ExpiresUtc > nowUtc)
            .ExecuteUpdateAsync(
                update => update.SetProperty(secret => secret.RedeemedUtc, nowUtc),
                cancellationToken)
            .ConfigureAwait(false);

        if (affected == 0)
        {
            return null;
        }

        // Reached only where this call won the swap. The booking a row names never changes, so
        // reading it afterwards is safe — and reading it BEFORE would have meant answering from a
        // row this call had not yet claimed.
        var row = await db.CancellationSecrets
            .AsNoTracking()
            .FirstOrDefaultAsync(secret => secret.Hash == hash, cancellationToken)
            .ConfigureAwait(false);

        return row?.BookingId;
    }
}
