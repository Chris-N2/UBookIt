using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

public class RehydrationTests
{
    private static BookingInterval Interval() => BookingInterval.Create(
        TestData.Utc(TestData.BaseDate, "09:00"),
        TestData.Utc(TestData.BaseDate, "10:00"),
        TestData.LondonZoneId).Value;

    [Fact]
    public void Rehydration_is_faithful_including_statuses_no_pathway_creates()
    {
        var id = Guid.NewGuid();
        var claims = new[] { new ResourceClaim(Guid.NewGuid()), new ResourceClaim(Guid.NewGuid()) };

        var result = Booking.Rehydrate(id, Interval(), TestData.Booker(), claims, BookingStatus.Declined, TestData.Now);

        Assert.True(result.Succeeded);
        var booking = result.Value;
        Assert.Equal(id, booking.Id);
        Assert.Equal(BookingStatus.Declined, booking.Status);
        Assert.Equal(claims, booking.Claims);
        Assert.Equal(TestData.Now, booking.CreatedUtc);
    }

    [Fact]
    public void Rehydrated_booking_still_enforces_the_status_machine()
    {
        var booking = Booking.Rehydrate(
            Guid.NewGuid(), Interval(), TestData.Booker(),
            [new ResourceClaim(Guid.NewGuid())], BookingStatus.Requested, TestData.Now).Value;

        Assert.True(booking.Confirm().Succeeded);
        Assert.False(booking.Decline().Succeeded);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
    }

    [Fact]
    public void Zero_claims_is_rejected()
    {
        var result = Booking.Rehydrate(
            Guid.NewGuid(), Interval(), TestData.Booker(), [], BookingStatus.Confirmed, TestData.Now);

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.ClaimsInvalid, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void Duplicate_resource_claims_are_rejected()
    {
        var resourceId = Guid.NewGuid();

        var result = Booking.Rehydrate(
            Guid.NewGuid(), Interval(), TestData.Booker(),
            [new ResourceClaim(resourceId), new ResourceClaim(resourceId)], BookingStatus.Confirmed, TestData.Now);

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.ClaimsInvalid, Assert.Single(result.Failures).Code);
    }
}
