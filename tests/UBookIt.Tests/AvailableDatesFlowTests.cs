using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Resources;
using UBookIt.Tests.Support;
using UBookIt.Web.Rendering;

namespace UBookIt.Tests;

/// <summary>
/// The date step driven through a real flow, across the whole horizon.
/// </summary>
/// <remarks>
/// <para>
/// <b>This file exists because the seam between two well-tested halves had nothing on it.</b>
/// The window was tested pure, the rendering was tested from hand-built fixtures, and the code
/// that decides what span to read — which lives in neither — was tested by neither. A regression
/// sat there: the read was widened to span both the window and an out-of-window date, which
/// exceeds the site's own query guardrail, so the read was refused and a visitor who typed any
/// date more than thirty days ahead got "no times available" for a completely empty diary.
/// </para>
/// <para>
/// So these drive <see cref="ResourceBookingFlow"/> and <see cref="ServiceBookingFlow"/> rather
/// than a builder, and they sweep the horizon rather than sampling it. A defect that only appears
/// past a boundary is exactly what a handful of chosen dates misses.
/// </para>
/// </remarks>
public class AvailableDatesFlowTests
{
    /// <summary>A room open 08:00–18:00 every day, with nothing booked at all.</summary>
    /// <remarks>
    /// Empty on purpose: every date in the horizon is bookable, so ANY date reporting no times
    /// is the flow failing rather than the diary being full. It makes the failure unambiguous.
    /// </remarks>
    private static (ResourceBookingFlow Flow, Resource Room) Build(SiteBookingSettings settings)
    {
        var room = TestData.Room(
            availability: TestData.Config(
                TestData.Weekly(
                    "08:00", "18:00",
                    DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
                    DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday)));

        var resources = new InMemoryResourceStore().Add(room);
        var bookings = new InMemoryBookingStore();
        var time = new FixedTimeProvider(TestData.Now);

        return (
            new ResourceBookingFlow(
                resources,
                new AvailabilityService(resources, bookings, time, settings),
                settings,
                time),
            room);
    }

    [Fact]
    public async Task Every_date_in_the_horizon_can_be_chosen_and_shows_its_times()
    {
        // THE REGRESSION, swept rather than sampled. It began at exactly offset 31 on a default
        // configuration — the first date whose span with the window exceeds MaxQueryRangeDays —
        // and held for every date after it, which is two thirds of what a stock site offers.
        var settings = TestData.Settings;
        var (flow, room) = Build(settings);
        var today = BookingFormBuilder.TodayIn(TestData.Now, TestData.London);
        var horizon = room.Availability.Constraints.HorizonDays;

        var barren = new List<int>();

        for (var offset = 0; offset <= horizon; offset++)
        {
            var outcome = await flow.BuildAsync(
                room.Id,
                new BookingFlowInput { Date = today.AddDays(offset), DurationMinutes = 60 });

            if (outcome.Form is null || !outcome.Form.HasTimes)
            {
                barren.Add(offset);
            }
        }

        Assert.True(
            barren.Count == 0,
            "A room open every day with nothing booked reported no times on "
            + $"{barren.Count} of {horizon + 1} dates in its own horizon, at offsets: "
            + string.Join(", ", barren.Take(12))
            + (barren.Count > 12 ? " …" : string.Empty)
            + ". A date the site offers must be bookable through the step that offers it.");
    }

    [Fact]
    public async Task The_date_list_survives_a_jump_beyond_the_window()
    {
        // The recovery path. When the read was refused, the list vanished along with the times —
        // so a visitor who landed on an empty page had nothing to click back to either.
        var (flow, room) = Build(TestData.Settings);
        var today = BookingFormBuilder.TodayIn(TestData.Now, TestData.London);

        var outcome = await flow.BuildAsync(
            room.Id,
            new BookingFlowInput { Date = today.AddDays(60), DurationMinutes = 60 });

        Assert.NotNull(outcome.Form);
        Assert.True(outcome.Form!.HasTimes, "the far date's own times are missing");
        Assert.NotEmpty(outcome.Form.AvailableDates);
        Assert.False(
            outcome.Form.SelectedDateIsListed,
            "a date sixty days out is not in a thirty-day window, so the step must say which "
            + "date it is showing rather than appearing to have it selected");
    }

