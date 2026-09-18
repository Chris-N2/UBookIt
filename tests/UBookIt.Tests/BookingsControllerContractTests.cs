using System.Reflection;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
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
    public void The_window_cannot_be_omitted_by_a_caller()
    {
        // "The window SHALL NOT be optional" is a guarantee about MODEL BINDING, and until
        // now nothing asserted it: removing both `[BindRequired]` attributes passed all 860
        // tests, and the endpoint then answered a windowless call with 200 and an empty page
        // over a window starting at 0001-01-01 — succeeding, and wrong.
        //
        // The generated TypeScript client does pin the required shape, but it is a committed
        // artifact: it only disagrees with the C# after someone regenerates it, so it sits a
        // build behind the defect rather than catching it.
        //
        // This asks MVC's own metadata layer the question its binder asks, rather than
        // looking for an attribute — a guard on the attribute would pass on any other
        // mechanism that made the parameter required, and fail on this one if the framework
        // ever expressed it differently.
        //
        // MVC's own provider, built the way MVC builds it — the detail providers that decide
        // `IsBindingRequired` are internal, and assembling a hand-rolled subset would be a
        // second opinion about the very thing under test.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMvcCore();

        var provider = (ModelMetadataProvider)services.BuildServiceProvider()
            .GetRequiredService<IModelMetadataProvider>();

        var action = typeof(BookingsController)
            .GetMethod(nameof(BookingsController.ListBookings))!;

        foreach (var name in new[] { "from", "to" })
        {
            var parameter = action.GetParameters().Single(p => p.Name == name);

            Assert.True(
                provider.GetMetadataForParameter(parameter).IsBindingRequired,
                $"'{name}' is not binding-required, so a caller can omit the window.");
        }

        // And the filters stay optional, so this cannot be satisfied by making everything
        // required — which would refuse the ordinary "show me this week" call.
        foreach (var name in new[] { "statuses", "resourceIds", "skip", "take" })
        {
            var parameter = action.GetParameters().Single(p => p.Name == name);

            Assert.False(
                provider.GetMetadataForParameter(parameter).IsBindingRequired,
                $"'{name}' became binding-required; the port treats it as optional.");
        }

        // THE PARAMETER SET IS CLOSED, which is the half of the requirement the two loops above
        // cannot reach. The spec says the window cannot be omitted **and that the list accepts no
        // reference parameter**; only the first clause was covered, so adding an optional
        // `reference` to this action — the alternative D1 explicitly rejects — passed the whole
        // suite.
        //
        // It matters beyond tidiness. A reference filter on the LIST would be a windowed,
        // status-filtered, paged read of a booking somebody quoted, which answers "is this
        // reference in this window" — a question a caller can sweep. The by-reference endpoint
        // exists precisely so that lookup is a seek on a unique index, answered once, under its
        // own authorization. Two doors to one answer is how the second one stops being reviewed.
        Assert.Equal(
            new[] { "cancellationToken", "from", "resourceIds", "skip", "statuses", "take", "to" },
            action.GetParameters()
                .Select(parameter => parameter.Name!)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    // The "status default is not restated" assertion that used to live here has moved to
    // BookingsEndpointTests, which asserts it through the controller.
    //
    // It was a source grep — "the file must not contain the word Confirmed" — and that is
    // a proxy for the guarantee rather than the guarantee. It broke the moment a COMMENT
    // mentioned a status name, which is a test dictating prose rather than behaviour; and
    // it never covered paging, where the same defect was actually present and shipped past
    // it. Both defaults are now asserted against the port's own constants, through a real
    // call, which is what the requirement is about.

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
