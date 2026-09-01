using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// Reference uniqueness, against the database that enforces it.
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
}
