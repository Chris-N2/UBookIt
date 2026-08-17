using Microsoft.EntityFrameworkCore;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// The atomic-placement contract for bookings claiming several resources
/// (bookings spec, "Atomic placement contract"), proved against real SQL Server
/// rather than inherited from the single-claim case.
/// <para>
/// The store's code has looked multi-claim-ready since change ③ — it sorts claim
/// ids and takes a lock per resource — but looking right is not evidence: this
/// repository's ③ QA found a CRITICAL concurrency hole in an implementation that
/// also looked right. These tests are designed to go red if the per-resource
/// lock is removed, which was confirmed by removing it.
/// </para>
/// </summary>
[Collection(SqlServerCollection.Name)]
public class MultiClaimConcurrencyTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Start = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    private static Booking Claiming(DateTimeOffset startUtc, TimeSpan duration, params Guid[] resourceIds)
        => Booking.Rehydrate(
            Guid.NewGuid(),
            BookingInterval.Create(startUtc, startUtc + duration, "UTC").Value,
            Booker.Create(null, "Integration Tester", "integration@example.com", "01234 567890").Value,
            resourceIds.Select(id => new ResourceClaim(id)),
            BookingStatus.Confirmed,
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)).Value;

    /// <summary>
    /// Runs each booking on its own context — its own connection and
    /// transaction — started as close together as the scheduler allows.
    /// </summary>
    private async Task<DomainResult<Booking>[]> RaceAsync(params Booking[] bookings)
    {
        using var gate = new Barrier(bookings.Length);

        return await Task.WhenAll(bookings.Select(booking => Task.Run(async () =>
        {
            await using var context = fixture.CreateContext();
            var store = new SqlBookingStore(context);

            // Opened before the gate, not after: connection establishment is far
            // slower than the critical section, so racers that opened their
            // connections after being released would arrive staggered and never
            // overlap the window this test exists to squeeze.
            await context.Database.OpenConnectionAsync(Ct);

            // Every racer waits for the others before touching the database, so
            // the placements genuinely overlap rather than queueing.
            gate.SignalAndWait(Ct);

            return await store.PlaceAsync(booking, Ct);
        }, Ct)));
    }

    [Fact]
    public async Task Spec_scenario_racing_multi_claim_placements_that_share_a_resource()
    {
        fixture.EnsureAvailable();

        var a = await Seed.EveryDayRoomAsync(fixture, Ct);
        var b = await Seed.EveryDayRoomAsync(fixture, Ct);
        var c = await Seed.EveryDayRoomAsync(fixture, Ct);

        // {A,B} against {B,C} over overlapping intervals: the case a per-booking
        // lock would pass and a per-resource lock catches.
        //
        // Twelve racers rather than two, and this is not belt-and-braces: with
        // the per-resource lock removed, a two-racer version passed three runs
        // out of three, because the window between the conflict check and the
        // insert is too narrow for two threads to reliably straddle. It only
        // goes red at this width, which is the difference between a test that
        // proves atomicity and one that happens to agree with it.
        var results = await RaceAsync(
            [.. Enumerable.Range(0, 12).Select(n => n % 2 == 0
                ? Claiming(Start, TimeSpan.FromHours(1), a, b)
                : Claiming(Start.AddMinutes(30), TimeSpan.FromHours(1), b, c))]);

        Assert.Equal(1, results.Count(r => r.Succeeded));
        Assert.All(
            results.Where(r => !r.Succeeded),
            r => Assert.Equal(FailureCodes.Conflict, Assert.Single(r.Failures).Code));

        // B was claimed once, by the winner, and the loser left nothing behind.
        await using var context = fixture.CreateContext();
        Assert.Equal(1, context.Claims.Count(claim => claim.ResourceId == b));

        var winner = results.Single(r => r.Succeeded).Value;
        Assert.Equal(
            winner.Claims.Select(claim => claim.ResourceId).OrderBy(id => id),
            context.Claims.Where(claim => claim.BookingId == winner.Id)
                .Select(claim => claim.ResourceId)
                .ToList()
                .OrderBy(id => id));
    }

    [Fact]
    public async Task Spec_scenario_multi_claim_placements_that_share_nothing_both_succeed()
    {
        fixture.EnsureAvailable();

        var a = await Seed.EveryDayRoomAsync(fixture, Ct);
        var b = await Seed.EveryDayRoomAsync(fixture, Ct);
        var c = await Seed.EveryDayRoomAsync(fixture, Ct);
        var d = await Seed.EveryDayRoomAsync(fixture, Ct);

        // The counterpart that stops the sharing test passing for the wrong
        // reason: a store that simply serialized every placement, or locked more
        // than the claimed resources, would fail one of these.
        var results = await RaceAsync(
            Claiming(Start, TimeSpan.FromHours(1), a, b),
            Claiming(Start, TimeSpan.FromHours(1), c, d));

        Assert.All(results, r => Assert.True(r.Succeeded, "disjoint claim sets do not conflict"));

        await using var context = fixture.CreateContext();
        Assert.Equal(4, context.Claims.Count(claim => new[] { a, b, c, d }.Contains(claim.ResourceId)));
    }

    [Fact]
    public async Task Spec_scenario_a_failed_multi_claim_placement_leaves_nothing()
    {
        fixture.EnsureAvailable();

        var a = await Seed.EveryDayRoomAsync(fixture, Ct);
        var b = await Seed.EveryDayRoomAsync(fixture, Ct);

        await using (var context = fixture.CreateContext())
        {
            var store = new SqlBookingStore(context);
            Assert.True((await store.PlaceAsync(Claiming(Start, TimeSpan.FromHours(1), b), Ct)).Succeeded);
        }

        await using (var context = fixture.CreateContext())
        {
            var store = new SqlBookingStore(context);
            var blocked = await store.PlaceAsync(Claiming(Start, TimeSpan.FromHours(1), a, b), Ct);

            Assert.False(blocked.Succeeded);
            Assert.Equal(FailureCodes.Conflict, Assert.Single(blocked.Failures).Code);

            // Nothing for A, whose own calendar was free — the candidate loop
            // depends on a failed attempt persisting no state at all.
            Assert.Empty(context.Claims.Where(claim => claim.ResourceId == a));
            Assert.Equal(1, context.Bookings.Count(booking => booking.Claims.Any(claim => claim.ResourceId == b)));
        }
    }

    [Fact]
    public async Task Spec_scenario_deadlock_free_under_crossing_claim_sets()
    {
        fixture.EnsureAvailable();

        var a = await Seed.EveryDayRoomAsync(fixture, Ct);
        var b = await Seed.EveryDayRoomAsync(fixture, Ct);

        // The same two resources claimed in opposite orders. The store sorts
        // before locking, so both sessions take A then B; without that sort this
        // is the classic two-lock deadlock, and one side would fail with a
        // deadlock or lock-timeout exception rather than a `conflict` result.
        var results = await RaceAsync(
            Claiming(Start, TimeSpan.FromHours(1), a, b),
            Claiming(Start, TimeSpan.FromHours(1), b, a));

        Assert.Equal(1, results.Count(r => r.Succeeded));
        Assert.All(
            results.Where(r => !r.Succeeded),
            r => Assert.Equal(FailureCodes.Conflict, Assert.Single(r.Failures).Code));
    }

    [Fact]
    public async Task Multi_claim_placement_needed_no_schema_change()
    {
        fixture.EnsureAvailable();

        // The claims table has always been one row per claim, so multi-claim
        // placement added no migration. Pinned rather than stated — but note what
        // the pin does and does not mean.
        //
        // It is a **prompt**, not a proof. Any new migration trips it, including
        // ones with nothing to do with claims: `AddDirectBookability` adds a
        // boolean column to the resources table and leaves the claim model exactly
        // as it was. The value of the list is that someone has to look at each new
        // entry and confirm that, which is why entries are added deliberately
        // rather than the assertion being loosened.
        //
        // Anything that appears here and DOES touch the claims table means the
        // guarantee this test names has been broken, and appending it would be the
        // wrong response.
        await using var context = fixture.CreateContext();
        var applied = await context.Database.GetAppliedMigrationsAsync(Ct);

        Assert.Equal(
            [
                "20260727191209_Initial",
                "20260727193625_ClaimResourceIndexIncludesBookingId",
                "20260807125020_AddServices",
                "20260814082537_AddCapabilities",

                // Resources gain `DirectlyBookable`. Checked: no claim or booking
                // table is touched, so the guarantee above still holds.
                "20260817080001_AddDirectBookability",
            ],
            applied.OrderBy(name => name, StringComparer.Ordinal));

        Assert.Empty(await context.Database.GetPendingMigrationsAsync(Ct));
    }
}
