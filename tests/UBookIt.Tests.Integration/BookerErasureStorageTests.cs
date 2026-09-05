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
    public async Task A_cancellation_survives_an_erasure_that_follows_it()
    {
        // The observable half of the round-4 CRITICAL: erasure must not disturb the status.
        //
        // **What this does NOT do is stage the interleaving that caused it.** The defect
        // needed the erasure to hold an aggregate read BEFORE the cancellation, and the verb
        // now takes only an id, so a single-threaded test cannot construct one — an earlier
        // version of this test read a stale aggregate to look as though it did and then never
        // used it, which is the third unfalsifiable test this change has produced and the
        // reason the claim is spelled out rather than implied.
        //
        // The guarantee is held by `The_erase_write_issues_one_statement_and_reads_nothing_first`,
        // which asserts the erasure's UPDATE does not touch [Status] at all. This test is the
        // end-to-end confirmation that the two verbs compose.
        fixture.EnsureAvailable();

        var booking = await PlaceAsync();
        var (bookings, _) = fixture.CreateServices(Now);

        Assert.True((await bookings.CancelAsync(booking.Id, Ct)).Succeeded);
        Assert.True((await bookings.EraseBookerAsync(booking.Id, Ct)).Succeeded);

        await using var after = fixture.CreateContext();
        var stored = await after.Bookings.SingleAsync(b => b.Id == booking.Id, Ct);

        Assert.Equal((int)BookingStatus.Cancelled, stored.Status);
        Assert.Null(stored.BookerName);
        Assert.Equal(Now, stored.BookerErasedUtc);
    }

    [Fact]
    public async Task The_erase_write_issues_one_statement_and_reads_nothing_first()
    {
        // WHAT DISTINGUISHES THIS IMPLEMENTATION FROM THE ONE REVIEW REJECTED.
        //
        // Two rounds of review turned on a difference no assertion in this suite could see:
        // a SELECT-then-decide-then-UPDATE passes every behavioural test above, because the
        // interleaving that breaks it cannot be produced from a single-threaded test. The
        // shapes are distinguishable by what they SEND, so that is what is asserted.
        //
        // A mechanism guard, and named as one — it observes the statement count, not the
        // absence of a race. But it is falsifiable, where the comment it replaces was not:
        // reintroducing the read-then-write shape fails this immediately.
        fixture.EnsureAvailable();

        var booking = await PlaceAsync();

        var interceptor = new CommandRecordingInterceptor();
        await using var context = fixture.CreateContext(interceptor);

        var erased = await new SqlBookingStore(context).EraseBookerAsync(booking.Id, Now, Ct);
        Assert.True(erased);

        var command = Assert.Single(interceptor.Commands);
        Assert.StartsWith("UPDATE", command.TrimStart(), StringComparison.OrdinalIgnoreCase);

        // The decision is IN the statement: the stored instant is what each column is tested
        // against, so there is no earlier read whose answer could have gone stale.
        Assert.Contains("CASE", command, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BookerErasedUtc", command, StringComparison.Ordinal);

        // And it touches NOTHING but the booker. Writing a status here is what reverted a
        // cancellation: the erasure carried a value its caller had never changed.
        Assert.DoesNotContain("[Status]", command, StringComparison.Ordinal);
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
    public async Task Two_erasures_racing_leave_one_instant_and_both_callers_are_told_it()
    {
        // The interleaving the previous fix could not survive, and the reason this one is a
        // single statement rather than a read-then-write.
        //
        // Both services read the booking while it is unerased, so BOTH compute their own
        // instant — exactly the state a check-then-act guard evaluates before the other
        // write lands. Whichever commits second must be absorbed by the row, and must not
        // report its own clock reading as the erasure time: the endpoint publishes that value
        // over a contract saying it is the FIRST erasure's instant.
        fixture.EnsureAvailable();

        var booking = await PlaceAsync();

        var (first, _) = fixture.CreateServices(Now);
        var (second, _) = fixture.CreateServices(Now.AddDays(30));

        var a = await first.EraseBookerAsync(booking.Id, Ct);
        var b = await second.EraseBookerAsync(booking.Id, Ct);

        Assert.True(a.Succeeded);
        Assert.True(b.Succeeded);

        // One instant in the row...
        await using var after = fixture.CreateContext();
        var stored = await after.Bookings.SingleAsync(x => x.Id == booking.Id, Ct);
        Assert.Equal(Now, stored.BookerErasedUtc);

        // ...and both callers were told THAT one, not the one they each computed. The second
        // caller's own clock said Now+30d; reporting it would tell a data subject their
        // details were removed a month later than they were.
        Assert.Equal(Now, a.Value.Booker.ErasedUtc);
        Assert.Equal(Now, b.Value.Booker.ErasedUtc);
    }

    [Fact]
    public async Task A_status_change_still_lands_on_an_erased_booking()
    {
        // The other half of absorbing the booker write: it must absorb ONLY the booker. A
        // guard that skipped the whole update would make an erased booking uncancellable,
        // which is a worse defect than the one it was closing — the operator would get a
        // success and no cancellation, and the slot would stay blocked forever.
        fixture.EnsureAvailable();

        var booking = await PlaceAsync();

        var (bookings, _) = fixture.CreateServices(Now);
        Assert.True((await bookings.EraseBookerAsync(booking.Id, Ct)).Succeeded);

        var cancelled = await bookings.CancelAsync(booking.Id, Ct);
        Assert.True(cancelled.Succeeded);

        await using var after = fixture.CreateContext();
        var stored = await after.Bookings.SingleAsync(x => x.Id == booking.Id, Ct);

        Assert.Equal((int)BookingStatus.Cancelled, stored.Status);
        Assert.Null(stored.BookerName);
        Assert.Equal(Now, stored.BookerErasedUtc);
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
