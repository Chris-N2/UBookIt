using Microsoft.EntityFrameworkCore;
using UBookIt.Core.Bookings;
using UBookIt.Core.Stores;
using UBookIt.Persistence;
using UBookIt.Persistence.Entities;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// The cancellation-secret store against a real SQL Server (`self-service-cancellation`, "A secret
/// is single use and expires when the booking starts"; `persistence`, "Cancellation secrets are
/// held in their own additive table").
/// </summary>
/// <remarks>
/// <b>Integration rather than unit, because the claim that matters is a property of the SQL.</b>
/// "A secret is accepted at most once" is a statement about what happens when two redemptions race,
/// and an in-memory double with a lock would prove only that the double has a lock. The
/// compare-and-swap is a single UPDATE whose WHERE clause carries the conditions, and only a real
/// database can be asked whether that holds.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class CancellationSecretStoreTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Now = new(2026, 9, 5, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Expires = new(2026, 9, 10, 10, 0, 0, TimeSpan.Zero);

    private ICancellationSecretStore Store(UBookItDbContext context)
        => new SqlCancellationSecretStore(context, new FixedClock(Now));

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private async Task<(Guid BookingId, CancellationSecret Secret)> IssueAsync(DateTimeOffset? expires = null)
    {
        var bookingId = Guid.NewGuid();
        var secret = CancellationSecret.Issue();

        await using var context = fixture.CreateContext();
        await Store(context).IssueAsync(bookingId, secret.Hash, expires ?? Expires, Ct);

        return (bookingId, secret);
    }

    [Fact]
    public async Task An_issued_secret_is_found_by_its_hash()
    {
        fixture.EnsureAvailable();
        var (bookingId, secret) = await IssueAsync();

        await using var context = fixture.CreateContext();
        var found = await Store(context).FindAsync(secret.Hash, Ct);

        Assert.NotNull(found);
        Assert.Equal(bookingId, found!.BookingId);
        Assert.Equal(Expires, found.ExpiresUtc);
        Assert.False(found.Redeemed);
    }

    [Fact]
    public async Task The_plaintext_secret_is_not_what_is_stored()
    {
        // THE GUARANTEE THE TABLE EXISTS TO KEEP. Looking a secret up by its own value must find
        // nothing, because the value was never written.
        fixture.EnsureAvailable();
        var (_, secret) = await IssueAsync();

        await using var context = fixture.CreateContext();
        Assert.Null(await Store(context).FindAsync(secret.Value, Ct));

        // And the value appears nowhere in the row, under any column.
        var rows = await context.Set<CancellationSecretRow>().AsNoTracking().ToListAsync(Ct);
        Assert.DoesNotContain(rows, row => row.Hash.Contains(secret.Value, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Finding_changes_nothing()
    {
        // Retrieving the cancellation page must not consume the booker's one use: mail scanners
        // fetch every link in a message, unattended, before a person sees it.
        fixture.EnsureAvailable();
        var (_, secret) = await IssueAsync();

        await using (var reading = fixture.CreateContext())
        {
            await Store(reading).FindAsync(secret.Hash, Ct);
            await Store(reading).FindAsync(secret.Hash, Ct);
        }

        await using var context = fixture.CreateContext();
        Assert.False((await Store(context).FindAsync(secret.Hash, Ct))!.Redeemed);
        Assert.NotNull(await Store(context).TryRedeemAsync(secret.Hash, Now, Ct));
    }

    [Fact]
    public async Task A_secret_redeems_once_and_only_once()
    {
        fixture.EnsureAvailable();
        var (bookingId, secret) = await IssueAsync();

        await using var first = fixture.CreateContext();
        Assert.Equal(bookingId, await Store(first).TryRedeemAsync(secret.Hash, Now, Ct));

        await using var second = fixture.CreateContext();
        Assert.Null(await Store(second).TryRedeemAsync(secret.Hash, Now, Ct));
    }

    [Fact]
    public async Task Two_simultaneous_redemptions_produce_exactly_one_winner()
    {
        // THE RACE, RUN. A read-then-write implementation passes every test above and fails this
        // one: both callers see RedeemedUtc null, both proceed, and a single-use credential has
        // been used twice. The compare-and-swap is why only one wins.
        fixture.EnsureAvailable();
        var (bookingId, secret) = await IssueAsync();

        var attempts = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var context = fixture.CreateContext();
            return await Store(context).TryRedeemAsync(secret.Hash, Now, Ct);
        }));

        Assert.Equal(1, attempts.Count(result => result is not null));
        Assert.All(attempts.Where(result => result is not null), result => Assert.Equal(bookingId, result));
    }

    [Fact]
    public async Task An_expired_secret_is_refused()
    {
        fixture.EnsureAvailable();
        var (_, secret) = await IssueAsync(expires: Now.AddMinutes(-1));

        await using var context = fixture.CreateContext();
        Assert.Null(await Store(context).TryRedeemAsync(secret.Hash, Now, Ct));
    }

    [Fact]
    public async Task A_secret_expiring_exactly_now_is_refused()
    {
        // The boundary is the booking's start, and at the start it is already too late.
        fixture.EnsureAvailable();
        var (_, secret) = await IssueAsync(expires: Now);

        await using var context = fixture.CreateContext();
        Assert.Null(await Store(context).TryRedeemAsync(secret.Hash, Now, Ct));
    }

    [Fact]
    public async Task A_hash_that_was_never_issued_is_refused_and_found_as_nothing()
    {
        fixture.EnsureAvailable();
        var never = CancellationSecret.Issue();

        await using var context = fixture.CreateContext();
        Assert.Null(await Store(context).FindAsync(never.Hash, Ct));
        Assert.Null(await Store(context).TryRedeemAsync(never.Hash, Now, Ct));
    }

    [Fact]
    public async Task An_expired_secret_is_still_visible_to_a_read()
    {
        // FindAsync deliberately returns expired and redeemed records rather than hiding them as
        // absent: the four causes of refusal converge on one sentence, and that convergence is a
        // decision taken in one place rather than scattered across a store returning null for some
        // causes and a record for others.
        fixture.EnsureAvailable();
        var (_, secret) = await IssueAsync(expires: Now.AddMinutes(-1));

        await using var context = fixture.CreateContext();
        Assert.NotNull(await Store(context).FindAsync(secret.Hash, Ct));
    }
}
