using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

public class StatusMachineTests
{
    private static Booking BookingIn(BookingStatus status)
    {
        var interval = BookingInterval.Create(
            TestData.Utc(TestData.BaseDate, "09:00"),
            TestData.Utc(TestData.BaseDate, "10:00"),
            TestData.LondonZoneId).Value;

        return Booking.Create(
            Guid.NewGuid(), interval, TestData.Booker(), [new ResourceClaim(Guid.NewGuid())], status, TestData.Now, service: null);
    }

    [Theory]
    [InlineData(BookingStatus.Requested, BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Requested, BookingStatus.Declined)]
    [InlineData(BookingStatus.Requested, BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Confirmed, BookingStatus.Cancelled)]
    public void Permitted_transitions_succeed(BookingStatus from, BookingStatus to)
    {
        var booking = BookingIn(from);

        var result = Apply(booking, to);

        Assert.True(result.Succeeded);
        Assert.Equal(to, booking.Status);
    }

    [Theory]
    [InlineData(BookingStatus.Confirmed, BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Confirmed, BookingStatus.Declined)]
    [InlineData(BookingStatus.Declined, BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Declined, BookingStatus.Declined)]
    [InlineData(BookingStatus.Declined, BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Cancelled, BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Cancelled, BookingStatus.Declined)]
    [InlineData(BookingStatus.Cancelled, BookingStatus.Cancelled)]
    public void Forbidden_transitions_fail_and_leave_status_unchanged(BookingStatus from, BookingStatus to)
    {
        var booking = BookingIn(from);

        var result = Apply(booking, to);

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.InvalidStatusTransition, Assert.Single(result.Failures).Code);
        Assert.Equal(from, booking.Status);
    }

    private static DomainResult Apply(Booking booking, BookingStatus target) => target switch
    {
        BookingStatus.Confirmed => booking.Confirm(),
        BookingStatus.Declined => booking.Decline(),
        BookingStatus.Cancelled => booking.Cancel(),
        _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };
}
