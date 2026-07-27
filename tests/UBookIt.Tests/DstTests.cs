using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// DST edge tests pinned to Europe/London per the availability spec.
/// 2026: clocks go forward Sunday 29 March (01:00 GMT → 02:00 BST) and
/// back Sunday 25 October (02:00 BST → 01:00 GMT).
/// </summary>
public class DstTests
{
    private static readonly DateOnly SpringForward = new(2026, 3, 29);
    private static readonly DateOnly FallBack = new(2026, 10, 25);

    [Fact]
    public void Wall_clock_time_in_the_spring_gap_maps_to_the_transition_instant()
    {
        var utc = WallClockMapper.ToUtc(SpringForward, new TimeOnly(1, 30), TestData.London);

        Assert.Equal(new DateTimeOffset(2026, 3, 29, 1, 0, 0, TimeSpan.Zero), utc);
    }

    [Fact]
    public void Ambiguous_fall_back_time_resolves_to_first_occurrence()
    {
        // Spec scenario: wall-clock 01:30 on fall-back day → 00:30 UTC (the BST occurrence).
        var utc = WallClockMapper.ToUtc(FallBack, new TimeOnly(1, 30), TestData.London);

        Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero), utc);
    }

    [Fact]
    public async Task Spring_forward_gap_is_not_bookable_time()
    {
        // Open 00:30–02:30 on the spring-forward Sunday. The wall hour
        // 01:00–02:00 does not exist, so the free interval is one UTC hour:
        // local 00:30–01:00 (GMT) + local 02:00–02:30 (BST) = 00:30Z–01:30Z.
        var room = TestData.Room(TestData.Config(
            TestData.Weekly("00:30", "02:30", DayOfWeek.Sunday),
            constraints: BookingConstraints.Create(minDuration: TimeSpan.FromMinutes(30)).Value));
        var now = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var (_, availability, _) = TestData.Services(room, now);

        var result = await availability.GetFreeTimeAsync(room.Id, SpringForward, SpringForward);

        Assert.True(result.Succeeded);
        var free = Assert.Single(result.Value);
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 0, 30, 0, TimeSpan.Zero), free.StartUtc);
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.Zero), free.EndUtc);
        Assert.Equal(TimeSpan.FromHours(1), free.Duration);
    }

    [Fact]
    public async Task Slots_around_the_gap_render_as_the_wall_clock_times_either_side()
    {
        var room = TestData.Room(TestData.Config(
            TestData.Weekly("00:30", "02:30", DayOfWeek.Sunday),
            constraints: BookingConstraints.Create(
                granularity: TimeSpan.FromMinutes(30), minDuration: TimeSpan.FromMinutes(30)).Value));
        var now = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var (_, availability, _) = TestData.Services(room, now);

        var result = await availability.GetSlotsAsync(room.Id, SpringForward, SpringForward, TimeSpan.FromMinutes(30));

        Assert.True(result.Succeeded);
        var localStarts = result.Value
            .Select(s => TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(s.StartUtc, TestData.London).DateTime))
            .ToArray();

        // 00:30 (before the gap) and 02:00 (after it) — never 01:xx.
        Assert.Equal([new TimeOnly(0, 30), new TimeOnly(2, 0)], localStarts);
    }

    [Fact]
    public async Task Booking_placed_at_ambiguous_wall_time_stores_first_occurrence_utc()
    {
        var room = TestData.Room(TestData.Config(
            TestData.Weekly("01:00", "03:00", DayOfWeek.Sunday),
            constraints: BookingConstraints.Create(minDuration: TimeSpan.FromMinutes(30)).Value));
        var now = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var (bookings, _, _) = TestData.Services(room, now);

        var result = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = room.Id,
            Start = WallClockMapper.ToUtc(FallBack, new TimeOnly(1, 30), TestData.London),
            Duration = TimeSpan.FromMinutes(30),
            Booker = TestData.Booker(),
        });

        Assert.True(result.Succeeded);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero), result.Value.Interval.StartUtc);
        Assert.Equal(TestData.LondonZoneId, result.Value.Interval.TimeZoneId);
    }
}
