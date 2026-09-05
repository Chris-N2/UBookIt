using System.Reflection;
using UBookIt.Backoffice.Mapping;
using UBookIt.Backoffice.Models;
using UBookIt.Core.Bookings;
using UBookIt.Core.Availability;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Stores;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Erasing a booking's personal data: the domain state, the verb, and how the three
/// conditions reach the wire.
/// </summary>
/// <remarks>
/// The storage half — that <c>UpdateAsync</c> actually writes the booker columns — is not here
/// and cannot be: asserting against the returned aggregate proves only that the aggregate
/// changed, which it does whether or not a row was written. That check lives in the
/// integration suite, against a re-read in a fresh context.
/// </remarks>
public class BookerErasureTests
{
    private static readonly DateTimeOffset ErasedAt = new(2026, 9, 4, 22, 15, 0, TimeSpan.Zero);

    private static BookingInterval Interval() => BookingInterval.Create(
        TestData.Utc(TestData.BaseDate, "09:00"),
        TestData.Utc(TestData.BaseDate, "10:00"),
        TestData.LondonZoneId).Value;

    private static Booking Booking(BookingStatus status = BookingStatus.Confirmed, Guid? memberKey = null)
        => Core.Bookings.Booking.Rehydrate(
            Guid.NewGuid(),
            References.Any(),
            Interval(),
            TestData.Booker(memberKey),
            [new ResourceClaim(Guid.NewGuid())],
            status,
            TestData.Now,
            new ServiceAttribution(Guid.NewGuid(), "Initial Consultation")).Value;

    [Fact]
    public void An_erased_booker_cannot_be_given_contact_details_back()
    {
        // The `booker-erasure` capability requires that no operation returns an erased booker
        // to carrying contact details, and that the neither-state is unconstructible. Both hold
        // ONLY because the constructor is private and no property has an `init` accessor —
        // which is a thinner thread than it looks. `Booker` is a record, so adding `init` to
        // `Contact` is an edit that reads as ordinary modernisation, produces no warning and
        // breaks no behavioural test, and it immediately makes
        //
        //     Booker.Erased(t) with { Contact = new BookerContact(...) }
        //
        // legal from any assembly. Un-erasure would be back, silently, in a package whose
        // documentation promises there is no way back.
        //
        // Asserted over the construction surface rather than by attempting the mutation,
        // because the mutation would not compile today — a test that cannot be written is not
        // the same as a guarantee that cannot be broken.
        var settable = typeof(Booker)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod is not null)
            .Select(property => property.Name)
            .ToList();

        Assert.Empty(settable);

