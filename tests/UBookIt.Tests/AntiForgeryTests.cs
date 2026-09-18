using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Web.Controllers;
using UBookIt.Web.Rendering;

namespace UBookIt.Tests;

/// <summary>
/// Every visitor-facing form submission is anti-forgery protected (`default-frontend`,
/// "Anti-forgery-protected submission"; `self-service-cancellation`, "Cancelling requires a
/// protected submission").
/// </summary>
/// <remarks>
/// <para>
/// <b>The class, not the instance.</b> QA found the cancellation POST unguarded and observed that
/// the two existing booking POSTs were equally unguarded — the requirement had been stated since
/// the first Razor flow and asserted by nothing. Fixing only the new one would have left the older
/// two exactly as they were, which is the habit this project names: a finding enumerates a sample,
/// not the population.
/// </para>
/// <para>
/// <b>Derived, not listed — and that claim is now true of BOTH halves.</b> The first version
/// derived the actions but hardcoded the three controllers, while its remark said it derived
/// everything. QA disproved it by adding a fourth controller with an unprotected POST and watching
/// every test pass. Both the controller set and its actions come from the assembly now, and the
/// controller set is pinned in the positive control so a new one forces a decision.
/// </para>
/// <para>
/// These are anonymous endpoints: nothing authenticates the caller, so anti-forgery is the only
/// thing standing between a third-party page and a booking placed, or cancelled, on a visitor's
/// behalf without their knowledge.
/// </para>
/// </remarks>
public class AntiForgeryTests
{
    /// <summary>
    /// Every visitor-facing controller in the package — <b>derived from the assembly</b>, not listed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This was a hardcoded list of three types, under a remark claiming it was derived.</b> QA
    /// disproved the claim the only way it could be disproved: by adding a new controller with an
    /// unprotected <c>[HttpPost]</c> and watching all three tests pass. That is exactly how
    /// <c>CancellationController.Cancel</c> itself arrived — on a NEW controller — which is why
    /// nothing caught it for a whole round.
    /// </para>
    /// <para>
    /// So the set is now every MVC controller in the Web assembly that is not a delivery-API
    /// controller. A visitor-facing POST added tomorrow is covered the day it exists, rather than
    /// the day somebody remembers this file.
    /// </para>
    /// <para>
    /// <b>The delivery API is the stated exclusion, and it is not an oversight.</b> It is a
    /// machine-to-machine JSON API with no browser session and no cookie to forge against, it is
    /// off by default, and anti-forgery on it would break every legitimate consumer. Excluded by
    /// its base type, so a new delivery endpoint inherits the exclusion and a new visitor-facing
    /// controller does not.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<Type> VisitorFacing()
        => [.. typeof(CancellationController).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsPublic: true }
                && typeof(Controller).IsAssignableFrom(type)
                && !typeof(UBookItDeliveryApiControllerBase).IsAssignableFrom(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal)];

    private static IEnumerable<MethodInfo> PostActions(Type controller)
        => controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpPostAttribute>().Any());

    [Fact]
    public void Every_visitor_facing_post_validates_an_anti_forgery_token()
    {
        var unprotected = VisitorFacing()
            .SelectMany(controller => PostActions(controller)
                .Where(method => !method.GetCustomAttributes<ValidateAntiForgeryTokenAttribute>().Any())
                .Select(method => $"{controller.Name}.{method.Name}"))
            .ToArray();

        Assert.True(
            unprotected.Length == 0,
            "A visitor-facing POST accepts a submission without an anti-forgery token: "
            + string.Join(", ", unprotected));
    }

    [Fact]
    public void The_scan_finds_the_posts_it_is_supposed_to_be_checking()
    {
        // POSITIVE CONTROL, and it earns its place: a reflection scan that found no actions would
        // report every controller protected. This project has shipped a scan that passed by
        // matching nothing, which is why every scan here now carries one of these.
        var controllers = VisitorFacing().Select(type => type.Name).ToArray();

        // THE CONTROLLER SET IS PINNED, not just the actions. A new visitor-facing controller now
        // forces a decision here rather than slipping past — which is the failure QA reproduced by
        // adding one and watching every test pass.
        Assert.Equal(
            ["BookingSurfaceController", "CancellationController", "ServiceBookingSurfaceController"],
            controllers);

        var found = VisitorFacing()
            .SelectMany(controller => PostActions(controller).Select(method => $"{controller.Name}.{method.Name}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "BookingSurfaceController.Submit",
                "CancellationController.Cancel",
                "ServiceBookingSurfaceController.Submit",
            ],
            found);

        // And the exclusion really excludes something, or "not a delivery controller" would be a
        // condition that never fires and the derivation would be the whole assembly by accident.
        Assert.Contains(
            typeof(CancellationController).Assembly.GetTypes(),
            type => typeof(UBookItDeliveryApiControllerBase).IsAssignableFrom(type) && !type.IsAbstract);
    }

    [Fact]
    public void The_cancellation_get_is_not_a_post()
    {
        // The safe/acting split, asserted structurally: a GET that carried [HttpPost] — or a POST
        // that lost it — would make the page's retrieval a state change, which is exactly what
        // mail scanners would then perform unattended.
        var index = typeof(CancellationController).GetMethod(nameof(CancellationController.Index))!;

        Assert.NotEmpty(index.GetCustomAttributes<HttpGetAttribute>());
        Assert.Empty(index.GetCustomAttributes<HttpPostAttribute>());
        Assert.Empty(index.GetCustomAttributes<ValidateAntiForgeryTokenAttribute>());
    }
}
