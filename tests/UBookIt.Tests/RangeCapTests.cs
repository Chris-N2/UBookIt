using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Common;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Availability spec, "Bounded query range": an over-wide query is rejected
/// with <c>date-range-too-large</c> before any work, protecting the day-by-day
/// computation. Both the free-time and slot paths honour the cap.
/// </summary>
public class RangeCapTests
{
    private static readonly DateOnly From = TestData.BaseDate;

    private static AvailabilityService AvailabilityWithCap(int maxDays, out Guid resourceId)
    {
        var room = TestData.Room();
        resourceId = room.Id;
        var resources = new InMemoryResourceStore().Add(room);
        var settings = new SiteBookingSettings { TimeZoneId = TestData.LondonZoneId, MaxQueryRangeDays = maxDays };
        return new AvailabilityService(resources, new InMemoryBookingStore(), new FixedTimeProvider(TestData.Now), settings);
    }

    [Fact]
    public async Task Range_within_the_maximum_is_computed()
    {
        var availability = AvailabilityWithCap(31, out var id);

        // 31 days inclusive (from .. from+30).
        var result = await availability.GetFreeTimeAsync(id, From, From.AddDays(30));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Range_exactly_at_the_maximum_is_accepted()
    {
        var availability = AvailabilityWithCap(31, out var id);

        var result = await availability.GetFreeTimeAsync(id, From, From.AddDays(30));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Range_exceeding_the_maximum_is_rejected()
    {
        var availability = AvailabilityWithCap(31, out var id);

        // 32 days inclusive (from .. from+31).
        var result = await availability.GetFreeTimeAsync(id, From, From.AddDays(31));

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.DateRangeTooLarge, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public async Task Slot_query_honours_the_same_bound()
    {
        var availability = AvailabilityWithCap(31, out var id);

        var result = await availability.GetSlotsAsync(id, From, From.AddDays(31), TimeSpan.FromMinutes(60));

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.DateRangeTooLarge, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public async Task Bound_is_evaluated_before_resource_lookup()
    {
        var availability = AvailabilityWithCap(31, out _);

        // An unknown resource with an over-wide range fails on the range, not the lookup.
        var result = await availability.GetFreeTimeAsync(Guid.NewGuid(), From, From.AddDays(31));

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.DateRangeTooLarge, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public async Task Cap_is_configurable()
    {
        var availability = AvailabilityWithCap(7, out var id);

        Assert.True((await availability.GetFreeTimeAsync(id, From, From.AddDays(6))).Succeeded);   // 7 days

        var tooWide = await availability.GetFreeTimeAsync(id, From, From.AddDays(7));              // 8 days
        Assert.False(tooWide.Succeeded);
        Assert.Equal(FailureCodes.DateRangeTooLarge, Assert.Single(tooWide.Failures).Code);
    }
}
