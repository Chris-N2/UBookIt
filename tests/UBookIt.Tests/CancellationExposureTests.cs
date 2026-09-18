using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UBookIt.Core;
using UBookIt.Tests.Support;
using UBookIt.Web.Composing;
using UBookIt.Web.Controllers;

namespace UBookIt.Tests;

/// <summary>
/// The cancellation route is absent until a site turns it on (`self-service-cancellation`, "The
/// feature is off until a site turns it on" and "Turning the feature off stops issuing links").
/// </summary>
/// <remarks>
/// <para>
/// <b>Written after QA found the guarantee had no test at all.</b> The route was verified absent
/// once, live, by a 404 — which is evidence about one build, not a regression guard. The claim it
/// stands for is that a site which never enabled this serves no anonymous booking-cancelling route,
/// and that is exactly the kind of thing that fails silently: everything stays green while the
/// route is served.
/// </para>
/// <para>
/// <b>Both halves, because the sibling convention already learned that lesson.</b>
/// `DeliveryApiExposureTests` carries two tests whose stated reason is that every other exposure
/// test registers the convention itself and so cannot see whether the composer registered it. That
/// reasoning applies here verbatim — a convention nobody registers removes nothing.
/// </para>
/// </remarks>
public class CancellationExposureTests
{
    private static readonly string[] CancellationActions =
    [
        nameof(CancellationController.Index),
        nameof(CancellationController.Cancel),
    ];

    /// <summary>The actions MVC keeps when the convention runs with these settings.</summary>
    private static IReadOnlyList<string> Served(SelfServiceCancellationSettings settings)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddControllers(options => options.Conventions.Add(new CancellationExposureConvention(settings)))
            .AddApplicationPart(typeof(CancellationController).Assembly);

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(descriptor => descriptor.ControllerTypeInfo == typeof(CancellationController).GetTypeInfo())
            .Select(descriptor => descriptor.ActionName)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    [Fact]
    public void An_untouched_install_serves_no_cancellation_route()
    {
        // THE DEFAULT THE FEATURE EXISTS TO PROTECT. No descriptor means no route, so a request
        // reaches the host's own not-found handling — the same code path as a made-up URL, which
        // is what makes "absent" indistinguishable from "never existed".
        Assert.Empty(Served(new SelfServiceCancellationSettings()));
    }

    [Fact]
    public void Enabling_it_serves_both_the_page_and_the_submission()
    {
        // Both, or the feature is broken in a way a single-action check would miss: a GET with no
        // POST is a page whose button does nothing, and a POST with no GET is a link that 404s.
        Assert.Equal(
            CancellationActions.Order(StringComparer.Ordinal),
            Served(new SelfServiceCancellationSettings { Enabled = true }));
    }

    [Fact]
    public void Turning_it_off_removes_the_route_again()
    {
        // The withdrawal path: outstanding links lapse because the route stops being served, which
        // is the documented consequence rather than a mitigated one.
        Assert.NotEmpty(Served(new SelfServiceCancellationSettings { Enabled = true }));
        Assert.Empty(Served(new SelfServiceCancellationSettings { Enabled = false }));
    }

    [Fact]
    public void Other_controllers_are_untouched_by_the_switch()
    {
        // The other direction of the precondition, and both directions need guarding: turning
        // cancellation off must not remove the shipped booking form's own routes.
        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddControllers(options => options.Conventions.Add(
                new CancellationExposureConvention(new SelfServiceCancellationSettings())))
            .AddApplicationPart(typeof(CancellationController).Assembly);

        using var provider = services.BuildServiceProvider();

        var survivors = provider.GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Select(descriptor => descriptor.ControllerTypeInfo.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.Contains("BookingSurfaceController", survivors);
        Assert.Contains("ServiceBookingSurfaceController", survivors);
        Assert.DoesNotContain(nameof(CancellationController), survivors);
    }

    // ---- the composer half: a convention nobody registers removes nothing -----------------------

    private static (MvcOptions Options, SelfServiceCancellationSettings Settings) ComposeReal(
        Dictionary<string, string?> configuration)
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().AddInMemoryCollection(configuration).Build();

        new UBookItDeliveryApiComposer().Compose(new ServicesOnlyUmbracoBuilder(services, config));

        using var provider = services.BuildServiceProvider();

        return (
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MvcOptions>>().Value,
            new SelfServiceCancellationSettings
            {
                Enabled = config.GetValue<bool>($"{SelfServiceCancellationSettings.SectionKey}:Enabled"),
            });
    }

    [Fact]
    public void The_composer_registers_the_convention()
    {
        // Without this, every test above could pass while production registered nothing — the API
        // always-on failure the sibling suite records, pointed at a route that cancels bookings.
        var (options, _) = ComposeReal(new Dictionary<string, string?>
        {
            ["UBookIt:SelfServiceCancellation:Enabled"] = "true",
        });

        Assert.Single(options.Conventions.OfType<CancellationExposureConvention>());
    }

    [Fact]
    public void The_composer_registers_a_convention_carrying_the_bound_value()
    {
        // Asserted BY EFFECT, because the instance's settings are not observable and a convention
        // constructed with the wrong record would satisfy any containment check.
        var enabled = Assert.Single(ComposeReal(new Dictionary<string, string?>
        {
            ["UBookIt:SelfServiceCancellation:Enabled"] = "true",
        }).Options.Conventions.OfType<CancellationExposureConvention>());

        var disabled = Assert.Single(ComposeReal([]).Options.Conventions
            .OfType<CancellationExposureConvention>());

        Assert.True(SurvivesConvention(enabled));
        Assert.False(SurvivesConvention(disabled));
    }

    [Fact]
    public void The_composer_defaults_to_off_when_the_section_is_absent()
    {
        // The null-coalesced fallback at the binding site, which nothing else exercises: an
        // install that never mentions the feature must serve no route.
        var (options, settings) = ComposeReal([]);

        Assert.False(settings.Enabled);
        Assert.False(SurvivesConvention(
            Assert.Single(options.Conventions.OfType<CancellationExposureConvention>())));
    }

    /// <summary>
    /// Whether the cancellation page keeps its selectors when THE REGISTERED convention instance is
    /// applied to a minimal model holding it.
    /// </summary>
    private static bool SurvivesConvention(CancellationExposureConvention convention)
    {
        var method = typeof(CancellationController)
            .GetMethod(nameof(CancellationController.Index))!;

        var controller = new Microsoft.AspNetCore.Mvc.ApplicationModels.ControllerModel(
            typeof(CancellationController).GetTypeInfo(), []);

        var action = new Microsoft.AspNetCore.Mvc.ApplicationModels.ActionModel(method, [])
        {
            Controller = controller,
        };

        action.Selectors.Add(new Microsoft.AspNetCore.Mvc.ApplicationModels.SelectorModel());
        controller.Actions.Add(action);

        var application = new Microsoft.AspNetCore.Mvc.ApplicationModels.ApplicationModel();
        application.Controllers.Add(controller);

        convention.Apply(application);

        return action.Selectors.Count > 0;
    }
}
