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
/// <b>Derived, not listed.</b> The set is every public `[HttpPost]` on the package's
/// visitor-facing controllers, found by reflection — so a POST added tomorrow is covered the day it
/// exists rather than the day somebody remembers this file. A hardcoded list would pass forever
/// while the thing it names drifts.
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
    /// The visitor-facing controllers — the ones a browser posts a form to.
    /// </summary>
    /// <remarks>
    /// The delivery API is deliberately excluded and that is not an oversight: it is a
    /// machine-to-machine JSON API with no browser session and no cookie to forge against, it is
    /// off by default, and anti-forgery on it would break every legitimate consumer. Stated here so
    /// the absence reads as a decision rather than a gap.
    /// </remarks>
    private static readonly Type[] VisitorFacing =
    [
        typeof(BookingSurfaceController),
        typeof(ServiceBookingSurfaceController),
        typeof(CancellationController),
    ];

    private static IEnumerable<MethodInfo> PostActions(Type controller)
        => controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpPostAttribute>().Any());

    [Fact]
    public void Every_visitor_facing_post_validates_an_anti_forgery_token()
    {
        var unprotected = VisitorFacing
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
        var found = VisitorFacing
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