    [Fact]
    public async Task The_list_never_contains_a_date_outside_the_window_it_names()
    {
        // The legend says "in the next N days". A list carrying a date outside those N days
        // contradicts its own heading — and it also suppresses the "Showing …" statement, which
        // is the sentence that exists to explain exactly this situation.
        var (flow, room) = Build(TestData.Settings);
        var today = BookingFormBuilder.TodayIn(TestData.Now, TestData.London);

        foreach (var offset in new[] { 0, 29, 30, 31, 45, 89 })
        {
            var outcome = await flow.BuildAsync(
                room.Id,
                new BookingFlowInput { Date = today.AddDays(offset), DurationMinutes = 60 });

            Assert.NotNull(outcome.Form);
            Assert.True(
                outcome.Form!.AvailableDates.Count <= outcome.Form.WindowDays,
                $"at offset {offset} the list held {outcome.Form.AvailableDates.Count} dates for "
                + $"a window of {outcome.Form.WindowDays} days.");

            foreach (var date in outcome.Form.AvailableDates)
            {
                Assert.InRange(date.Date, today, today.AddDays(outcome.Form.WindowDays - 1));
            }
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(31)]
    public async Task A_tight_query_guardrail_leaves_every_date_reachable(int guardrail)
    {
        // The guardrail makes the window SHORTER, which makes more of the horizon lie outside
        // it — so the tighter the setting, the more dates depend on the out-of-window path. A
        // site that tightened this was the worst affected and the least likely to be tested.
        var settings = TestData.Settings with { MaxQueryRangeDays = guardrail };
        var (flow, room) = Build(settings);
        var today = BookingFormBuilder.TodayIn(TestData.Now, TestData.London);

        foreach (var offset in new[] { 0, guardrail - 1, guardrail, guardrail + 1, 50, 89 })
        {
            var outcome = await flow.BuildAsync(
                room.Id,
                new BookingFlowInput { Date = today.AddDays(offset), DurationMinutes = 60 });

            Assert.NotNull(outcome.Form);
            Assert.True(
                outcome.Form!.HasTimes,
                $"with a guardrail of {guardrail}, offset {offset} reported no times on an "
                + "empty diary.");

            Assert.True(
                outcome.Form.WindowDays <= guardrail,
                $"the window spans {outcome.Form.WindowDays} days against a guardrail of {guardrail}.");
        }
    }

    [Fact]
    public async Task A_date_in_the_window_is_served_by_one_read_and_agrees_with_the_list()
    {
        // The common case, and the one D1's "one reading cannot contradict itself" is about:
        // when the selected date is IN the window, its times are that read filtered rather than
        // a second read. Observable as agreement — a date the list offers has times.
        var (flow, room) = Build(TestData.Settings);
        var today = BookingFormBuilder.TodayIn(TestData.Now, TestData.London);

        var outcome = await flow.BuildAsync(
            room.Id,
            new BookingFlowInput { Date = today.AddDays(5), DurationMinutes = 60 });

        Assert.NotNull(outcome.Form);
        Assert.True(outcome.Form!.SelectedDateIsListed);
        Assert.True(outcome.Form.HasTimes);
        Assert.Contains(outcome.Form.AvailableDates, date => date.Date == today.AddDays(5));
    }

    [Fact]
    public async Task The_window_read_and_a_single_day_read_agree_about_that_day()
    {
        // THE DIFFERENTIAL, and this time it differentiates.
        //
        // The first version of this compared a hand-built list against the same LINQ predicate
        // re-typed over that same list, and called GetBookableStartsAsync nowhere. It could only
        // report that the test's own filter agreed with the test's own filter — a guard watching
        // a reconstruction of the thing it guards, which is the defect this project has now
        // produced four times.
        //
        // What the claim actually is: widening the read must change how much is asked for and
        // NOTHING about what is answered. So both reads are really issued and compared.
        var settings = TestData.Settings;
        var (_, room) = Build(settings);
        var resources = new InMemoryResourceStore().Add(room);
        var bookingStore = new InMemoryBookingStore();
        var time = new FixedTimeProvider(TestData.Now);
        var availability = new AvailabilityService(resources, bookingStore, time, settings);
        var bookings = new BookingService(resources, bookingStore, time, settings);

        var today = BookingFormBuilder.TodayIn(TestData.Now, TestData.London);
        var day = today.AddDays(4);

        // WITH A BOOKING ON THE DAY, so the claims read does real work and the two reads have
        // something to disagree about. Against an empty diary the projection is the same
        // arithmetic twice and the comparison proves much less.
        var placed = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = room.Id,
            Start = TestData.Utc(day, "10:00"),
            Duration = TimeSpan.FromMinutes(60),
            Booker = TestData.Booker(),
        });

        Assert.True(placed.Succeeded);

        var single = await availability.GetBookableStartsAsync(room.Id, day, day);
        Assert.True(single.Succeeded);

        var (windowFrom, windowTo) = AvailableDateWindow.Compute(
            today, room.Availability.Constraints.HorizonDays, settings.MaxQueryRangeDays);

        var window = await availability.GetBookableStartsAsync(room.Id, windowFrom, windowTo);
        Assert.True(window.Succeeded);

        var filtered = BookingFormBuilder.OnDate(window.Value, TestData.London, day);

        Assert.NotEmpty(single.Value);
        Assert.Equal(single.Value, filtered);
    }

    [Fact]
    public async Task A_lead_time_removes_the_dates_it_leaves_nothing_bookable_on()
    {
        // THE SCENARIO THAT HAD NO TEST. The requirement claimed the window was derived from
        // lead time; the code does not do that and is right not to — a lead time is a duration,
        // so shifting the window by it would skip whole days a site still sells. What is
        // actually guaranteed is that a date it leaves nothing bookable on is not LISTED, and
        // that follows from listing only dates with an admitting start.
        //
        // Emergent guarantees are the ones most worth testing: nothing in the window code
        // mentions lead time at all, so nothing would break visibly if the projection stopped
        // applying it.
        //
        // 30 hours: today and tomorrow morning are inside it, so today cannot be listed while
        // dates comfortably beyond it must be.
        var room = TestData.Room(
            availability: TestData.Config(
                TestData.Weekly(
                    "08:00", "18:00",
                    DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
                    DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday),
                constraints: BookingConstraints.Create(leadTime: TimeSpan.FromHours(30)).Value));

        var settings = TestData.Settings;
        var resources = new InMemoryResourceStore().Add(room);
        var time = new FixedTimeProvider(TestData.Now);
        var flow = new ResourceBookingFlow(
            resources,
            new AvailabilityService(resources, new InMemoryBookingStore(), time, settings),
            settings,
            time);

        var today = BookingFormBuilder.TodayIn(TestData.Now, TestData.London);

        var outcome = await flow.BuildAsync(
            room.Id, new BookingFlowInput { Date = today.AddDays(5), DurationMinutes = 60 });

        Assert.NotNull(outcome.Form);
        Assert.NotEmpty(outcome.Form!.AvailableDates);

        Assert.DoesNotContain(
            outcome.Form.AvailableDates,
            date => date.Date == today);

        Assert.Contains(outcome.Form.AvailableDates, date => date.Date == today.AddDays(5));
    }
}
