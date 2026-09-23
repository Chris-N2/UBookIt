using Microsoft.EntityFrameworkCore;
using UBookIt.Core;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Stores;
using UBookIt.Persistence;
using UBookIt.Persistence.Entities;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// The move write against real SQL Server (persistence spec, "Atomic move on SQL Server";
/// bookings spec, "Atomic move contract").
/// </summary>
/// <remarks>
/// <para>
/// <b>Every assertion reads the row back in a fresh context</b>, for the reason the erasure
/// suite records: the aggregate carries the change whether or not a row was written.
/// </para>
/// <para>
/// <b>The interleavings are staged, not hoped for.</b> The contract's hardest clause — the
/// status predicate is inside the update statement — produces identical rows to a
/// read-then-write in every single-threaded test. So the "between read and write" cases put a
/// real concurrent write at exactly that moment through a store wrapper around the real SQL
/// store, and the statement's shape is asserted from what was sent.
/// </para>
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class MoveStorageTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Now = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Ten = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Fourteen = new(2026, 9, 16, 14, 0, 0, TimeSpan.Zero);

    private static readonly BookingStatus[] Movable = [BookingStatus.Requested, BookingStatus.Confirmed];

    private static BookingInterval Interval(DateTimeOffset start, int minutes)
        => BookingInterval.Create(start, start.AddMinutes(minutes), "UTC").Value;

    private async Task<Booking> PlaceAsync(Guid resourceId, DateTimeOffset start, int minutes = 60, BookingStatus status = BookingStatus.Confirmed)
    {
        await using var context = fixture.CreateContext();
        var placed = await new SqlBookingStore(context)
            .PlaceAsync(Seed.ConfirmedBooking(resourceId, start, TimeSpan.FromMinutes(minutes), status), Ct);
        Assert.True(placed.Succeeded, string.Join(", ", placed.Failures.Select(f => f.Code)));
        return placed.Value;
    }

    private async Task<BookingRow> RowAsync(Guid bookingId)
    {
        await using var context = fixture.CreateContext();
        return await context.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId, Ct);
    }

    [Fact]
    public async Task A_move_reaches_the_database_and_touches_only_the_interval()
    {
        fixture.EnsureAvailable();
        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var booking = await PlaceAsync(resourceId, Ten);

        await using var context = fixture.CreateContext();
        var moved = await new SqlBookingStore(context).MoveAsync(booking.Id, Interval(Fourteen, 90), Movable, Ct);

        Assert.True(moved.Succeeded);
        var row = await RowAsync(booking.Id);
        Assert.Equal(Fourteen, row.StartUtc);
        Assert.Equal(Fourteen.AddMinutes(90), row.EndUtc);
        Assert.Equal("UTC", row.TimeZoneId);
        Assert.Equal((int)BookingStatus.Confirmed, row.Status);
        Assert.Equal("Integration Tester", row.BookerName);
        Assert.Equal(booking.Reference.Value, row.Reference);
        Assert.Equal(1, (await fixture.CreateContext().Claims.CountAsync(c => c.BookingId == booking.Id, Ct)));
    }

    [Fact]
    public async Task A_move_does_not_conflict_with_its_own_claims()
    {
        fixture.EnsureAvailable();
        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var booking = await PlaceAsync(resourceId, Ten);

        await using var context = fixture.CreateContext();
        var moved = await new SqlBookingStore(context).MoveAsync(booking.Id, Interval(Ten.AddMinutes(30), 60), Movable, Ct);

        Assert.True(moved.Succeeded, string.Join(", ", moved.Failures.Select(f => f.Code)));
        Assert.Equal(Ten.AddMinutes(30), (await RowAsync(booking.Id)).StartUtc);
    }

    [Fact]
    public async Task A_failed_move_leaves_the_old_interval()
    {
        fixture.EnsureAvailable();
        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var booking = await PlaceAsync(resourceId, Ten);
        await PlaceAsync(resourceId, Fourteen);

        await using var context = fixture.CreateContext();
        var moved = await new SqlBookingStore(context).MoveAsync(booking.Id, Interval(Fourteen.AddMinutes(-30), 60), Movable, Ct);

        Assert.Equal(FailureCodes.Conflict, Assert.Single(moved.Failures).Code);
        var row = await RowAsync(booking.Id);
        Assert.Equal(Ten, row.StartUtc);
        Assert.Equal(Ten.AddHours(1), row.EndUtc);
    }

    [Fact]
    public async Task A_move_of_a_multi_claim_booking_locks_and_checks_every_resource()
    {
        fixture.EnsureAvailable();
        var room = await Seed.EveryDayRoomAsync(fixture, Ct);
        var therapist = await Seed.EveryDayRoomAsync(fixture, Ct, type: "therapist");

        Booking twoClaims;
        await using (var context = fixture.CreateContext())
        {
            twoClaims = Booking.Rehydrate(
                Guid.NewGuid(),
                new RandomBookingReferenceFactory().Next(),
                Interval(Ten, 60),
                Booker.Create(null, "Pair Person", "pair@example.com").Value,
                [new ResourceClaim(room), new ResourceClaim(therapist)],
                BookingStatus.Confirmed,
                Now).Value;
            Assert.True((await new SqlBookingStore(context).PlaceAsync(twoClaims, Ct)).Succeeded);
        }

        // The therapist alone is busy at 14:00.
        await PlaceAsync(therapist, Fourteen);

        await using var moveContext = fixture.CreateContext();
        var moved = await new SqlBookingStore(moveContext).MoveAsync(twoClaims.Id, Interval(Fourteen, 60), Movable, Ct);

        Assert.Equal(FailureCodes.Conflict, Assert.Single(moved.Failures).Code);
        Assert.Equal(Ten, (await RowAsync(twoClaims.Id)).StartUtc);
    }

    [Fact]
    public async Task An_unknown_booking_is_reported_as_not_found()
    {
        fixture.EnsureAvailable();

        await using var context = fixture.CreateContext();
        var moved = await new SqlBookingStore(context).MoveAsync(Guid.NewGuid(), Interval(Fourteen, 60), Movable, Ct);

        Assert.Equal(FailureCodes.BookingNotFound, Assert.Single(moved.Failures).Code);
    }

    [Fact]
    public async Task Racing_one_move_against_many_placements_for_the_same_interval_yields_exactly_one_holder()
    {
        // The persistence spec's concurrency proof, for the move: one mover and at least ten
        // placers all want 14:00–15:00 on one resource. Exactly one gets it.
        fixture.EnsureAvailable();
        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var booking = await PlaceAsync(resourceId, Ten);

        var mover = Task.Run(async () =>
        {
            await using var context = fixture.CreateContext();
            return await new SqlBookingStore(context).MoveAsync(booking.Id, Interval(Fourteen, 60), Movable, Ct);
        }, Ct);

        var placers = Enumerable.Range(0, 12).Select(_ => Task.Run(async () =>
        {
            await using var context = fixture.CreateContext();
            var placed = await new SqlBookingStore(context)
                .PlaceAsync(Seed.ConfirmedBooking(resourceId, Fourteen, TimeSpan.FromHours(1)), Ct);
            return placed.Succeeded ? DomainResult.Success() : DomainResult.Failure(placed.Failures);
        }, Ct));

        var outcomes = await Task.WhenAll(placers.Prepend(mover));

        Assert.Equal(1, outcomes.Count(o => o.Succeeded));
        Assert.All(outcomes.Where(o => !o.Succeeded), o => Assert.Equal(FailureCodes.Conflict, Assert.Single(o.Failures).Code));

        await using var after = fixture.CreateContext();
        var holders = await (
            from claim in after.Claims
            join b in after.Bookings on claim.BookingId equals b.Id
            where claim.ResourceId == resourceId && b.StartUtc < Fourteen.AddHours(1) && Fourteen < b.EndUtc
            select b.Id).CountAsync(Ct);
        Assert.Equal(1, holders);
    }

    [Fact]
    public async Task Two_moves_of_the_same_booking_complete_without_deadlock_and_one_interval_remains()
    {
        fixture.EnsureAvailable();
        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var booking = await PlaceAsync(resourceId, Ten);
        var thirteen = Ten.AddHours(3);
        var fifteen = Ten.AddHours(5);

        var outcomes = await Task.WhenAll(
            Task.Run(async () =>
            {
                await using var context = fixture.CreateContext();
                return await new SqlBookingStore(context).MoveAsync(booking.Id, Interval(thirteen, 60), Movable, Ct);
            }, Ct),
            Task.Run(async () =>
            {
                await using var context = fixture.CreateContext();
                return await new SqlBookingStore(context).MoveAsync(booking.Id, Interval(fifteen, 60), Movable, Ct);
            }, Ct));

        Assert.All(outcomes, o => Assert.True(o.Succeeded, string.Join(", ", o.Failures.Select(f => f.Code))));
        var row = await RowAsync(booking.Id);
        Assert.Contains(row.StartUtc, new[] { thirteen, fifteen });
        Assert.Equal(1, await fixture.CreateContext().Claims.CountAsync(c => c.BookingId == booking.Id, Ct));
    }

    [Fact]
    public async Task The_move_write_is_one_update_whose_predicate_is_the_status()
    {
        // WHAT DISTINGUISHES THIS FROM A READ-THEN-WRITE. A SELECT-then-decide-then-UPDATE
        // produces identical rows in every single-threaded test; it is distinguishable only by
        // what is SENT. A mechanism guard, named as one, and falsifiable: loading the row and
        // saving it back fails this immediately, because that UPDATE's WHERE is the id alone.
        fixture.EnsureAvailable();
        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var booking = await PlaceAsync(resourceId, Ten);

        var interceptor = new CommandRecordingInterceptor();
        await using var context = fixture.CreateContext(interceptor);

        var moved = await new SqlBookingStore(context).MoveAsync(booking.Id, Interval(Fourteen, 60), Movable, Ct);
        Assert.True(moved.Succeeded);

        var updates = interceptor.Commands.Where(c => c.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)).ToList();
        var update = Assert.Single(updates);

        // The status is a PREDICATE of the statement, not a value it sets.
        var setClause = update[..update.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase)];
        var whereClause = update[update.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase)..];
        Assert.Contains("[Status]", whereClause, StringComparison.Ordinal);
        Assert.DoesNotContain("[Status]", setClause, StringComparison.Ordinal);

        // The interval columns, and nothing else.
        Assert.Contains("[StartUtc]", setClause, StringComparison.Ordinal);
        Assert.Contains("[EndUtc]", setClause, StringComparison.Ordinal);
        Assert.Contains("[TimeZoneId]", setClause, StringComparison.Ordinal);
        Assert.DoesNotContain("Booker", setClause, StringComparison.Ordinal);
        Assert.DoesNotContain("[Reference]", setClause, StringComparison.Ordinal);

        // And the application locks were taken before it.
        Assert.Contains(interceptor.Commands, c => c.Contains("sp_getapplock", StringComparison.Ordinal));
    }

    /// <summary>
    /// The real SQL store, with a hook at the one moment the contract is about: after the
    /// service has read and decided, before the store's move runs.
    /// </summary>
    private sealed class Interposing(IBookingStore inner, Func<Task> beforeMove) : IBookingStore
    {
        public Task<IReadOnlyList<ClaimInfo>> GetClaimsAsync(Guid resourceId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
            => inner.GetClaimsAsync(resourceId, fromUtc, toUtc, cancellationToken);

        public Task<IReadOnlyList<ClaimInfo>> GetClaimsAsync(IReadOnlyCollection<Guid> resourceIds, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
            => inner.GetClaimsAsync(resourceIds, fromUtc, toUtc, cancellationToken);

        public Task<DomainResult<Booking>> PlaceAsync(Booking booking, CancellationToken cancellationToken = default)
            => inner.PlaceAsync(booking, cancellationToken);

        public Task<Booking?> GetBookingAsync(Guid bookingId, CancellationToken cancellationToken = default)
            => inner.GetBookingAsync(bookingId, cancellationToken);

        public Task UpdateAsync(Booking booking, CancellationToken cancellationToken = default)
            => inner.UpdateAsync(booking, cancellationToken);

        public async Task<DomainResult> MoveAsync(Guid bookingId, BookingInterval newInterval, IReadOnlyCollection<BookingStatus> permittedFrom, CancellationToken cancellationToken = default)
        {
            await beforeMove();
            return await inner.MoveAsync(bookingId, newInterval, permittedFrom, cancellationToken);
        }

        public Task<bool> EraseBookerAsync(Guid bookingId, DateTimeOffset erasedUtc, CancellationToken cancellationToken = default)
            => inner.EraseBookerAsync(bookingId, erasedUtc, cancellationToken);

        public Task<IReadOnlyList<Guid>> GetBookingIdsDueForErasureAsync(DateTimeOffset cutoffUtc, int take, CancellationToken cancellationToken = default)
            => inner.GetBookingIdsDueForErasureAsync(cutoffUtc, take, cancellationToken);
    }

    /// <summary>
    /// The real resource store with its real closure store, over one context — so this
    /// test exercises the same hydration production does, closures included.
    /// </summary>
    private static SqlResourceStore ResourceStoreOver(UBookItDbContext context)
        => new(context, new SqlSiteClosureStore(context));

    /// <summary>The real service over the real SQL stores, with the hook installed on the booking store.</summary>
    private BookingService ServiceWith(Func<Task> beforeMove)
    {
        var settings = new SiteBookingSettings { TimeZoneId = "UTC" };
        return new BookingService(
            ResourceStoreOver(fixture.CreateContext()),
            new Interposing(new SqlBookingStore(fixture.CreateContext()), beforeMove),
            new FixedTimeProvider(Now),
            settings);
    }

    [Fact]
    public async Task A_cancel_committed_between_the_read_and_the_write_wins()
    {
        // THE INTERLEAVING THE STATUS PREDICATE EXISTS FOR. The service read a Confirmed
        // booking and decided to move it; a colleague cancelled it; the move's update runs.
        // A read-then-write would move a cancelled booking. The predicate moves nothing.
        fixture.EnsureAvailable();
        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var booking = await PlaceAsync(resourceId, Ten);

        var service = ServiceWith(async () =>
        {
            var (colleague, _) = fixture.CreateServices(Now);
            Assert.True((await colleague.CancelAsync(booking.Id, Ct)).Succeeded);
        });

        var moved = await service.MoveAsync(booking.Id, Fourteen, TimeSpan.FromHours(1), Ct);

        Assert.False(moved.Succeeded);
        Assert.Equal(FailureCodes.InvalidStatusTransition, Assert.Single(moved.Failures).Code);
        var row = await RowAsync(booking.Id);
        Assert.Equal((int)BookingStatus.Cancelled, row.Status);
        Assert.Equal(Ten, row.StartUtc);
    }

    [Fact]
    public async Task An_erasure_committed_between_the_read_and_the_write_survives_the_move()
    {
        fixture.EnsureAvailable();
        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var booking = await PlaceAsync(resourceId, Ten);
        var erasedAt = Now.AddDays(2);

        var service = ServiceWith(async () =>
        {
            await using var context = fixture.CreateContext();
            Assert.True(await new SqlBookingStore(context).EraseBookerAsync(booking.Id, erasedAt, Ct));
        });

        var moved = await service.MoveAsync(booking.Id, Fourteen, TimeSpan.FromHours(1), Ct);

        Assert.True(moved.Succeeded, string.Join(", ", moved.Failures.Select(f => f.Code)));
        var row = await RowAsync(booking.Id);
        Assert.Equal(Fourteen, row.StartUtc);
        Assert.Null(row.BookerName);
        Assert.Null(row.BookerEmail);
        Assert.Equal(erasedAt, row.BookerErasedUtc);
    }

    [Fact]
    public async Task A_move_through_the_service_over_SQL_releases_the_old_interval()
    {
        fixture.EnsureAvailable();
        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var (bookings, _) = fixture.CreateServices(Now);

        var placed = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = resourceId,
            Start = Ten,
            Duration = TimeSpan.FromHours(1),
            Booker = Booker.Create(null, "Moving Morgan", "morgan@example.com").Value,
        }, Ct);
        Assert.True(placed.Succeeded);

        var moved = await bookings.MoveAsync(placed.Value.Id, Fourteen, TimeSpan.FromHours(1), Ct);
        Assert.True(moved.Succeeded, string.Join(", ", moved.Failures.Select(f => f.Code)));

        // 10:00 is free again; 14:00 is not.
        var atTen = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = resourceId,
            Start = Ten,
            Duration = TimeSpan.FromHours(1),
            Booker = Booker.Create(null, "Next Nia", "nia@example.com").Value,
        }, Ct);
        Assert.True(atTen.Succeeded);

        var atFourteen = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = resourceId,
            Start = Fourteen,
            Duration = TimeSpan.FromHours(1),
            Booker = Booker.Create(null, "Late Lee", "lee@example.com").Value,
        }, Ct);
        Assert.Equal(FailureCodes.Conflict, Assert.Single(atFourteen.Failures).Code);
    }
}
