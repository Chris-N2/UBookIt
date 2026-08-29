using System.Reflection;
using UBookIt.Core.Bookings;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// How a booking comes to name a service — and how it cannot.
/// <para>
/// The guarantee here is structural rather than behavioural, which is unusual and
/// deliberate. Recording the wrong service is not a wrong answer to a query that someone
/// notices; it is a wrong <b>fact</b>, stored permanently and indistinguishable afterwards
/// from a real attribution. Nothing downstream can detect it, so the only place to prevent
/// it is the shape of the API that writes it.
/// </para>
/// </summary>
public class ServiceAttributionTests
{
    private static BookingInterval Interval() => BookingInterval.Create(
        TestData.Utc(TestData.BaseDate, "09:00"),
        TestData.Utc(TestData.BaseDate, "10:00"),
        TestData.LondonZoneId).Value;

    [Fact]
    public void The_general_multi_claim_contract_cannot_name_a_service()
    {
        // If this ever fails, the failure is not "a test needs updating" — it is that any
        // caller can now attribute a booking to a service whose roles its resources do not
        // satisfy. The direct-placement overload already refuses to take a "this is a
        // direct booking" flag for exactly this reason, and this is that reasoning applied
        // to the other end of the same interface.
        var offenders = typeof(MultiClaimBookingRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property =>
                property.PropertyType == typeof(ServiceAttribution)
                || property.Name.Contains("Service", StringComparison.OrdinalIgnoreCase))
            .Select(property => property.Name)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "MultiClaimBookingRequest now carries a service, so a caller can assert an "
            + "attribution the placement did not make: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Naming_a_service_requires_the_entry_point_that_places_through_one()
    {
        // The other half: the capability exists, and it exists in exactly one place. A
        // structural absence test alone would still pass if the feature had been dropped.
        var entryPoint = typeof(IBookingService).GetMethod(nameof(IBookingService.PlaceForServiceAsync));

        Assert.NotNull(entryPoint);
        Assert.Equal(typeof(ServiceAttribution), entryPoint!.GetParameters()[0].ParameterType);

        // And no OTHER member of the contract accepts one, so this stays the single door.
        var others = typeof(IBookingService)
            .GetMethods()
            .Where(method => method.Name != nameof(IBookingService.PlaceForServiceAsync))
            .Where(method => method.GetParameters()
                .Any(parameter => parameter.ParameterType == typeof(ServiceAttribution)))
            .Select(method => method.Name)
            .ToList();

        Assert.True(
            others.Count == 0,
            "A second way to name a service has appeared on IBookingService: "
            + string.Join(", ", others));
    }

    [Fact]
    public async Task Asking_for_a_service_booking_without_a_service_throws()
    {
        // Not "places an unattributed booking". A caller reaching this overload has asked
        // for a service booking; giving them an ordinary one that succeeds and looks right
        // is the quiet failure this whole change exists to prevent. The general overload
        // is what "no service" means, and it is one call away.
        var service = new BookingService(
            new InMemoryResourceStore(),
            new InMemoryBookingStore(),
            new FixedTimeProvider(TestData.Now),
            TestData.Settings);

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.PlaceForServiceAsync(
            null!,
            new MultiClaimBookingRequest
            {
                ResourceIds = [Guid.NewGuid()],
                Start = TestData.Utc(TestData.BaseDate, "09:00"),
                Duration = TimeSpan.FromMinutes(60),
                Booker = TestData.Booker(),
            }));
    }

    [Fact]
    public void A_booking_created_without_a_service_reports_none()
    {
        var booking = Booking.Rehydrate(
            Guid.NewGuid(),
            Interval(),
            TestData.Booker(),
            [new ResourceClaim(Guid.NewGuid())],
            BookingStatus.Confirmed,
            TestData.Now);

        Assert.Null(booking.Value.Service);
    }

    [Fact]
    public void Rehydration_accepts_a_service_it_cannot_verify()
    {
        // The stored attribution is historical fact, on the same terms as the stored
        // status. A service that no longer exists must not make an old booking unreadable
        // for having been successful — and rehydration has no store to check against
        // anyway, which is the point: it is a persistence boundary, not a validator.
        var attribution = new ServiceAttribution(Guid.NewGuid(), "A service since deleted");

        var result = Booking.Rehydrate(
            Guid.NewGuid(),
            Interval(),
            TestData.Booker(),
            [new ResourceClaim(Guid.NewGuid())],
            BookingStatus.Confirmed,
            TestData.Now,
            attribution);

        Assert.True(result.Succeeded);
        Assert.Equal(attribution, result.Value.Service);
    }
}
