using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// Reference uniqueness, against the database that enforces it.
/// <para>
/// <b>Every literal reference in the integration suites must be unique across the whole
/// suite.</b> These tests share one database, so two tests naming the same reference collide
/// and whichever runs second is refused — a failure that depends on ordering and therefore
/// appears and disappears. It happened while writing these: this file and
/// <c>BookingManagementStoreTests</c> both used <c>QF7M3XKB</c>.
/// </para>
/// <para>
/// The unit tests exercise the retry against an in-memory store that mirrors this rule. That
/// mirror is only worth trusting if the real store actually behaves the way it copies, which
/// is what this establishes — a fake more permissive than the thing it stands in for tests
/// nothing.
/// </para>
/// </summary>
[Collection(SqlServerCollection.Name)]
public class BookingReferenceUniquenessTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_second_booking_cannot_take_a_reference_already_in_use()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var reference = BookingReference.FromCanonical("7QX4M2NP");

        var first = Seed.ConfirmedBooking(
            resourceId, new DateTimeOffset(2026, 9, 15, 9, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1), reference: reference);

        // A different interval, so nothing but the reference can be what refuses it.
        var second = Seed.ConfirmedBooking(
            resourceId, new DateTimeOffset(2026, 9, 15, 14, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1), reference: reference);

        await using var context = fixture.CreateContext();
        var store = new SqlBookingStore(context);

        Assert.True((await store.PlaceAsync(first, Ct)).Succeeded);

        var refused = await store.PlaceAsync(second, Ct);

        Assert.False(refused.Succeeded);
        Assert.Equal(FailureCodes.ReferenceTaken, Assert.Single(refused.Failures).Code);
    }

    [Fact]
    public async Task A_refused_reference_leaves_no_row_behind()
    {
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var reference = BookingReference.FromCanonical("5KGTBW9D");

        var first = Seed.ConfirmedBooking(
            resourceId, new DateTimeOffset(2026, 9, 16, 9, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1), reference: reference);
        var second = Seed.ConfirmedBooking(
            resourceId, new DateTimeOffset(2026, 9, 16, 14, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1), reference: reference);

        await using var context = fixture.CreateContext();
        var store = new SqlBookingStore(context);

        Assert.True((await store.PlaceAsync(first, Ct)).Succeeded);
        Assert.False((await store.PlaceAsync(second, Ct)).Succeeded);

        // The refusal rolls back inside the placement transaction. A half-written booking
        // would hold its interval against every future placement while belonging to nobody.
        Assert.Null(await store.GetBookingAsync(second.Id, Ct));
    }

    /// <summary>SQL Server's two duplicate-key errors: unique index, and unique constraint.</summary>
    private static readonly int[] DuplicateKey = [2601, 2627];

    [Fact]
    public async Task The_schema_refuses_a_duplicate_reference_even_when_nothing_checks_first()
    {
        // THE GUARANTEE, observed directly. Three normative statements say the INDEX is what
        // makes a reference unique and that the store's pre-check explicitly is not:
        // `bookings` ("guaranteed by the store itself rather than rest on a check performed
        // beforehand… what it MUST NOT do is let that check be the thing uniqueness depends
        // on"), `persistence` ("enforced by the schema"), and design D3 ("delete the index and
        // the check does not save you").
        //
        // Nothing observed it. Removing the unique index — model, migration and both
        // snapshots, consistently, so no drift detector fires — passed 1796 of 1796 tests. The
        // two tests above assert `ReferenceTaken`, which only the pre-check can produce, so
        // they watch the check and never the constraint.
        //
        // This writes the row the way something that is not our code would: a raw INSERT that
        // performs no check at all. If the index is gone, it succeeds and this fails, which is
        // the whole point.
        fixture.EnsureAvailable();

        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var reference = BookingReference.FromCanonical("WXY8NPQ2");

        var placed = Seed.ConfirmedBooking(
            resourceId,
            new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.Zero),
            TimeSpan.FromHours(1),
            reference: reference);

        await using var context = fixture.CreateContext();

        Assert.True((await new SqlBookingStore(context).PlaceAsync(placed, Ct)).Succeeded);

        // A different interval, so an overlap could never be what refuses it — and no claims,
        // because the booking table alone carries the constraint under test.
        var duplicate = await Record.ExceptionAsync(() => context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO uBookItBooking
                (Id, Reference, StartUtc, EndUtc, TimeZoneId, Status, CreatedUtc, BookerName, BookerEmail)
            VALUES
                ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8})
            """,
            [
                Guid.NewGuid(),
                reference.Value,
                new DateTimeOffset(2026, 12, 2, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 12, 2, 10, 0, 0, TimeSpan.Zero),
                "UTC",
                (int)BookingStatus.Confirmed,
                new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero),
                "Raw Writer",
                "raw@example.com",
            ],
            Ct));

        Assert.NotNull(duplicate);

        // Specifically a duplicate-key violation, so this cannot pass because the insert failed
        // for some unrelated reason — a wrong column list would throw too, and would prove
        // nothing about uniqueness.
        var sql = Assert.IsType<SqlException>(duplicate.InnerException ?? duplicate);

        Assert.Contains(sql.Number, DuplicateKey);
    }
}
