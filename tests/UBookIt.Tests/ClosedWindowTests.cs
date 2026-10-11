using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Services;
using UBookIt.Tests.Support;
using UBookIt.Web.Rendering;

namespace UBookIt.Tests;

/// <summary>
/// The booking page's date window starts after a closure that covers today
/// (closure-date-ranges: the <c>default-frontend</c> delta) — pure, then through both real flows.
/// </summary>
/// <remarks>
/// Driven through <see cref="ResourceBookingFlow"/> and <see cref="ServiceBookingFlow"/> as well
/// as the pure window, because the seam between "where does the window start" and "what does the
/// page list" is exactly where the available-dates change once shipped a regression that both
/// halves' own tests passed.
/// </remarks>
public class ClosedWindowTests
{
    private static readonly DateOnly Today = BookingFormBuilder.TodayIn(TestData.Now, TestData.London);

    private static WeeklyOpenHours EveryDay()
        => TestData.Weekly(
            "08:00", "18:00",
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday);

    private static SiteClosure Closure(DateOnly from, DateOnly to)
        => SiteClosure.Create(from, "Notice period", endDate: to).Value;

    // ── Pure ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Without_a_first_open_date_the_window_starts_today()
    {
        var (from, _) = AvailableDateWindow.Compute(Today, horizonDays: 90, maxQueryRangeDays: 31);

        Assert.Equal(Today, from);
    }

    [Fact]
    public void A_first_open_date_within_the_horizon_moves_the_start()
    {
        var (from, to) = AvailableDateWindow.Compute(Today, 90, 31, Today.AddDays(40));

        Assert.Equal(Today.AddDays(40), from);
        Assert.Equal(Today.AddDays(69), to);
    }

    [Fact]
    public void The_horizon_is_still_counted_from_today()
    {
        // Bookable 90 days ahead means up to today + 90, however the window is placed.
        var (from, to) = AvailableDateWindow.Compute(Today, 90, 31, Today.AddDays(80));

        Assert.Equal(Today.AddDays(80), from);
        Assert.Equal(Today.AddDays(90), to);
    }

    [Fact]
    public void A_first_open_date_beyond_the_horizon_leaves_the_window_at_today()
    {
        var (from, to) = AvailableDateWindow.Compute(Today, 90, 31, Today.AddDays(200));

        Assert.Equal(Today, from);
        Assert.Equal(Today.AddDays(29), to);
    }

    [Fact]
    public void The_query_guardrail_still_bounds_a_shifted_window()
    {
        var (from, to) = AvailableDateWindow.Compute(Today, 90, 7, Today.AddDays(10));

        Assert.Equal(7, to.DayNumber - from.DayNumber + 1);
    }

    // ── Through the resource flow ────────────────────────────────────────────────────────

    private static async Task<IBookingFormView?> ResourceForm(AvailabilityConfiguration availability)
    {
        var settings = TestData.Settings;
        var room = TestData.Room(availability: availability);
        var resources = new InMemoryResourceStore().Add(room);
        var time = new FixedTimeProvider(TestData.Now);

        var flow = new ResourceBookingFlow(
            resources,
            new AvailabilityService(resources, new InMemoryBookingStore(), time, settings),
            settings,
            time,
            new TestEmailSender());

        return (await flow.BuildAsync(room.Id, new BookingFlowInput { DurationMinutes = 60 })).Form;
    }

    [Fact]
    public async Task A_closure_covering_today_moves_the_resource_window_past_it()
    {
        var form = await ResourceForm(
            AvailabilityConfiguration.Create(EveryDay(), closures: [Closure(Today.AddDays(-3), Today.AddDays(20))]).Value);

        Assert.NotNull(form);
        Assert.Equal(Today.AddDays(21), form.ShiftedWindowFrom);
        Assert.Equal(Today.AddDays(21), form.AvailableDates.First().Date);
        Assert.All(form.AvailableDates, date => Assert.True(date.Date > Today.AddDays(20)));

        // A visitor who chose no date is shown the window's first, not a closed today.
        Assert.True(form.SelectedDateIsListed);
    }

    [Fact]
    public async Task Adjoining_closure_and_closed_period_are_followed()
    {
        var form = await ResourceForm(
            AvailabilityConfiguration.Create(
                EveryDay(),
                closures: [Closure(Today, Today.AddDays(4))],
                closedPeriods: [ClosedPeriod.Create(Today.AddDays(5), Today.AddDays(9)).Value]).Value);

        Assert.Equal(Today.AddDays(10), form!.ShiftedWindowFrom);
    }

    [Fact]
    public async Task With_today_open_the_window_starts_today_and_names_no_dates()
    {
        // The closure is absent from the configuration, exactly as an opted-out one is.
        var form = await ResourceForm(
            AvailabilityConfiguration.Create(EveryDay(), closures: [Closure(Today.AddDays(3), Today.AddDays(9))]).Value);

        Assert.Null(form!.ShiftedWindowFrom);
        Assert.Equal(Today, form.AvailableDates.First().Date);
    }

    [Fact]
    public async Task A_closure_reaching_past_the_horizon_leaves_the_window_at_today()
    {
        var form = await ResourceForm(
            AvailabilityConfiguration.Create(EveryDay(), closures: [Closure(Today, Today.AddDays(400))]).Value);

        Assert.Null(form!.ShiftedWindowFrom);
        Assert.Empty(form.AvailableDates);
    }

    // ── Through the service flow ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_service_window_starts_where_its_first_candidate_reopens()
    {
        var settings = TestData.Settings;

        UBookIt.Core.Resources.Resource Room(string id, int closedForDays)
            => UBookIt.Core.Resources.Resource.Create(
                "room",
                $"Room {id}",
                directlyBookable: true,
                availability: AvailabilityConfiguration.Create(
                    EveryDay(),
                    closedPeriods: [ClosedPeriod.Create(Today, Today.AddDays(closedForDays - 1)).Value]).Value,
                id: new Guid($"00000000-0000-0000-0000-00000000040{id}")).Value;

        var service = Service.Create(
            "Massage",
            duration: null,
            roles: [new ServiceRole("room", 1)],
            id: new Guid("00000000-0000-0000-0000-000000000900")).Value;

        var serviceStore = new InMemoryServiceStore().Add(service);
        var resourceStore = new InMemoryResourceStore().Add(Room("1", 5)).Add(Room("2", 10));
        var (core, _, _) = TestData.ServiceBookingWith(serviceStore, resourceStore);
        var time = new FixedTimeProvider(TestData.Now);

        var flow = new ServiceBookingFlow(serviceStore, core, settings, time, new TestEmailSender());
        var form = (await flow.BuildAsync(service.Id, new BookingFlowInput { DurationMinutes = 60 })).Form;

        Assert.Equal(Today.AddDays(5), form!.ShiftedWindowFrom);
        Assert.Equal(Today.AddDays(5), form.AvailableDates.First().Date);
    }
}
