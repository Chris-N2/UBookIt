using UBookIt.Core.Availability;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The pairwise grid arithmetic (service-booking spec, "A permanent start-grid
/// misalignment is detectable", and design D3).
/// <para>
/// Written from the arithmetic rather than from the implementation: each case
/// states the offset and the two steps and says whether Bézout admits a
/// solution, so a predicate that merely looks right fails here. The differential
/// test at the bottom then holds the arithmetic against the real projector,
/// because a rule about start grids that has drifted from the grid the projector
/// walks would be confidently wrong in exactly the case it exists to detect.
/// </para>
/// </summary>
public class StartGridTests
{
    private static TimeOnly At(string time) => TimeOnly.Parse(time);

    private static TimeSpan Minutes(int minutes) => TimeSpan.FromMinutes(minutes);

    private static bool CanMeet(string firstStart, int firstStep, string secondStart, int secondStep)
        => StartGrid.CanMeet(At(firstStart), Minutes(firstStep), At(secondStart), Minutes(secondStep));

    [Fact]
    public void The_worked_example_never_meets()
    {
        // 09:00/30 against 09:15/20: 30k − 20m = 15. The left side is divisible
        // by gcd(30, 20) = 10 for every integer k and m; 15 is not. No solution,
        // on any day, ever.
        Assert.False(CanMeet("09:00", 30, "09:15", 20));
    }

    [Fact]
    public void A_half_hour_offset_between_the_same_two_steps_does_meet()
    {
        // Same steps, offset 30: gcd(30, 20) = 10 divides 30. 09:30 + 20 + 20 +
        // 20 = 10:30 = 09:00 + 30 × 3, so they really do coincide.
        Assert.True(CanMeet("09:00", 30, "09:30", 20));
    }

    [Fact]
    public void Order_does_not_change_the_answer()
    {
        // The offset changes sign between the two orderings, and divisibility is
        // sign-blind. A remainder test that assumed a non-negative offset would
        // answer differently depending on which role was listed first.
        Assert.Equal(CanMeet("09:00", 30, "09:15", 20), CanMeet("09:15", 20, "09:00", 30));
        Assert.Equal(CanMeet("09:00", 30, "09:30", 20), CanMeet("09:30", 20, "09:00", 30));
    }

    [Theory]
    [InlineData("09:00", "09:30", true)]   // offset 30, a multiple of the step
    [InlineData("09:00", "10:00", true)]   // offset 60, likewise
    [InlineData("09:00", "09:10", false)]  // offset 10, not a multiple of 30
    [InlineData("09:00", "09:25", false)]  // offset 25, likewise
    public void Equal_steps_meet_exactly_when_the_offset_is_a_multiple_of_them(
        string first, string second, bool expected)
    {
        // With equal steps gcd(s, s) = s, so the test reduces to "the offset is a
        // whole number of steps" — which is also the case a divisibility test
        // dropped in favour of an equality test would get wrong in one direction
        // only.
        Assert.Equal(expected, CanMeet(first, 30, second, 30));
    }

    [Theory]
    [InlineData("09:01")]
    [InlineData("09:07")]
    [InlineData("09:23")]
    public void Coprime_steps_always_meet_whatever_the_offset(string secondStart)
    {
        // gcd(25, 12) = 1 divides everything, so no offset can keep them apart —
        // including one that is a multiple of neither step.
        Assert.True(CanMeet("09:00", 25, secondStart, 12));
    }

    [Theory]
    [InlineData(20, 30)]
    [InlineData(45, 45)]
    [InlineData(7, 11)]
    public void A_zero_offset_always_meets(int firstStep, int secondStep)
    {
        // Two windows opening at the same minute share their first start whatever
        // their steps: gcd divides zero.
        Assert.True(CanMeet("09:00", firstStep, "09:00", secondStep));
    }

    [Fact]
    public void A_step_that_does_not_divide_a_day_is_judged_on_the_real_offset()
    {
        // 7-minute steps: gcd(7, 7) = 7, and the offset 09:00 → 09:03 is 3
        // minutes, so they cannot meet. Wrapping a negative difference by 24
        // hours — as elapsed-clock subtraction does — would add 1440 minutes,
        // which is not a multiple of 7, and could flip the answer in either
        // direction depending on the order of the arguments.
        Assert.False(CanMeet("09:00", 7, "09:03", 7));
        Assert.False(CanMeet("09:03", 7, "09:00", 7));

        Assert.True(CanMeet("09:00", 7, "09:14", 7));
        Assert.True(CanMeet("09:14", 7, "09:00", 7));
    }

    // ------------------------------------------------------ against the projector

    private static Resource SweepResource(string type, string open, int granularityMinutes)
        => Resource.Create(
            type,
            $"{type} {open}/{granularityMinutes}",
            availability: TestData.Config(
                TestData.Weekly(open, "18:00", TestData.BaseDate.DayOfWeek),
                constraints: BookingConstraints.Create(
                    granularity: Minutes(granularityMinutes),
                    minDuration: Minutes(granularityMinutes),
                    maxDuration: Minutes(granularityMinutes * 4)).Value)).Value;

    /// <summary>
    /// The starts the real projection offers for a resource over one open day,
    /// with no claims — so what comes back is exactly the open-window grid the
    /// alignment check reasons about.
    /// </summary>
    private static HashSet<DateTimeOffset> ProjectedStarts(
        AvailabilityService availability, Resource resource)
    {
        var starts = availability.ProjectBookableStarts(resource, [], TestData.BaseDate, TestData.BaseDate);

        Assert.True(starts.Succeeded);

        return [.. starts.Value.Select(s => s.StartUtc)];
    }

    [Fact]
    public void Every_permanently_misaligned_verdict_really_has_no_shared_start()
    {
        // The guard on drift. For a sweep of window/step pairs, the arithmetic's
        // verdict is held against what ProjectBookableStarts actually yields:
        //
        //   "cannot meet"  MUST have no shared start — a false accusation is the
        //                  one failure mode design D1 forbids outright.
        //   "may meet"     may still have none: the shared instant can fall
        //                  outside both windows, which is why the check only ever
        //                  reports the negative.
        //
        // Deliberately not a test of the predicate against itself: the expected
        // side is enumerated starts from the projector, so a predicate that
        // drifted from the grid the projector walks fails here even though its
        // own unit tests still pass.
        var resources = new InMemoryResourceStore();
        var availability = new AvailabilityService(
            resources, new InMemoryBookingStore(), new FixedTimeProvider(TestData.Now), TestData.Settings);

        string[] opens = ["09:00", "09:05", "09:15", "09:20", "09:30"];
        int[] steps = [10, 12, 15, 20, 30];

        var compared = 0;
        var misaligned = 0;

        foreach (var firstOpen in opens)
        {
            foreach (var firstStep in steps)
            {
                var first = SweepResource(ResourceTypes.Room, firstOpen, firstStep);
                var firstStarts = ProjectedStarts(availability, first);

                foreach (var secondOpen in opens)
                {
                    foreach (var secondStep in steps)
                    {
                        var second = SweepResource("therapist", secondOpen, secondStep);

                        var verdict = CanMeet(firstOpen, firstStep, secondOpen, secondStep);
                        var shared = firstStarts.Intersect(ProjectedStarts(availability, second)).Any();

                        compared++;

                        if (!verdict)
                        {
                            misaligned++;

                            Assert.False(
                                shared,
                                $"{firstOpen}/{firstStep} against {secondOpen}/{secondStep} was reported as " +
                                "permanently misaligned, but the projector offers a start both can take.");
                        }
                    }
                }
            }
        }

        // The sweep has to contain both verdicts, or it proves nothing: an
        // implication with no true antecedent is vacuous, and one with no false
        // antecedent never exercises the arithmetic that matters.
        Assert.Equal(625, compared);
        Assert.InRange(misaligned, 1, compared - 1);
    }

    [Fact]
    public void A_verdict_that_the_grids_may_meet_is_not_a_promise_of_a_shared_start()
    {
        // The other direction, asserted rather than assumed. Both open at 09:00
        // with steps 30 and 20, so gcd(30, 20) = 10 divides the zero offset and
        // the check stays silent — but the second resource's window is one step
        // long, so its only start is 09:00 … which the first also offers. Move
        // the pair apart instead: 09:00/30 against 09:20/20 has gcd 10 dividing
        // 20, yet the shared instants all fall after both windows close.
        var resources = new InMemoryResourceStore();
        var availability = new AvailabilityService(
            resources, new InMemoryBookingStore(), new FixedTimeProvider(TestData.Now), TestData.Settings);

        var room = Resource.Create(
            ResourceTypes.Room,
            "Narrow Room",
            availability: TestData.Config(
                TestData.Weekly("09:00", "09:30", TestData.BaseDate.DayOfWeek),
                constraints: BookingConstraints.Create(
                    granularity: Minutes(30), minDuration: Minutes(30), maxDuration: Minutes(30)).Value)).Value;

        var therapist = Resource.Create(
            "therapist",
            "Narrow Therapist",
            availability: TestData.Config(
                TestData.Weekly("09:20", "09:40", TestData.BaseDate.DayOfWeek),
                constraints: BookingConstraints.Create(
                    granularity: Minutes(20), minDuration: Minutes(20), maxDuration: Minutes(20)).Value)).Value;

        Assert.True(CanMeet("09:00", 30, "09:20", 20));

        Assert.Empty(ProjectedStarts(availability, room).Intersect(ProjectedStarts(availability, therapist)));
    }
}
