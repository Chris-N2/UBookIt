using System.Reflection;
using UBookIt.Backoffice.Controllers;
using UBookIt.Backoffice.Models;
using UBookIt.Core.Bookings;
using UBookIt.Core.Stores;

namespace UBookIt.Tests;

/// <summary>
/// What the booking endpoint promises about its own contract: no domain type on the wire,
/// no default restated, and no route to raw booking storage.
/// </summary>
public class BookingsControllerContractTests
{
    private static readonly Assembly Backoffice = typeof(BookingsController).Assembly;

    [Fact]
    public void No_domain_type_appears_in_the_http_contract()
    {
        // The requirement is about the CONTRACT, so this looks at the models and at the
        // action's own parameters — a query parameter is as much a published type as a
        // response body. The first draft of this endpoint failed this on both counts by
        // putting BookingStatus in and out.
        var offenders = new List<string>();

        foreach (var model in Backoffice.GetTypes()
                     .Where(type => type.Namespace == typeof(BookingModel).Namespace && type.IsPublic))
        {
            foreach (var property in model.GetProperties())
            {
                foreach (var type in Unwrap(property.PropertyType))
                {
                    if (IsDomainType(type))
                    {
                        offenders.Add($"{model.Name}.{property.Name} is {type.FullName}");
                    }
                }
            }
        }

        foreach (var action in typeof(BookingsController).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                     .Where(method => method.DeclaringType == typeof(BookingsController)))
        {
            foreach (var parameter in action.GetParameters())
            {
                foreach (var type in Unwrap(parameter.ParameterType))
                {
                    if (IsDomainType(type))
                    {
                        offenders.Add($"{action.Name}({parameter.Name}) is {type.FullName}");
                    }
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Domain types reached the HTTP contract: " + string.Join(", ", offenders));
    }

    [Fact]
    public void The_controller_cannot_reach_raw_booking_storage()
    {
        // The containment guarantee, asserted for the bookings controller the way the
        // existing test asserts it for the resource ones. It depends on the management
        // port, which is the widened requirement's whole point.
        var dependencies = typeof(BookingsController)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToList();

        Assert.Contains(typeof(IBookingManagementStore), dependencies);
        Assert.DoesNotContain(typeof(IBookingStore), dependencies);

        // And no API-layer type mentions the rehydration surface at all.
        Assert.DoesNotContain(
            Backoffice.GetTypes()
                .SelectMany(type => type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                .SelectMany(method => method.GetParameters())
                .Select(parameter => parameter.ParameterType),
            type => type == typeof(IBookingStore));
    }

    [Fact]
    public void The_status_default_is_not_restated_at_the_http_layer()
    {
        // The endpoint must not carry its own copy of "blocking statuses". A default in
        // two places is two defaults, and which one a caller meets depends on the layer
        // they reach first.
        //
        // Asserted by behaviour rather than by reading source: an omitted status list must
        // produce whatever the port produces for an omitted status list.
        var settings = new Core.SiteBookingSettings { TimeZoneId = "UTC" };
        var from = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var portDefault = BookingQuery.Create(from, from.AddDays(1), settings).Value.Statuses;
        var viaEmpty = BookingQuery.Create(from, from.AddDays(1), settings, statuses: []).Value.Statuses;

        Assert.Equal(portDefault.Order(), viaEmpty.Order());

        // And the controller passes an EMPTY list rather than a set of its own when the
        // caller supplies none — which is what makes the two agree.
        Assert.DoesNotContain(
            nameof(BookingStatus.Confirmed),
            SourceOf("src/UBookIt.Backoffice/Controllers/BookingsController.cs"),
            StringComparison.Ordinal);
    }

    private static string SourceOf(string path) => Support.RepoFiles.Read(path);

    /// <summary>Element and generic argument types, so a collection is not a hiding place.</summary>
    private static IEnumerable<Type> Unwrap(Type type)
    {
        yield return type;

        if (type.IsArray && type.GetElementType() is { } element)
        {
            yield return element;
        }

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                yield return argument;
            }
        }
    }

    private static bool IsDomainType(Type type)
        => type.Assembly == typeof(BookingStatus).Assembly;
}
