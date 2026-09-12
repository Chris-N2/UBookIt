using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using UBookIt.Web;
using UBookIt.Web.Composing;
using UBookIt.Web.Controllers;

namespace UBookIt.Tests;

/// <summary>
/// The delivery API's exposure: off until a site turns it on, per direction, and a
/// disabled direction is absent rather than refused.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asserted through MVC's own pipeline, not a hand-built model.</b> The harness
/// stands up <c>AddControllers</c> over the real <c>UBookIt.Web</c> assembly with the
/// real convention and reads what MVC actually produced: an action whose selectors the
/// convention cleared yields <b>no action descriptor</b> — the routing fact itself,
/// since a route cannot match a descriptor that does not exist — and no API
/// description, which is what feeds the OpenAPI document. A hand-built model would
/// test the convention against my idea of the application model rather than MVC's.
/// </para>
/// <para>
/// <b>The expected action lists are hard-coded by name, deliberately.</b> Deriving
/// them from the direction attributes would be circular — the convention reads the
/// same attributes, so a misclassified action would satisfy the test while exposing
/// the wrong endpoint. The lists below are the decision, and reclassifying an action
/// fails here by name.
/// </para>
/// </remarks>
public class DeliveryApiExposureTests
{
    private static readonly string[] ReadActions =
    [
        "GetFreeTime", "GetSlots", "GetBookableStarts",
        "GetPrivacy",
        "ListResources", "GetResource",
        "ListServices", "GetService", "GetServiceBookableStarts",
    ];

    private static readonly string[] PlacementActions = ["PlaceBooking", "PlaceServiceBooking"];

    /// <summary>The no-JS booking form's surface controllers, which exposure must never touch.</summary>
    private static readonly string[] SurfaceControllers =
    [
        "BookingSurfaceController", "ServiceBookingSurfaceController",
    ];

    // ---- the classification is total (design D2) ----

