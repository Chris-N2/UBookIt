using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
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
/// <para>
/// <b>Both flows, and that sentence used to be false.</b> It said the same thing while only the
/// resource flow was driven — the service flow's out-of-window path was guarded by nothing, in a
/// change that had already shipped that exact regression once. Every test here is now
/// parameterised over both, which is a claim a theory cannot make falsely.
/// </para>
/// </para>
/// </remarks>
public class AvailableDatesFlowTests
{
    /// <summary>
    /// One flow, whichever kind — so every property below is asserted of BOTH.
    /// </summary>
    /// <remarks>
    /// <b>This abstraction exists because its absence cost a round.</b> The regression lived
    /// identically in both flows and the fix landed identically in both, but the tests written to
    /// close the class drove only the resource one — and this file's own summary claimed
    /// otherwise. QA forced `selectedIsInWindow` to <c>true</c> in the service flow, reinstating
    /// the CRITICAL's exact user-visible symptom there, and all 2227 tests passed.
    /// <para>
    /// So the seam is named once and every test runs over both. A doc comment claiming coverage
    /// that does not exist is worse than silence, and a parameterised test cannot make that claim
    /// falsely.
    /// </para>
    /// </remarks>
    private delegate Task<IBookingFormView?> Drive(DateOnly date, int durationMinutes);

    private sealed record Subject(string Kind, Drive Build, int HorizonDays);

    /// <summary>Every day open 08:00–18:00, nothing booked at all.</summary>
    /// <remarks>
    /// Empty on purpose: every date in the horizon is bookable, so ANY date reporting no times is
    /// the flow failing rather than the diary being full. It makes the failure unambiguous.
    /// </remarks>
    private static WeeklyOpenHours EveryDay()
        => TestData.Weekly(
            "08:00", "18:00",
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday);

    private static Subject Resource(SiteBookingSettings settings)
    {
        var room = TestData.Room(availability: TestData.Config(EveryDay()));
        var resources = new InMemoryResourceStore().Add(room);
        var time = new FixedTimeProvider(TestData.Now);

        var flow = new ResourceBookingFlow(
            resources,
            new AvailabilityService(resources, new InMemoryBookingStore(), time, settings),
            settings,
            time);

        return new Subject(
            "resource",
            async (date, minutes) => (await flow.BuildAsync(
                room.Id, new BookingFlowInput { Date = date, DurationMinutes = minutes })).Form,
            room.Availability.Constraints.HorizonDays);
    }

    private static Subject Service(SiteBookingSettings settings)
    {
        var room = UBookIt.Core.Resources.Resource.Create(
            "room",
            "Treatment Room",
            directlyBookable: true,
            availability: TestData.Config(EveryDay()),
            id: new Guid("00000000-0000-0000-0000-000000000401")).Value;

        var service = UBookIt.Core.Services.Service.Create(
            "Massage",
            duration: null,
            roles: [new ServiceRole("room", 1)],
            id: new Guid("00000000-0000-0000-0000-000000000900")).Value;

        var serviceStore = new InMemoryServiceStore().Add(service);
        var resourceStore = new InMemoryResourceStore().Add(room);
        var (core, _, _) = TestData.ServiceBookingWith(serviceStore, resourceStore);
        var time = new FixedTimeProvider(TestData.Now);

        var flow = new ServiceBookingFlow(serviceStore, core, settings, time);

        return new Subject(
            "service",
            async (date, minutes) => (await flow.BuildAsync(
                service.Id, new BookingFlowInput { Date = date, DurationMinutes = minutes })).Form,
            room.Availability.Constraints.HorizonDays);
    }

    public static TheoryData<string> Flows() => new() { "resource", "service" };

    private static Subject SubjectFor(string kind, SiteBookingSettings settings)
        => kind == "resource" ? Resource(settings) : Service(settings);

    [Theory]
    [MemberData(nameof(Flows))]
    public async Task Every_date_in_the_horizon_can_be_chosen_and_shows_its_times(string kind)
    {
        // THE REGRESSION, swept rather than sampled. It began at exactly offset 31 on a default
        // configuration — the first date whose span with the window exceeds MaxQueryRangeDays —
        // and held for every date after it, which is two thirds of what a stock site offers.
        var settings = TestData.Settings;
        var subject = SubjectFor(kind, settings);
        var today = BookingFormBuilder.TodayIn(TestData.Now, TestData.London);

        // ANTI-VACUITY, and QA proved it was needed: the horizon comes from an unpinned fixture
        // default, so a shorter one would make this a loop over dates that were never at risk.
        // With the CRITICAL reinstated AND a 20-day horizon, this sweep PASSED — the regression
        // starts at 31. The sweep must reach past the window to be a sweep of anything.
        Assert.True(
            subject.HorizonDays > settings.MaxQueryRangeDays,
            $"the {kind} horizon is {subject.HorizonDays} days against a query guardrail of "
            + $"{settings.MaxQueryRangeDays}, so no date in this sweep lies outside the window "
            + "and the sweep cannot see the failure it exists for.");

        var barren = new List<int>();

        for (var offset = 0; offset <= subject.HorizonDays; offset++)
        {
            var form = await subject.Build(today.AddDays(offset), 60);

            if (form is null || !form.HasTimes)
            {
                barren.Add(offset);
            }
        }

        Assert.True(
            barren.Count == 0,
            $"A {kind} open every day with nothing booked reported no times on "
            + $"{barren.Count} of {subject.HorizonDays + 1} dates in its own horizon, at offsets: "
            + string.Join(", ", barren.Take(12))
            + (barren.Count > 12 ? " …" : string.Empty)
            + ". A date the site offers must be bookable through the step that offers it.");
    }

