using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Services;
using UBookIt.Tests.Support;
using UBookIt.Web.Controllers;

namespace UBookIt.Tests;

/// <summary>
/// The delivery API never publishes a service's instructions for the booker
/// (service-booker-instructions: the <c>delivery-api</c> delta, design 6).
/// </summary>
/// <remarks>
/// <b>Asserted on the serialised body, not on the read model's type.</b> A property check misses a
/// member added later under another name, or nested inside another. A marker string absent from
/// the whole body does not — and the second test makes the comparison total: the body for a
/// service WITH instructions is byte-for-byte the body for the same service without them.
/// </remarks>
public class ServiceBookerInstructionsDeliveryTests
{
    private const string Marker = "SECRET-JOIN-91c4 https://example.test/meet";

    private static readonly Guid ServiceId = new("00000000-0000-0000-0000-0000000b0071");

    private static Service Consultation(string? instructions)
        => Service.Create("Online consultation", null, [new ServiceRole("room", 1)], ServiceId).Value
            .WithBookerInstructions(instructions).Value;

    private static ServicesController Controller(Service service)
    {
        var serviceStore = new InMemoryServiceStore().Add(service);
        var resourceStore = new InMemoryResourceStore();
        var bookingStore = new InMemoryBookingStore();
        var time = new FixedTimeProvider(TestData.Now);
        var settings = TestData.Settings;

        var availability = new AvailabilityService(resourceStore, bookingStore, time, settings);
        var bookings = new BookingService(resourceStore, bookingStore, time, settings);
        var serviceBooking = new ServiceBookingService(
            serviceStore, resourceStore, bookingStore, availability, bookings, settings);

        return new ServicesController(serviceStore, serviceBooking, settings);
    }

    private static string Body(IActionResult result)
        => JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(result).Value, JsonSerializerOptions.Web);

    private static async Task<(string Get, string List)> ReadAsync(Service service)
    {
        var controller = Controller(service);

        return (Body(await controller.GetService(ServiceId)), Body(await controller.ListServices()));
    }

    [Fact]
    public async Task Neither_public_read_contains_the_instructions_anywhere_in_its_body()
    {
        var (get, list) = await ReadAsync(Consultation($"Join on Teams:\n{Marker}"));

        // The service is really there, so absence is not an empty response passing for one.
        Assert.Contains("Online consultation", get, StringComparison.Ordinal);
        Assert.Contains("Online consultation", list, StringComparison.Ordinal);

        Assert.DoesNotContain("SECRET-JOIN-91c4", get, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SECRET-JOIN-91c4", list, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("instruction", get, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("instruction", list, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_read_model_publishes_exactly_what_it_does_for_the_same_service_without_them()
    {
        var with = await ReadAsync(Consultation($"Join on Teams:\n{Marker}"));
        var without = await ReadAsync(Consultation(null));

        Assert.Equal(without.Get, with.Get);
        Assert.Equal(without.List, with.List);
    }
}
