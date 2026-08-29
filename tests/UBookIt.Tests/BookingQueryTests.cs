using UBookIt.Core;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Stores;

namespace UBookIt.Tests;

/// <summary>
/// The window guard, which lives in the type rather than in front of it.
/// <para>
/// A service that validated before delegating would be something a caller could route
/// around, and would also be a Core service depending on a management store — which the
/// <c>bookings</c> capability forbids so the read ports stay the only pathway anonymous
/// delivery traffic reaches storage through. Making an invalid query unrepresentable
/// avoids both, and is a stronger guarantee than one that needs cooperation.
/// </para>
/// </summary>
public class BookingQueryTests
{
    private static readonly DateTimeOffset From = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    private static SiteBookingSettings Settings(int maxDays = 31)
        => new() { TimeZoneId = "UTC", MaxQueryRangeDays = maxDays };

    [Fact]
    public void A_window_at_the_maximum_is_allowed()
    {
        // Exactly the maximum, not one under it: an off-by-one in the comparison is only
        // visible at the boundary itself.
        var result = BookingQuery.Create(From, From.AddDays(31), Settings(maxDays: 31));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void A_window_a_whole_day_past_the_maximum_is_refused()
    {
        var result = BookingQuery.Create(From, From.AddDays(32), Settings(maxDays: 31));

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.DateRangeTooLarge, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void A_partial_day_over_the_maximum_is_allowed()
    {
        // Changed deliberately, and it relaxes what this guard previously refused.
        //
        // The earlier rule compared raw elapsed time, so a window one tick over the
        // maximum failed. That looked tidy and was wrong once a caller expressed a window
        // in local DATES: a site-local day is 25 hours across a fall-back transition, so a
        // calendar month resolves to slightly more than a whole number of days. With the
        // default guardrail of 31, "show me October" was refused on every European site,
        // every year.
        //
        // The guard exists to stop unbounded queries — a day-by-day walk over an
        // arbitrary span — and an hour either way is not what it protects against. It now
        // counts whole days, so a partial day over does not count.
        foreach (var over in new[] { TimeSpan.FromTicks(1), TimeSpan.FromHours(1), TimeSpan.FromHours(23) })
        {
            var result = BookingQuery.Create(From, From.AddDays(31) + over, Settings(maxDays: 31));

            Assert.True(
                result.Succeeded,
                $"A window of 31 days plus {over} should be within a 31-day guardrail.");
        }
    }

    [Fact]
    public void A_window_that_does_not_run_forwards_is_refused_with_its_own_code()
    {
        // Distinct from the over-wide code, so a caller can tell "you asked for nothing"
        // from "you asked for too much".
        foreach (var to in new[] { From, From.AddSeconds(-1) })
        {
            var result = BookingQuery.Create(From, to, Settings());

            Assert.False(result.Succeeded);
            Assert.Equal(FailureCodes.DateRangeInvalid, Assert.Single(result.Failures).Code);
        }
    }

    [Fact]
    public void The_maximum_is_the_site_setting_rather_than_a_constant()
    {
        // A hardcoded 31 passes every test above against the default settings. Varying
        // the setting is what proves the guard reads it.
        Assert.True(BookingQuery.Create(From, From.AddDays(60), Settings(maxDays: 90)).Succeeded);
        Assert.False(BookingQuery.Create(From, From.AddDays(60), Settings(maxDays: 7)).Succeeded);
    }

    [Fact]
    public void An_invalid_window_cannot_be_represented_at_all()
    {
        // The guarantee this design rests on: there is no public constructor, so no
        // caller can hand a store a query the store would have had to refuse — and no
        // `with` expression can clone around the factory, which is why this is a class
        // and not a record.
        var type = typeof(BookingQuery);

        Assert.Empty(type.GetConstructors());

        // Not a record: the compiler synthesises `<Clone>$` for one, which is what backs
        // a `with` expression — and `with` would clone around the factory and produce
        // exactly the state this type exists to make impossible.
        Assert.DoesNotContain(
            type.GetMethods(System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic),
            method => method.Name == "<Clone>$");

        // No settable or init-only members either: state is fixed at construction.
        Assert.All(
            type.GetProperties(),
            property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void The_default_status_set_is_the_blocking_statuses()
    {
        // Tied to the domain's own notion of blocking rather than a list restated here,
        // so adding a status forces this to be revisited rather than silently excluding it.
        var blocking = Enum.GetValues<BookingStatus>()
            .Where(status => Booking.Rehydrate(
                Guid.NewGuid(),
                BookingInterval.Create(From, From.AddHours(1), "UTC").Value,
                Booker.Create(null, "A Tester", "tester@example.com", null).Value,
                [new ResourceClaim(Guid.NewGuid())],
                status,
                From).Value.IsBlocking)
            .Order()
            .ToList();

        var query = BookingQuery.Create(From, From.AddDays(1), Settings()).Value;

        Assert.Equal(blocking, query.Statuses.Order());
    }

    [Fact]
    public void Supplied_statuses_are_used_exactly_as_given()
    {
        var query = BookingQuery
            .Create(From, From.AddDays(1), Settings(), statuses: [BookingStatus.Cancelled]).Value;

        Assert.Equal([BookingStatus.Cancelled], query.Statuses);
    }

    [Fact]
    public void An_empty_status_set_means_the_default_rather_than_nothing()
    {
        // An empty collection quietly meaning "match nothing" would return an empty list
        // for a query that looks reasonable, which is the worst kind of wrong answer.
        var defaulted = BookingQuery.Create(From, From.AddDays(1), Settings()).Value;
        var empty = BookingQuery.Create(From, From.AddDays(1), Settings(), statuses: []).Value;

        Assert.Equal(defaulted.Statuses.Order(), empty.Statuses.Order());
    }

    [Fact]
    public void An_absent_resource_set_is_empty_rather_than_null()
    {
        // The store branches on Count, so a null here would be a NullReferenceException
        // on the most ordinary query there is.
        var query = BookingQuery.Create(From, From.AddDays(1), Settings()).Value;

        Assert.NotNull(query.ResourceIds);
        Assert.Empty(query.ResourceIds);
    }

    [Fact]
    public void Paging_is_normalised_at_construction()
    {
        var negative = BookingQuery.Create(From, From.AddDays(1), Settings(), skip: -5, take: -1).Value;

        Assert.Equal(0, negative.Skip);
        Assert.Equal(0, negative.Take);

        // Capped rather than honoured, so an unreasonable page cannot become the whole
        // window — and capped to the same bound the other management list reads use.
        var huge = BookingQuery.Create(From, From.AddDays(1), Settings(), take: 10_000).Value;

        Assert.Equal(BookingQuery.MaxTake, huge.Take);
        Assert.Equal(500, BookingQuery.MaxTake);
    }
}
