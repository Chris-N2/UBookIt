using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Services;
using UBookIt.Tests.Support;
using UBookIt.Web.Controllers;

namespace UBookIt.Tests;

/// <summary>
/// The delivery API never publishes a service's email template set (service-email-templates: the
/// <c>delivery-api</c> delta, design 8).
/// </summary>
/// <remarks>
/// <b>Asserted on the serialised body, not on the read model's type</b>, on the terms
/// <see cref="ServiceBookerInstructionsDeliveryTests"/> set: a marker absent from the whole body,
/// and the body for a service WITH a set byte-for-byte the body for the same service without one.
/// </remarks>
public class ServiceTemplateSetDeliveryTests
{
    /// <summary>A valid set name nothing else in a response could contain by accident.</summary>
    private const string Marker = "zz-marker-set-7q";

    private static readonly Guid ServiceId = new("00000000-0000-0000-0000-0000000e7501");

    private static Service Consultation(string? templateSet)
        => Service.Create("Online consultation", null, [new ServiceRole("room", 1)], ServiceId).Value
            .WithTemplateSet(templateSet).Value;

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
    public async Task Neither_public_read_contains_the_set_anywhere_in_its_body()
    {
        var (get, list) = await ReadAsync(Consultation(Marker));

        // The service is really there, so absence is not an empty response passing for one.
        Assert.Contains("Online consultation", get, StringComparison.Ordinal);
        Assert.Contains("Online consultation", list, StringComparison.Ordinal);

        Assert.DoesNotContain(Marker, get, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Marker, list, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("templateSet", get, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("templateSet", list, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_read_model_publishes_exactly_what_it_does_for_the_same_service_without_one()
    {
        var with = await ReadAsync(Consultation(Marker));
        var without = await ReadAsync(Consultation(null));

        Assert.Equal(without.Get, with.Get);
        Assert.Equal(without.List, with.List);
    }
}
