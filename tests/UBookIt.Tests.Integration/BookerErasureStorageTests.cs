using Microsoft.EntityFrameworkCore;
using UBookIt.Core;
using UBookIt.Core.Bookings;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// That erasure reaches the database — the half no unit test can establish.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every assertion here reads the row back in a fresh context.</b> The aggregate handed to
/// the store carries the erasure whether or not a row was written, so asserting against it
/// proves only that the object changed. <c>SqlBookingStore.UpdateAsync</c> wrote nothing but
/// <c>Status</c> until this change, which is exactly the defect that shape of test cannot see:
/// the call succeeds, the returned booking looks erased, and the person's name is still in the
/// table.
/// </para>
/// <para>
/// A fresh context rather than the same one, because EF's change tracker would answer from
/// memory and reproduce the same blind spot one layer down.
/// </para>
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class BookerErasureStorageTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Now = new(2026, 9, 5, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Start = new(2026, 9, 10, 10, 0, 0, TimeSpan.Zero);

    private async Task<Booking> PlaceAsync()
    {
        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var booking = Seed.ConfirmedBooking(resourceId, Start, TimeSpan.FromHours(1));

        await using var context = fixture.CreateContext();
        var store = new SqlBookingStore(context);
        var placed = await store.PlaceAsync(booking, Ct);

        Assert.True(placed.Succeeded);
        return placed.Value;
    }

    [Fact]
    public async Task An_erasure_reaches_the_database()
    {
        fixture.EnsureAvailable();

        var booking = await PlaceAsync();

        // The details are in the table first, so the assertions below are observing a change
        // rather than a state the row was always in.
        await using (var before = fixture.CreateContext())
        {
            var row = await before.Bookings.SingleAsync(b => b.Id == booking.Id, Ct);
            Assert.Equal("Integration Tester", row.BookerName);
            Assert.Equal("integration@example.com", row.BookerEmail);
            Assert.Equal("01234 567890", row.BookerPhone);
            Assert.Null(row.BookerErasedUtc);
        }

        var (bookings, _) = fixture.CreateServices(Now);
        var erased = await bookings.EraseBookerAsync(booking.Id, Ct);
        Assert.True(erased.Succeeded);

        await using var after = fixture.CreateContext();
        var stored = await after.Bookings.SingleAsync(b => b.Id == booking.Id, Ct);

        Assert.Null(stored.BookerName);
        Assert.Null(stored.BookerEmail);
        Assert.Null(stored.BookerPhone);
        Assert.Null(stored.MemberKey);
        Assert.Equal(Now, stored.BookerErasedUtc);
    }

    [Fact]
    public async Task Erasure_keeps_the_row_its_reference_and_its_claims()
    {
        fixture.EnsureAvailable();

        var booking = await PlaceAsync();

        var (bookings, _) = fixture.CreateServices(Now);
        Assert.True((await bookings.EraseBookerAsync(booking.Id, Ct)).Succeeded);

        await using var context = fixture.CreateContext();
        var stored = await context.Bookings
            .Include(b => b.Claims)
            .SingleAsync(b => b.Id == booking.Id, Ct);

        // Anonymisation, not deletion. A cascade or trigger that removed the row — or its
        // claims — would hand sold time back to availability, and nothing on the screen
        // would say so.
        Assert.Equal(booking.Reference.Value, stored.Reference);
        Assert.Equal(booking.Interval.StartUtc, stored.StartUtc);
        Assert.Equal(booking.Interval.EndUtc, stored.EndUtc);
        Assert.Equal((int)BookingStatus.Confirmed, stored.Status);
        Assert.Equal(booking.CreatedUtc, stored.CreatedUtc);
        Assert.Equal(booking.Claims.Single().ResourceId, stored.Claims.Single().ResourceId);
    }

    [Fact]
    public async Task An_erased_booking_round_trips_through_the_store()
    {
        fixture.EnsureAvailable();

        var booking = await PlaceAsync();

        var (bookings, _) = fixture.CreateServices(Now);
        Assert.True((await bookings.EraseBookerAsync(booking.Id, Ct)).Succeeded);

        await using var context = fixture.CreateContext();
        var reloaded = await new SqlBookingStore(context).GetBookingAsync(booking.Id, Ct);

        Assert.NotNull(reloaded);

        // Rehydration accepts the erased state rather than refusing a booking for having had
        // its personal data removed.
        Assert.True(reloaded.Booker.IsErased);
        Assert.Null(reloaded.Booker.Contact);
        Assert.Null(reloaded.Booker.MemberKey);
        Assert.Equal(Now, reloaded.Booker.ErasedUtc);
        Assert.Equal(booking.Reference.Value, reloaded.Reference.Value);
    }

    [Fact]
    public async Task An_erased_booking_still_blocks_its_time_in_storage()
    {
        fixture.EnsureAvailable();

        var booking = await PlaceAsync();
        var resourceId = booking.Claims.Single().ResourceId;

        var (bookings, _) = fixture.CreateServices(Now);
        Assert.True((await bookings.EraseBookerAsync(booking.Id, Ct)).Succeeded);

        await using var context = fixture.CreateContext();
        var claims = await new SqlBookingStore(context)
            .GetClaimsAsync(resourceId, Start, Start.AddHours(1), Ct);

        // The claim is still there AND still blocking. Read through the availability path
        // rather than by counting rows, because "the row exists" and "the calendar still
        // knows about it" are different facts and only the second one matters to a visitor.
        var claim = Assert.Single(claims);
        Assert.Equal(booking.Id, claim.BookingId);
        Assert.Equal(BookingStatus.Confirmed, claim.Status);
    }

    [Fact]
    public async Task Erasing_twice_leaves_the_first_instant_in_the_database()
    {
        fixture.EnsureAvailable();

        var booking = await PlaceAsync();

        var (first, _) = fixture.CreateServices(Now);
        Assert.True((await first.EraseBookerAsync(booking.Id, Ct)).Succeeded);

        // A different clock, so an implementation that re-erased would write a visibly
        // different instant rather than the same one by luck.
        var (second, _) = fixture.CreateServices(Now.AddDays(30));
        Assert.True((await second.EraseBookerAsync(booking.Id, Ct)).Succeeded);

        await using var context = fixture.CreateContext();
        var stored = await context.Bookings.SingleAsync(b => b.Id == booking.Id, Ct);

        Assert.Equal(Now, stored.BookerErasedUtc);
    }

    [Fact]
    public async Task A_cancellation_holding_a_stale_booking_cannot_un_erase_it()
    {
        // THE INTERLEAVING THAT UN-ERASED A PERSON.
        //
        // Every other test in this file erases LAST, which is why the whole suite was green
        // while this was broken: nothing ever wrote a booking AFTER an erasure. Two ordinary
        // backoffice requests are enough — an operator opens cancel, a colleague erases, the
        // cancel completes and puts the name back — and both report success.
        //
        // The stale aggregate is obtained the way CancelAsync obtains one: read the booking
        // BEFORE the erasure, then hand it to the store afterwards. Not simulated with a
        // hand-built object, because the fault is that a real read-modify-write can straddle
        // an erasure.
        fixture.EnsureAvailable();

        var booking = await PlaceAsync();

        await using var staleContext = fixture.CreateContext();
        var stale = await new SqlBookingStore(staleContext).GetBookingAsync(booking.Id, Ct);
        Assert.NotNull(stale);
        Assert.NotNull(stale.Booker.Contact);

        var (bookings, _) = fixture.CreateServices(Now);
        Assert.True((await bookings.EraseBookerAsync(booking.Id, Ct)).Succeeded);

        // The cancellation proceeds on the stale aggregate, exactly as CancelAsync would.
        Assert.True(stale.Cancel().Succeeded);
        await new SqlBookingStore(staleContext).UpdateAsync(stale, Ct);

        await using var after = fixture.CreateContext();
        var stored = await after.Bookings.SingleAsync(b => b.Id == booking.Id, Ct);

        // The person stays gone.
        Assert.Null(stored.BookerName);
        Assert.Null(stored.BookerEmail);
        Assert.Null(stored.BookerPhone);
        Assert.Null(stored.MemberKey);
        Assert.Equal(Now, stored.BookerErasedUtc);

        // And the cancellation still happened — absorbing the booker write must not quietly
        // swallow the status change the caller actually asked for.
        Assert.Equal((int)BookingStatus.Cancelled, stored.Status);
    }

    [Fact]
    public async Task Erasing_an_already_erased_booking_keeps_the_first_instant_in_storage()
    {
        // The other side of "the stored erasure wins": the guard skips the booker columns on
        // a row that already records an erasure, so re-erasing must not be able to move the
        // instant either. Asserted through the store rather than the aggregate, because the
        // aggregate's own idempotence is already covered and it is the ROW that must not
        // drift.
        fixture.EnsureAvailable();

        var booking = await PlaceAsync();

        var (first, _) = fixture.CreateServices(Now);
        Assert.True((await first.EraseBookerAsync(booking.Id, Ct)).Succeeded);

        await using var reread = fixture.CreateContext();
        var erased = await new SqlBookingStore(reread).GetBookingAsync(booking.Id, Ct);
        Assert.NotNull(erased);
        Assert.True(erased.Booker.IsErased);

        // A fresh aggregate, already erased, written again through the same path.
        await new SqlBookingStore(reread).UpdateAsync(erased, Ct);

        await using var after = fixture.CreateContext();
        var stored = await after.Bookings.SingleAsync(b => b.Id == booking.Id, Ct);

        Assert.Equal(Now, stored.BookerErasedUtc);
        Assert.Null(stored.BookerName);
    }

    [Fact]
    public async Task The_management_list_reports_an_erased_booking_as_erased()
    {
        fixture.EnsureAvailable();

        var booking = await PlaceAsync();

        var (bookings, _) = fixture.CreateServices(Now);
        Assert.True((await bookings.EraseBookerAsync(booking.Id, Ct)).Succeeded);

        await using var context = fixture.CreateContext();

        // Scoped to this test's own resource. The fixture's database is shared across the
        // collection, so every test in this class books the same window on a different room —
        // an unscoped query returns all of them and `Single` fails for a reason that has
        // nothing to do with erasure.
        var query = BookingQuery.Create(
            Start.AddDays(-1),
            Start.AddDays(1),
            new SiteBookingSettings { TimeZoneId = "UTC" },
            [BookingStatus.Confirmed],
            [booking.Claims.Single().ResourceId],
            BookingQuery.DefaultSkip,
            BookingQuery.DefaultTake);

        Assert.True(query.Succeeded);

        var page = await new SqlBookingManagementStore(context).ListAsync(query.Value, Ct);
        var row = Assert.Single(page.Items);

        // The read port projects the columns independently of SqlBookingStore, so this is a
        // second mapping that could disagree with the first about what a NULL name means.
        Assert.True(row.Booker.IsErased);
        Assert.Null(row.Booker.Contact);
        Assert.Equal(Now, row.Booker.ErasedUtc);

        // And the row is still a usable row: withheld or erased, an operator identifies it
        // by its reference.
        Assert.Equal(booking.Reference.Value, row.Reference.Value);
    }
}
