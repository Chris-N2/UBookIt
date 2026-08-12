using UBookIt.Core.Availability;
using UBookIt.Core.Resources;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Bookable-start projection: each aligned start with the longest length still
/// bookable from it (availability spec, "Bookable-start projection"), and its
/// agreement with fixed-duration slot projection.
/// </summary>
public class BookableStartTests
{
    private static readonly DateOnly Date = TestData.BaseDate;

    private static Resource RoomOpen(string start, string end, int granularity, int min, int max)
        => TestData.Room(TestData.Config(
            TestData.Weekly(start, end, Date.DayOfWeek),
            constraints: BookingConstraints.Create(
                granularity: TimeSpan.FromMinutes(granularity),
                minDuration: TimeSpan.FromMinutes(min),
                maxDuration: TimeSpan.FromMinutes(max)).Value));

    private static TimeSpan Mins(int minutes) => TimeSpan.FromMinutes(minutes);

    [Fact]
    public async Task Spec_scenario_maximum_shortens_towards_the_end_of_a_free_interval()
    {
        // Minimum matches the granularity: BookingConstraints requires both
        // duration bounds to be multiples of it.
        var room = RoomOpen("09:00", "12:00", granularity: 60, min: 60, max: 480);
        var (_, availability, _) = TestData.Services(room);

        var result = await availability.GetBookableStartsAsync(room.Id, Date, Date);

        Assert.True(result.Succeeded);
        Assert.Equal(
            new[]
            {
                (TestData.Utc(Date, "09:00"), Mins(180)),
                (TestData.Utc(Date, "10:00"), Mins(120)),
                (TestData.Utc(Date, "11:00"), Mins(60)),
            },
            result.Value.Select(s => (s.StartUtc, s.MaxDuration)).ToArray());
    }

    [Fact]
    public async Task Spec_scenario_the_resource_maximum_caps_the_run()
    {
        var room = RoomOpen("09:00", "17:00", granularity: 60, min: 60, max: 120);
        var (_, availability, _) = TestData.Services(room);

        var result = await availability.GetBookableStartsAsync(room.Id, Date, Date);

        Assert.True(result.Succeeded);
        Assert.NotEmpty(result.Value);
        Assert.All(result.Value, s => Assert.True(s.MaxDuration <= Mins(120)));
    }

    [Fact]
    public async Task Spec_scenario_a_start_too_close_to_the_end_is_not_offered()
    {
        var room = RoomOpen("09:00", "09:45", granularity: 15, min: 30, max: 480);
        var (_, availability, _) = TestData.Services(room);

        var result = await availability.GetBookableStartsAsync(room.Id, Date, Date);

        Assert.True(result.Succeeded);
        Assert.Equal(
            new[] { TestData.Utc(Date, "09:00"), TestData.Utc(Date, "09:15") },
            result.Value.Select(s => s.StartUtc).ToArray());
    }

    [Fact]
    public async Task Spec_scenario_the_maximum_is_floored_to_a_granularity_multiple()
    {
        // 100 minutes of room from 09:00 on a 30-minute grid tops out at 90.
        var room = RoomOpen("09:00", "10:40", granularity: 30, min: 30, max: 480);
        var (_, availability, _) = TestData.Services(room);

        var result = await availability.GetBookableStartsAsync(room.Id, Date, Date);

        Assert.True(result.Succeeded);
        Assert.Equal(Mins(90), result.Value.First(s => s.StartUtc == TestData.Utc(Date, "09:00")).MaxDuration);
    }

    [Fact]
    public async Task Spec_scenario_bookable_starts_respect_lead_time()
    {
        var room = TestData.Room(TestData.Config(
            TestData.Weekly("09:00", "17:00", Date.DayOfWeek),
            constraints: BookingConstraints.Create(
                granularity: Mins(30), minDuration: Mins(30), leadTime: TimeSpan.FromDays(365)).Value));
        var (_, availability, _) = TestData.Services(room);

        var result = await availability.GetBookableStartsAsync(room.Id, Date, Date);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task The_minimum_is_the_resource_minimum()
    {
        var room = RoomOpen("09:00", "12:00", granularity: 15, min: 45, max: 480);
        var (_, availability, _) = TestData.Services(room);

        var result = await availability.GetBookableStartsAsync(room.Id, Date, Date);

        Assert.True(result.Succeeded);
        Assert.All(result.Value, s => Assert.Equal(Mins(45), s.MinDuration));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(60)]
    [InlineData(90)]
    [InlineData(120)]
    [InlineData(240)]
    public async Task Spec_scenario_the_two_projections_agree(int minutes)
    {
        // The equivalence the single traversal exists to guarantee: slots for a
        // duration are exactly the bookable starts whose range admits it.
        var room = RoomOpen("09:00", "13:00", granularity: 15, min: 30, max: 180);
        var (_, availability, _) = TestData.Services(room);
        var duration = Mins(minutes);

        var slots = await availability.GetSlotsAsync(room.Id, Date, Date, duration);
        var starts = await availability.GetBookableStartsAsync(room.Id, Date, Date);

        Assert.True(starts.Succeeded);

        if (!slots.Succeeded)
        {
            // The duration is invalid for this resource (too long), so no
            // bookable start may admit it either.
            Assert.DoesNotContain(starts.Value, s => s.Admits(duration));
            return;
        }

        Assert.Equal(
            slots.Value.Select(s => s.StartUtc).ToArray(),
            starts.Value.Where(s => s.Admits(duration)).Select(s => s.StartUtc).ToArray());
    }

    [Fact]
    public async Task Bookable_starts_resume_after_an_existing_booking()
    {
        var room = RoomOpen("09:00", "12:00", granularity: 30, min: 30, max: 480);
        var (bookings, availability, _) = TestData.Services(room);

        var placed = await bookings.PlaceAsync(new UBookIt.Core.Bookings.BookingRequest
        {
            ResourceId = room.Id,
            Start = TestData.Utc(Date, "10:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });
        Assert.True(placed.Succeeded);

        var result = await availability.GetBookableStartsAsync(room.Id, Date, Date);

        Assert.True(result.Succeeded);

        // The booking splits the day: 09:00–10:00 then 11:00–12:00, and each
        // start's maximum is bounded by its own free interval, not the day.
        Assert.Equal(Mins(60), result.Value.First(s => s.StartUtc == TestData.Utc(Date, "09:00")).MaxDuration);
        Assert.Equal(Mins(60), result.Value.First(s => s.StartUtc == TestData.Utc(Date, "11:00")).MaxDuration);
        Assert.DoesNotContain(result.Value, s => s.StartUtc == TestData.Utc(Date, "10:00"));
    }

    [Fact]
    public async Task Unknown_resource_fails_like_the_other_availability_queries()
    {
        var (_, availability, _) = TestData.Services(TestData.Room());

        var result = await availability.GetBookableStartsAsync(Guid.NewGuid(), Date, Date);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Code == UBookIt.Core.Common.FailureCodes.ResourceNotFound);
    }

    [Fact]
    public async Task Inverted_range_fails_like_the_other_availability_queries()
    {
        var room = TestData.Room();
        var (_, availability, _) = TestData.Services(room);

        var result = await availability.GetBookableStartsAsync(room.Id, Date, Date.AddDays(-1));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Code == UBookIt.Core.Common.FailureCodes.DateRangeInvalid);
    }
}
