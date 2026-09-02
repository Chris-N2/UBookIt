using UBookIt.Core.Bookings;
using UBookIt.Tests.Support;
using UBookIt.Web.Rendering;

namespace UBookIt.Tests;

/// <summary>
/// That a confirmation model carries the reference of the booking it was built from.
/// <para>
/// The rendering suite proves the shipped views print <c>Model.Reference</c> rather than the
/// Guid — but it renders a fixture, so it can say nothing about whether the model was filled
/// from the booking. Replacing both builders' <c>Reference</c> with a constant, so that every
/// visitor on both flows saw the same reference, passed all 1791 tests. That gap is what this
/// closes; the service flow's half is asserted where <c>BuildConfirmation</c> is already
/// exercised, in <c>ServiceFrontendTests</c>.
/// </para>
/// </summary>
public class ConfirmationModelTests
{
    [Fact]
    public void The_direct_confirmation_model_carries_the_bookings_reference()
    {
        var reference = References.Of("QF7M3XKB");

        var booking = Booking.Create(
            Guid.NewGuid(),
            reference,
            BookingInterval.Create(
                TestData.Utc(TestData.BaseDate, "09:00"),
                TestData.Utc(TestData.BaseDate, "10:00"),
                TestData.LondonZoneId).Value,
            TestData.Booker(),
            [new ResourceClaim(Guid.NewGuid())],
            BookingStatus.Confirmed,
            TestData.Now,
            service: null);

        var model = BookingSurfaceController.BuildConfirmation(booking, "Meeting Room A", TestData.London);

        // The grouped form, because this is a rendering model — the delivery API carries
        // canonical for a consumer that compares or stores it.
        Assert.Equal(reference.Display, model.Reference);
        Assert.Equal("QF7M-3XKB", model.Reference);
    }
}
