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
/// <para>
/// <b>Measured in the thread's own CPU time, not wall-clock time.</b> The first version timed one
/// contiguous loop per width with a stopwatch, and failed at random (195.5× on Windows, 136× on
/// Linux CI, against a limit of 120× and a norm of about 13×): a single GC pause or preemption of
/// ~50 ms inside a ~6 ms window loop decided the result. Better sampling alone did not cure it —
/// under induced load, interleaved wall-clock minimums still failed 23–51 trials in 200 — because
/// a stall is time the thread never ran, and only a CPU clock leaves it out. See
/// <see cref="ThreadCpuTime"/> and <c>stable-cost-measurement</c>.
/// </para>
/// <para>
/// <b>What the sampling is still for:</b> CPU time still includes GC work the measuring thread's
/// own allocations trigger. So each width is timed as many short batches, interleaved ABBA so
/// neither always follows the other, sized to last about as long as each other (longer batches
/// are the likelier to be hit), and the cheapest batch per call is taken.
/// </para>
/// <para>
/// <b>Known limit — memory exhaustion can still fail it.</b> A thread that triggers a collection
/// runs it on its own CPU time, and the minimum only absorbs that while collections are cheap.
/// Observed once, on Linux, with the heap at 2.4 GB in a 1.96 GB VM seconds before the kernel
/// killed the process: every window batch paid for a gen-1 collection, the window cost ~10× its
/// norm while one day stayed normal, and the ratio read 187.5×. Not seen in 400 trials under
/// ordinary heavy GC load. So the failure message carries the GC counts and heap size: a failure
/// with a heap near the machine's memory is this, not the algorithm — check the box, then the
/// code. (<c>stable-cost-measurement</c>, design, first risk.)
/// </para>
/// </remarks>
public class AvailableDatesCostTests
{
    private const int Rounds = 25;
    private const int WindowCallsPerBatch = 4;
    private const int OneDayCallsPerBatch = 40;

    /// <summary>
    /// Below this many usable batches for either width, there is no measurement to assert on —
    /// and the test says so rather than passing on what is left.
    /// </summary>
    private const int MinimumValidBatches = 10;

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

    /// <summary>The cheapest batch seen for one width, per call, and how many batches counted.</summary>
    private sealed class Width(DateOnly from, DateOnly to, int callsPerBatch)
    {
        public DateOnly From { get; } = from;
        public DateOnly To { get; } = to;
        public int CallsPerBatch { get; } = callsPerBatch;
        public double Cheapest { get; private set; } = double.MaxValue;
        public int Valid { get; private set; }

        public void Record(double? perCall)
        {
            if (perCall is { } value)
            {
                Cheapest = Math.Min(Cheapest, value);
                Valid++;
            }
        }
    }

    /// <summary>
    /// CPU time per call for one batch, or <see langword="null"/> when the batch finished on a
    /// different thread from the one it started on — a thread's CPU clock read on two threads
    /// measures nothing.
    /// </summary>
    private static async Task<double?> BatchAsync(
        AvailabilityService availability, Guid resourceId, Width width)
    {
        var thread = Environment.CurrentManagedThreadId;
        var start = ThreadCpuTime.Now();

        for (var call = 0; call < width.CallsPerBatch; call++)
        {
            var result = await availability.GetBookableStartsAsync(resourceId, width.From, width.To);
            Assert.True(result.Succeeded);
        }

        var end = ThreadCpuTime.Now();

        return Environment.CurrentManagedThreadId == thread
            ? (double)(end - start) / width.CallsPerBatch
            : null;
    }

    [Fact]
    public async Task The_window_read_costs_about_its_width_and_the_number_is_recorded()
    {
        var (availability, resourceId) = Busy();
        var today = BookingFormBuilder.TodayIn(TestData.Now, TestData.London);

        var (from, to) = AvailableDateWindow.Compute(
            today, horizonDays: 90, maxQueryRangeDays: TestData.Settings.MaxQueryRangeDays);

        var oneDay = new Width(today, today, OneDayCallsPerBatch);
        var window = new Width(from, to, WindowCallsPerBatch);

        // Warm once each — the first call pays for JIT and for the store's first
        // materialisation, and charging that to either width would distort the ratio.
        _ = await availability.GetBookableStartsAsync(resourceId, oneDay.From, oneDay.To);
        _ = await availability.GetBookableStartsAsync(resourceId, window.From, window.To);

        for (var round = 0; round < Rounds; round++)
        {
            var (first, second) = round % 2 == 0 ? (oneDay, window) : (window, oneDay);
            first.Record(await BatchAsync(availability, resourceId, first));
            second.Record(await BatchAsync(availability, resourceId, second));
        }

        Assert.True(
            oneDay.Valid >= MinimumValidBatches && window.Valid >= MinimumValidBatches,
            $"Could not measure: only {oneDay.Valid} one-day and {window.Valid} window batches of "
            + $"{Rounds} stayed on one thread (need {MinimumValidBatches} each). The read has "
            + "started completing asynchronously, so a per-thread CPU clock cannot time it.");

        var days = to.DayNumber - from.DayNumber + 1;
        var ratio = oneDay.Cheapest > 0 ? window.Cheapest / oneDay.Cheapest : double.NaN;

        // Written where a person will see it. A measurement nobody records is an impression.
        Console.WriteLine(
            $"[available-dates] one day: {oneDay.Cheapest:F0} {ThreadCpuTime.Unit} | "
            + $"{days} days: {window.Cheapest:F0} {ThreadCpuTime.Unit} | ratio: {ratio:F1}x | "
            + $"per-day: {window.Cheapest / days:F0} {ThreadCpuTime.Unit} | "
            + $"batches: {oneDay.Valid}/{Rounds} one-day, {window.Valid}/{Rounds} window");

        // THE SHAPE, not the speed. Reading n days should cost about n times reading one; a
        // super-linear projection is the failure a wall-clock number alone would hide, and it is
        // the one that would actually justify a cache. Generous, because this runs on whatever
        // machine happens to be free — it is here to catch an order-of-magnitude change in the
        // algorithm, never to police milliseconds.
        Assert.True(
            ratio < days * 4,
            $"Reading {days} days cost {ratio:F1}x reading one, which is super-linear enough to "
            + "suggest the projection does more than repeat itself per day. Numbers: "
            + $"one day {oneDay.Cheapest:F0} {ThreadCpuTime.Unit}, "
            + $"window {window.Cheapest:F0} {ThreadCpuTime.Unit}. Process GC at failure: "
            + $"gen0 {GC.CollectionCount(0)}, gen1 {GC.CollectionCount(1)}, gen2 {GC.CollectionCount(2)}, "
            + $"heap {GC.GetTotalMemory(forceFullCollection: false) / 1_000_000} MB. If that heap is "
            + "near the machine's memory, collections run on the measuring thread were charged to "
            + "it (see the class remarks); if it is small, the algorithm changed.");
    }
}
