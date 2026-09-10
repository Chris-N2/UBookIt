using System.Diagnostics;
using UBookIt.Core.Availability;
using UBookIt.Tests.Support;
using UBookIt.Web.Rendering;

namespace UBookIt.Tests;

/// <summary>
/// What widening the first step's availability read actually costs.
/// </summary>
/// <remarks>
/// <para>
/// <b>A measurement, deliberately not a threshold.</b> This does not assert that the window read
/// is fast enough — a timing assertion on a shared build agent is a flaky test wearing a
/// performance badge, and it would be the first thing anyone weakened. What it does is produce
/// the number, so the decision about caching is made from evidence.
/// </para>
/// <para>
/// The one thing it DOES assert is the shape of the cost: reading thirty days must cost about
/// thirty times reading one, not thirty times something worse. A quadratic projection would be
/// invisible in a wall-clock number nobody compared against anything.
/// </para>
/// </remarks>
public class AvailableDatesCostTests
{
    /// <summary>A resource open every day, so every day in the window does real work.</summary>
    private static (AvailabilityService Availability, Guid ResourceId) Busy()
    {
        var resource = TestData.Room(
            availability: TestData.Config(
                TestData.Weekly(
                    "08:00", "18:00",
                    DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
                    DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday)));

        var resources = new InMemoryResourceStore().Add(resource);
        var bookings = new InMemoryBookingStore();
        var time = new FixedTimeProvider(TestData.Now);

        return (new AvailabilityService(resources, bookings, time, TestData.Settings), resource.Id);
    }

    private static async Task<double> MillisecondsForAsync(
        AvailabilityService availability, Guid resourceId, DateOnly from, DateOnly to, int runs)
    {
        // Warm once — the first call pays for JIT and for the store's first materialisation, and
        // charging that to the window read would overstate it several-fold.
        _ = await availability.GetBookableStartsAsync(resourceId, from, to);

        var stopwatch = Stopwatch.StartNew();

        for (var run = 0; run < runs; run++)
        {
            var result = await availability.GetBookableStartsAsync(resourceId, from, to);
            Assert.True(result.Succeeded);
        }

        stopwatch.Stop();

        return stopwatch.Elapsed.TotalMilliseconds / runs;
    }

    [Fact]
    public async Task The_window_read_costs_about_its_width_and_the_number_is_recorded()
    {
        const int Runs = 20;

        var (availability, resourceId) = Busy();
        var today = BookingFormBuilder.TodayIn(TestData.Now, TestData.London);

        var oneDay = await MillisecondsForAsync(availability, resourceId, today, today, Runs);

        var (from, to) = AvailableDateWindow.Compute(
            today, horizonDays: 90, maxQueryRangeDays: TestData.Settings.MaxQueryRangeDays);
        var window = await MillisecondsForAsync(availability, resourceId, from, to, Runs);

        var days = to.DayNumber - from.DayNumber + 1;
        var ratio = oneDay > 0 ? window / oneDay : double.NaN;

        // Written where a person will see it. A measurement nobody records is an impression.
        Console.WriteLine(
            $"[available-dates] one day: {oneDay:F3} ms | {days} days: {window:F3} ms | "
            + $"ratio: {ratio:F1}x | per-day: {window / days:F3} ms");

        // THE SHAPE, not the speed. Reading n days should cost about n times reading one; a
        // super-linear projection is the failure a wall-clock number alone would hide, and it is
        // the one that would actually justify a cache. Generous, because this runs on whatever
        // machine happens to be free — it is here to catch an order-of-magnitude change in the
        // algorithm, never to police milliseconds.
        Assert.True(
            ratio < days * 4,
            $"Reading {days} days cost {ratio:F1}x reading one, which is super-linear enough to "
            + "suggest the projection does more than repeat itself per day. Numbers: "
            + $"one day {oneDay:F3} ms, window {window:F3} ms.");
    }
}