        var publicConstructors = typeof(Booker)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        Assert.Empty(publicConstructors);
    }

    [Fact]
    public void The_read_ports_booker_cannot_be_given_contact_details_back_either()
    {
        // SummaryBooker has the identical shape and carries the same two-state guarantee
        // across the read port, so it is open to the identical edit: add `init` to `Contact`
        // and `with { Contact = ... }` reconstitutes a person on a row the endpoint is about
        // to render. Guarded on the same terms as Booker rather than left as the one of the
        // pair somebody thought of.
        var settable = typeof(SummaryBooker)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod is not null)
            .Select(property => property.Name)
            .ToList();

        Assert.Empty(settable);
        Assert.Empty(typeof(SummaryBooker).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void An_erased_booker_carries_nothing_of_the_person()
    {
        var booker = Booker.Erased(ErasedAt);

        Assert.Null(booker.Contact);
        Assert.Null(booker.MemberKey);
        Assert.True(booker.IsErased);
        Assert.Equal(ErasedAt, booker.ErasedUtc);
    }

    [Fact]
    public void Placement_never_produces_an_erased_booker()
    {
        // The validation is unchanged, and this is the half of "two states" that the erased
        // factory cannot demonstrate: everything the ordinary pathway builds is in the other
        // state, whatever it was given.
        var booker = Booker.Create(Guid.NewGuid(), "Ada Lovelace", "ada@example.com", "01234 567890").Value;

        Assert.False(booker.IsErased);
        Assert.Null(booker.ErasedUtc);
        Assert.NotNull(booker.Contact);
        Assert.Equal("Ada Lovelace", booker.Contact.Name);
    }

    [Fact]
    public void Erasing_removes_the_member_key_as_well_as_the_contact_details()
    {
        // A member key identifies a person as reliably as an email address. This is asserted
        // separately because it is the piece an implementation is most likely to leave behind:
        // it lives on the booker rather than inside the contact object, so a change that
        // nulled only the contact would look complete and would not be.
        var booking = Booking(memberKey: Guid.NewGuid());
        Assert.NotNull(booking.Booker.MemberKey);

        booking.EraseBooker(ErasedAt);

        Assert.Null(booking.Booker.MemberKey);
        Assert.Null(booking.Booker.Contact);
    }

    [Fact]
    public void Erasing_changes_the_booker_and_nothing_else()
    {
        var booking = Booking();

        var id = booking.Id;
        var reference = booking.Reference.Value;
        var interval = booking.Interval;
        var status = booking.Status;
        var created = booking.CreatedUtc;
        var claims = booking.Claims.Select(claim => claim.ResourceId).ToList();
        var service = booking.Service;

        booking.EraseBooker(ErasedAt);

        Assert.Equal(id, booking.Id);
        Assert.Equal(reference, booking.Reference.Value);
        Assert.Equal(interval, booking.Interval);
        Assert.Equal(status, booking.Status);
        Assert.Equal(created, booking.CreatedUtc);
        Assert.Equal(claims, booking.Claims.Select(claim => claim.ResourceId));
        Assert.Equal(service, booking.Service);
    }

    [Theory]
    [InlineData(BookingStatus.Requested)]
    [InlineData(BookingStatus.Confirmed)]
    public void An_erased_booking_still_blocks_its_time(BookingStatus status)
    {
        // The whole reason erasure is anonymisation rather than deletion. A booking that
        // stopped blocking when its booker was erased would silently return sold time to
        // availability — and nothing on the screen would say so.
        var booking = Booking(status);
        Assert.True(booking.IsBlocking);

        booking.EraseBooker(ErasedAt);

        Assert.True(booking.IsBlocking);
    }

    [Fact]
    public void Erasing_twice_keeps_the_first_instant()
    {
        // Deliberately unlike cancelling, which refuses a second attempt. Overwriting the
        // instant would also misreport when the data actually left.
        var booking = Booking();

        booking.EraseBooker(ErasedAt);
        booking.EraseBooker(ErasedAt.AddDays(30));

        Assert.Equal(ErasedAt, booking.Booker.ErasedUtc);
    }

    private static Resource BookableResource()
        => Resource.Create(
            ResourceTypes.Room,
            "Meeting Room A",
            directlyBookable: true,
            availability: TestData.Config(
                TestData.Weekly("09:00", "17:00", TestData.BaseDate.DayOfWeek),
                constraints: BookingConstraints.Create(
                    granularity: TimeSpan.FromMinutes(30),
                    minDuration: TimeSpan.FromMinutes(30),
                    maxDuration: TimeSpan.FromMinutes(480)).Value)).Value;

    [Fact]
    public async Task The_verb_takes_its_instant_from_the_injected_clock()
    {
        // The clock is set to a date well before the booking it erases, so a hard-coded
        // "now" or a call to DateTimeOffset.UtcNow would produce a visibly different answer
        // rather than one that happens to match.
        var resource = BookableResource();
        var (bookings, _, _) = TestData.Services(resource, nowUtc: ErasedAt);

        var placed = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = resource.Id,
            Start = TestData.Utc(TestData.BaseDate, "10:00"),
            Duration = TimeSpan.FromMinutes(60),
            Booker = TestData.Booker(),
        });

        Assert.True(placed.Succeeded);
        Assert.NotNull(placed.Value.Booker.Contact);

        var erased = await bookings.EraseBookerAsync(placed.Value.Id);

        Assert.True(erased.Succeeded);
        Assert.Equal(ErasedAt, erased.Value.Booker.ErasedUtc);
        Assert.Null(erased.Value.Booker.Contact);
        Assert.Null(erased.Value.Booker.MemberKey);
    }

    [Fact]
    public async Task Erasing_the_same_booking_twice_succeeds_and_changes_nothing()
    {
        // Idempotence at the service level, not just on the aggregate: this is the property
        // the retention job will depend on, and the one that makes a retry after a timeout
        // safe rather than a second, differently-dated erasure.
        var resource = BookableResource();
        var (bookings, _, _) = TestData.Services(resource, nowUtc: ErasedAt);

        var placed = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = resource.Id,
            Start = TestData.Utc(TestData.BaseDate, "10:00"),
            Duration = TimeSpan.FromMinutes(60),
            Booker = TestData.Booker(),
        });

        Assert.True((await bookings.EraseBookerAsync(placed.Value.Id)).Succeeded);

        var again = await bookings.EraseBookerAsync(placed.Value.Id);

        Assert.True(again.Succeeded);
        Assert.Equal(ErasedAt, again.Value.Booker.ErasedUtc);
    }

    [Fact]
    public async Task Erasing_goes_through_the_erase_write_and_never_through_the_status_write()
    {
        // THE SHAPE THAT THE LAST THREE DEFECTS ALL CAME FROM, pinned directly.
        //
        // Erasure used to read a booking, mutate the aggregate and hand the whole thing to
        // UpdateAsync. Everything that aggregate carried was then written from a copy that
        // could already be stale — which produced, in consecutive reviews, a cancellation
        // restoring an erased person, and then an erasure reverting a committed cancellation
        // and re-blocking a released slot.
        //
        // The behavioural tests could not see the difference: a single-threaded test cannot
        // stage the interleaving, so a read-modify-write implementation passes all of them.
        // What distinguishes the designs is WHICH WRITE the verb uses, so that is asserted —
        // and asserted in both directions, because "it calls erase" would still pass if it
        // also called the status write.
        var resource = BookableResource();
        var resources = new InMemoryResourceStore().Add(resource);
        var store = new InMemoryBookingStore();
        var bookings = new BookingService(
            resources, store, new FixedTimeProvider(ErasedAt), TestData.Settings);

        var placed = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = resource.Id,
            Start = TestData.Utc(TestData.BaseDate, "10:00"),
            Duration = TimeSpan.FromMinutes(60),
            Booker = TestData.Booker(),
        });

        Assert.True(placed.Succeeded);

        var updatesBefore = store.UpdateCount;

        Assert.True((await bookings.EraseBookerAsync(placed.Value.Id)).Succeeded);

        Assert.Equal(1, store.EraseCount);

        // It must not ALSO write the status. Writing a status the caller never changed is
        // precisely how the erasure reverted a cancellation.
        Assert.Equal(updatesBefore, store.UpdateCount);
    }

    [Fact]
    public async Task Cancelling_goes_through_the_status_write_and_never_through_the_erase_write()
    {
        // The mirror, so the pair says the two verbs use disjoint writes rather than merely
        // that one of them uses the right one.
        var resource = BookableResource();
        var resources = new InMemoryResourceStore().Add(resource);
        var store = new InMemoryBookingStore();
        var bookings = new BookingService(
            resources, store, new FixedTimeProvider(ErasedAt), TestData.Settings);

        var placed = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = resource.Id,
            Start = TestData.Utc(TestData.BaseDate, "10:00"),
            Duration = TimeSpan.FromMinutes(60),
            Booker = TestData.Booker(),
        });

        Assert.True((await bookings.CancelAsync(placed.Value.Id)).Succeeded);

        Assert.Equal(0, store.EraseCount);
        Assert.True(store.UpdateCount > 0);
    }

    [Fact]
    public async Task Erasing_a_booking_that_does_not_exist_fails()
    {
        var (bookings, _, _) = TestData.Services(BookableResource());

        var erased = await bookings.EraseBookerAsync(Guid.NewGuid());

        Assert.False(erased.Succeeded);
        Assert.Equal(FailureCodes.BookingNotFound, Assert.Single(erased.Failures).Code);
    }

    // ---- How the three conditions reach the wire -------------------------------------------

    // Fixed ids, and a single shared reference. The comparison below is "these two rows differ
    // ONLY in the booker", which is worth nothing if every other field is freshly generated per
    // call — it would fail for the wrong reason, or pass for one.
    private static readonly Guid SummaryBookingId = Guid.NewGuid();
    private static readonly Guid SummaryResourceId = Guid.NewGuid();
    private static readonly Guid SummaryServiceId = Guid.NewGuid();
    private static readonly BookingReference SummaryReference = References.Any();

    private static BookingSummary Summary(SummaryBooker booker)
        => new(
            SummaryBookingId,
            SummaryReference,
            Interval(),
            BookingStatus.Confirmed,
            TestData.Now,
            booker,
            [new BookedResource(SummaryResourceId, "Meeting Room A")],
            new ServiceAttribution(SummaryServiceId, "Initial Consultation"));

    private static BookingSummary ErasedSummary() => Summary(SummaryBooker.Erased(ErasedAt));

    private static BookingSummary ShownSummary()
        => Summary(SummaryBooker.Of(new SummaryContact("Ada Lovelace", "ada@example.com")));

    // Two facts rather than a theory: BookerVisibility is internal, and a public test method
    // cannot take one as a parameter. The pair is the point — the assertion is that the answer
    // does not depend on the visibility — so they share one helper rather than being written
    // twice and drifting.
    private static void AssertReadsAsErased(BookerVisibility visibility)
    {
        // The precedence rule, and the reason it is a rule: telling a caller without
        // sensitive-data access that details are "withheld" sends them to a colleague who
        // cannot help either, because there is nothing left to show anybody.
        var model = BookingModelMapper.ToModel(ErasedSummary(), visibility);

        Assert.Equal(BookerConditions.Erased, model.Booker.Condition);
        Assert.Equal(ErasedAt, model.Booker.ErasedUtc);
        Assert.Null(model.Booker.Contact);
    }

    [Fact]
    public void An_erased_booking_reads_as_erased_to_a_permitted_caller()
        => AssertReadsAsErased(BookerVisibility.Shown);

    [Fact]
    public void An_erased_booking_reads_as_erased_to_a_caller_without_sensitive_data_access()
        => AssertReadsAsErased(BookerVisibility.Withheld);

    [Fact]
    public void Erased_and_withheld_are_different_answers()
    {
        // Asserted as a comparison rather than as two independent facts. A mapper that
        // reported both as the same condition would satisfy a test that only checked each
        // one carried no contact details — which is what makes the third state worth having.
        var erased = BookingModelMapper.ToModel(ErasedSummary(), BookerVisibility.Withheld);
        var withheld = BookingModelMapper.ToModel(ShownSummary(), BookerVisibility.Withheld);

        Assert.NotEqual(erased.Booker.Condition, withheld.Booker.Condition);
        Assert.Equal(BookerConditions.Erased, erased.Booker.Condition);
        Assert.Equal(BookerConditions.Withheld, withheld.Booker.Condition);

        // Neither carries details, so the condition is the only thing distinguishing them —
        // which is precisely why it has to be stated rather than inferred.
        Assert.Null(erased.Booker.Contact);
        Assert.Null(withheld.Booker.Contact);

        // And only the erased one says when.
        Assert.NotNull(erased.Booker.ErasedUtc);
        Assert.Null(withheld.Booker.ErasedUtc);
    }

    [Fact]
    public void Everything_that_is_not_personal_data_survives_erasure()
    {
        // The mirror of the withholding test. Erasure removes the person; a row that came back
        // sparser than that would be unusable for the operator who still has to answer the
        // telephone about it.
        var shown = BookingModelMapper.ToModel(ShownSummary(), BookerVisibility.Shown);
        var erased = BookingModelMapper.ToModel(ErasedSummary(), BookerVisibility.Shown);

        Assert.Equal(shown.StartUtc, erased.StartUtc);
        Assert.Equal(shown.EndUtc, erased.EndUtc);
        Assert.Equal(shown.TimeZoneId, erased.TimeZoneId);
        Assert.Equal(shown.Status, erased.Status);
        Assert.Equal(shown.CreatedUtc, erased.CreatedUtc);

        Assert.Equal(
            shown.Resources.Select(resource => (resource.ResourceId, resource.DisplayName)),
            erased.Resources.Select(resource => (resource.ResourceId, resource.DisplayName)));

        Assert.NotNull(erased.Service);
        Assert.Equal(shown.Service!.DisplayName, erased.Service.DisplayName);

        // The reference above all: it is not personal data, and after erasure it is the only
        // thing left to call the booking by.
        Assert.False(string.IsNullOrWhiteSpace(erased.Reference));
    }

    [Fact]
    public void The_booker_member_is_never_absent_in_any_condition()
    {
        BookingModel[] all =
        [
            BookingModelMapper.ToModel(ShownSummary(), BookerVisibility.Shown),
            BookingModelMapper.ToModel(ShownSummary(), BookerVisibility.Withheld),
            BookingModelMapper.ToModel(ErasedSummary(), BookerVisibility.Shown),
        ];

        Assert.All(all, model => Assert.NotNull(model.Booker));
        Assert.All(all, model => Assert.False(string.IsNullOrWhiteSpace(model.Booker.Condition)));

        // All three conditions are reachable, so this is not passing because two of the rows
        // happen to be the same.
        Assert.Equal(3, all.Select(model => model.Booker.Condition).Distinct().Count());
    }
}