    [Fact]
    public void Every_delivery_action_carries_exactly_one_direction()
    {
        var offenders = new List<string>();

        foreach (var action in DeliveryActions())
        {
            var reads = action.GetCustomAttribute<DeliveryReadAttribute>() is not null;
            var places = action.GetCustomAttribute<DeliveryPlacementAttribute>() is not null;

            if (reads == places)
            {
                offenders.Add(
                    $"{action.DeclaringType!.Name}.{action.Name} carries "
                    + (reads ? "BOTH directions" : "NO direction"));
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Every delivery action must be classified into exactly one direction — an "
            + "unclassified action must never default to exposed:\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// The hard-coded lists and the real assembly must describe the same population, or
    /// the matrix below silently stops covering a new endpoint.
    /// </summary>
    [Fact]
    public void The_expected_lists_cover_every_delivery_action()
    {
        var actual = DeliveryActions().Select(a => a.Name).OrderBy(n => n, StringComparer.Ordinal);
        var recorded = ReadActions.Concat(PlacementActions).OrderBy(n => n, StringComparer.Ordinal);

        Assert.Equal(recorded, actual);
    }

    // ---- the exposure matrix, through MVC's own pipeline ----

    [Fact]
    public void An_untouched_install_serves_nothing()
    {
        var result = Build(new DeliveryApiSettings());

        Assert.Empty(result.DeliveryDescriptorActions);
        Assert.Empty(result.Descriptions);
    }

    [Fact]
    public void Reads_can_be_enabled_without_placement()
    {
        var result = Build(new DeliveryApiSettings { EnableReads = true });

        Assert.Equal(Sorted(ReadActions), Sorted(result.DeliveryDescriptorActions));
        Assert.Equal(Sorted(ReadActions), Sorted(result.Descriptions));
    }

    [Fact]
    public void Placement_can_be_enabled_without_reads()
    {
        var result = Build(new DeliveryApiSettings { EnablePlacement = true });

        Assert.Equal(Sorted(PlacementActions), Sorted(result.DeliveryDescriptorActions));
        Assert.Equal(Sorted(PlacementActions), Sorted(result.Descriptions));
    }

    [Fact]
    public void Both_on_is_the_full_api()
    {
        var result = Build(
            new DeliveryApiSettings { EnableReads = true, EnablePlacement = true });

        var all = Sorted(ReadActions.Concat(PlacementActions));

        Assert.Equal(all, Sorted(result.DeliveryDescriptorActions));
        Assert.Equal(all, Sorted(result.Descriptions));
    }

    /// <summary>
    /// The other direction of the convention's precondition (a rule has preconditions,
    /// and both directions need guarding): controllers that are NOT delivery
    /// controllers — the no-JS form's surface controllers above all — keep their routes
    /// whatever the settings say. If this fails, turning the API off broke the shipped
    /// booking form.
    /// </summary>
    [Fact]
    public void Non_delivery_controllers_are_untouched_by_the_switches()
    {
        var off = Build(new DeliveryApiSettings());
        var on = Build(new DeliveryApiSettings { EnableReads = true, EnablePlacement = true });

        foreach (var surface in SurfaceControllers)
        {
            var offActions = off.ActionsOf(surface);
            var onActions = on.ActionsOf(surface);

            // Present with settings off, and identical either way: the switches reach
            // delivery controllers only. NotEmpty first, so "identical because both
            // vanished" cannot pass.
            Assert.NotEmpty(offActions);
            Assert.Equal(Sorted(onActions), Sorted(offActions));
        }
    }

    // ---- harness ----

    private sealed record ExposureResult(
        IReadOnlyList<(string Controller, string Action)> Descriptors,
        string[] Descriptions)
    {
        /// <summary>Delivery actions that received an action descriptor — the routing fact.</summary>
        public string[] DeliveryDescriptorActions => Descriptors
            .Where(d => DeliveryControllerNames.Contains(d.Controller))
            .Select(d => d.Action)
            .ToArray();

        public string[] ActionsOf(string controller)
            => Descriptors.Where(d => d.Controller == controller).Select(d => d.Action).ToArray();
    }

    private static readonly HashSet<string> DeliveryControllerNames =
        typeof(UBookItDeliveryApiControllerBase).Assembly.GetTypes()
            .Where(t => typeof(UBookItDeliveryApiControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<MethodInfo> DeliveryActions()
        => typeof(UBookItDeliveryApiControllerBase).Assembly.GetTypes()
            .Where(t => typeof(UBookItDeliveryApiControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => !m.IsSpecialName);

    /// <summary>
    /// Runs MVC's application-model pipeline over the real UBookIt.Web assembly with
    /// the real convention, and returns what MVC actually produced: which actions
    /// received an action descriptor (the routing fact — no descriptor, no route), and
    /// which delivery actions ApiExplorer can see (what the OpenAPI document is built
    /// from).
    /// </summary>
    private static ExposureResult Build(DeliveryApiSettings settings)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddControllers(options => options.Conventions.Add(new DeliveryApiExposureConvention(settings)))
            .AddApplicationPart(typeof(UBookItDeliveryApiControllerBase).Assembly);

        using var provider = services.BuildServiceProvider();

        var descriptors = provider.GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items
            .OfType<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>()
            .Select(d => (d.ControllerTypeInfo.Name, d.ActionName))
            .ToList();

        var descriptions = provider.GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items
            .SelectMany(g => g.Items)
            .Select(d => d.ActionDescriptor)
            .OfType<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>()
            .Where(d => DeliveryControllerNames.Contains(d.ControllerTypeInfo.Name))
            .Select(d => d.ActionName)
            .ToArray();

        return new ExposureResult(descriptors, descriptions);
    }

    private static string[] Sorted(IEnumerable<string> names)
        => names.OrderBy(n => n, StringComparer.Ordinal).ToArray();
}