    [Theory]
    [MemberData(nameof(Flows))]
    public async Task The_date_list_survives_a_jump_beyond_the_window(string kind)
    {
        // The recovery path. When the read was refused, the list vanished along with the times —
        // so a visitor who landed on an empty page had nothing to click back to either.
        var subject = SubjectFor(kind, TestData.Settings);
        var today = BookingFormBuilder.TodayIn(TestData.Now, TestData.London);

        var form = await subject.Build(today.AddDays(60), 60);

        Assert.NotNull(form);
        Assert.True(form!.HasTimes, $"the far date's own times are missing on the {kind} flow");
        Assert.NotEmpty(form.AvailableDates);
        Assert.False(
            form.SelectedDateIsListed,
            "a date sixty days out is not in a thirty-day window, so the step must say which "
            + "date it is showing rather than appearing to have it selected");
    }

    [Theory]
    [MemberData(nameof(Flows))]
    public async Task The_list_never_contains_a_date_outside_the_window_it_names(string kind)
    {
        // The legend says "in the next N days". A list carrying a date outside those N days
        // contradicts its own heading — and it also suppresses the "Showing …" statement, which
        // is the sentence that exists to explain exactly this situation.
        //
        // Bounded by the model's OWN WindowDays, so a model reporting a wrong window would move
        // the assertion with it. That is not a hole because the window's width is pinned
        // separately — A_tight_query_guardrail_leaves_every_date_reachable asserts
        // WindowDays <= guardrail — and between them the claim is held. Stated because neither
        // test says on its own that it leans on the other.
        var settings = TestData.Settings;
        var subject = SubjectFor(kind, settings);
        var today = BookingFormBuilder.TodayIn(TestData.Now, TestData.London);

        foreach (var offset in new[] { 0, 29, 30, 31, 45, 89 })
        {
            var form = await subject.Build(today.AddDays(offset), 60);

            Assert.NotNull(form);
            Assert.True(
                form!.AvailableDates.Count <= form.WindowDays,
                $"on the {kind} flow at offset {offset} the list held "
                + $"{form.AvailableDates.Count} dates for a window of {form.WindowDays} days.");

            Assert.True(form.WindowDays <= settings.MaxQueryRangeDays);

            foreach (var date in form.AvailableDates)
            {
                Assert.InRange(date.Date, today, today.AddDays(form.WindowDays - 1));
            }
        }
    }

    [Theory]
    [InlineData("resource", 3)]
    [InlineData("resource", 7)]
    [InlineData("resource", 31)]
    [InlineData("service", 3)]
    [InlineData("service", 7)]
    [InlineData("service", 31)]
    public async Task A_tight_query_guardrail_leaves_every_date_reachable(string kind, int guardrail)
    {
        // The guardrail makes the window SHORTER, which makes more of the horizon lie outside
        // it — so the tighter the setting, the more dates depend on the out-of-window path. A
        // site that tightened this was the worst affected and the least likely to be tested.
        var settings = TestData.Settings with { MaxQueryRangeDays = guardrail };
        var subject = SubjectFor(kind, settings);
        var today = BookingFormBuilder.TodayIn(TestData.Now, TestData.London);

        foreach (var offset in new[] { 0, guardrail - 1, guardrail, guardrail + 1, 50, 89 })
        {
            var form = await subject.Build(today.AddDays(offset), 60);

            Assert.NotNull(form);
            Assert.True(
                form!.HasTimes,
                $"on the {kind} flow with a guardrail of {guardrail}, offset {offset} reported "
                + "no times on an empty diary.");

            Assert.True(
                form.WindowDays <= guardrail,
                $"the window spans {form.WindowDays} days against a guardrail of {guardrail}.");
        }
    }

    [Theory]
    [MemberData(nameof(Flows))]
    public async Task A_date_in_the_window_is_served_by_one_read_and_agrees_with_the_list(string kind)
    {
        // The common case, and the one D1's "one reading cannot contradict itself" is about:
        // when the selected date is IN the window, its times are that read filtered rather than
        // a second read. Observable as agreement — a date the list offers has times.
        var subject = SubjectFor(kind, TestData.Settings);
        var today = BookingFormBuilder.TodayIn(TestData.Now, TestData.London);

        var form = await subject.Build(today.AddDays(5), 60);

        Assert.NotNull(form);
        Assert.True(form!.SelectedDateIsListed);
        Assert.True(form.HasTimes);
        Assert.Contains(form.AvailableDates, date => date.Date == today.AddDays(5));
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
        var room = TestData.Room(availability: TestData.Config(EveryDay()));
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
