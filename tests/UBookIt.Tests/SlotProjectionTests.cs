using UBookIt.Core.Availability;
using UBookIt.Core.Common;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

public class SlotProjectionTests
{
    private static readonly DateOnly Date = TestData.BaseDate;

    private static UBookIt.Core.Resources.Resource RoomOpen(string start, string end, TimeSpan granularity)
        => TestData.Room(TestData.Config(
            TestData.Weekly(start, end, Date.DayOfWeek),
            constraints: BookingConstraints.Create(
                granularity: granularity, minDuration: granularity).Value));

    [Fact]
    public async Task Spec_scenario_60_minute_slots_in_a_two_hour_window()
    {
        var room = RoomOpen("09:00", "11:00", TimeSpan.FromMinutes(30));
        var (_, availability, _) = TestData.Services(room);

        var result = await availability.GetSlotsAsync(room.Id, Date, Date, TimeSpan.FromMinutes(60));

        Assert.True(result.Succeeded);
        Assert.Equal(
            new[] { TestData.Utc(Date, "09:00"), TestData.Utc(Date, "09:30"), TestData.Utc(Date, "10:00") },
            result.Value.Select(s => s.StartUtc).ToArray());
    }

    [Fact]
    public async Task Duration_that_cannot_fit_produces_no_slots()
    {
        var room = RoomOpen("09:00", "10:00", TimeSpan.FromMinutes(30));
        var (_, availability, _) = TestData.Services(room);

        var result = await availability.GetSlotsAsync(room.Id, Date, Date, TimeSpan.FromMinutes(90));

        Assert.True(result.Succeeded);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task Slots_resume_after_an_existing_booking()
    {
        var room = TestData.Room();
        var (bookings, availability, _) = TestData.Services(room);

        var placed = await bookings.PlaceAsync(new UBookIt.Core.Bookings.BookingRequest
        {
            ResourceId = room.Id,
            Start = TestData.Utc(Date, "08:30"),
            Duration = TimeSpan.FromHours(1),
            Booker = TestData.Booker(),
        });
        Assert.True(placed.Succeeded);

        var result = await availability.GetSlotsAsync(room.Id, Date, Date, TimeSpan.FromMinutes(30));

        Assert.True(result.Succeeded);
        Assert.Equal(TestData.Utc(Date, "08:00"), result.Value[0].StartUtc);
        Assert.Equal(TestData.Utc(Date, "09:30"), result.Value[1].StartUtc);
    }

    [Fact]
    public async Task Duration_not_aligned_to_granularity_is_rejected()
    {
        var room = TestData.Room();
        var (_, availability, _) = TestData.Services(room);

        var result = await availability.GetSlotsAsync(room.Id, Date, Date, TimeSpan.FromMinutes(40));

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.Granularity, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public async Task Duration_outside_constraint_bounds_is_rejected()
    {
        var room = TestData.Room();
        var (_, availability, _) = TestData.Services(room);

        var tooShort = await availability.GetSlotsAsync(room.Id, Date, Date, TimeSpan.FromMinutes(15));
        var tooLong = await availability.GetSlotsAsync(room.Id, Date, Date, TimeSpan.FromHours(9));

        Assert.Equal(FailureCodes.DurationTooShort, Assert.Single(tooShort.Failures).Code);
        Assert.Equal(FailureCodes.DurationTooLong, Assert.Single(tooLong.Failures).Code);
    }
}
