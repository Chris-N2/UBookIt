using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace UBookIt.Web.Theming;

/// <summary>
/// Checks, once at boot, the two things about a registered theme that are otherwise
/// discovered by looking at a wrong-looking page.
/// <list type="number">
/// <item>
/// <b>Does the theme actually win?</b> The registration mechanism is designed so it must,
/// but that design has been wrong once already — and its failure mode was a site that
/// rendered perfectly and was simply not themed. So the mechanism is not trusted: this runs
/// the real expander chain out of the resolved <see cref="RazorViewEngineOptions"/> and
/// checks the theme's location comes out first.
/// </item>
/// <item>
/// <b>Is the theme complete?</b> Per-view fallback is the framework's default and it is
/// entirely silent: a theme supplying <c>Booking/Default</c> but not
/// <c>Booking/Unavailable</c> renders a mixture of two designs with no diagnostic at all.
/// What this changes is the silence, not the fallback.
/// </item>
/// </list>
/// <para>
/// Neither check fails boot. An incomplete theme is a packaging slip and a mis-ordered
/// chain is a package defect; taking a production site down for either is a worse outcome
/// than a loud log line and a site that still serves bookings. Failing boot, and a
/// Development-fails / Production-logs split, were both considered and rejected — the
/// second because the two environments would then disagree about whether a theme is valid.
/// </para>
/// <para>
/// This is the second line of defence. <see cref="UBookItThemeCompleteness"/> is public so
/// the completeness half is caught in the theme author's own build, before a site sees it.
/// </para>
/// </summary>
public sealed class UBookItThemeBootCheck(
    ApplicationPartManager partManager,
    IOptions<UBookItThemeOptions> themeOptions,
    IOptions<RazorViewEngineOptions> viewEngineOptions,
    ILogger<UBookItThemeBootCheck> logger)
    : INotificationHandler<UmbracoApplicationStartedNotification>
{
    public void Handle(UmbracoApplicationStartedNotification notification) => Run();

    /// <summary>
    /// Runs both checks and logs. Separated from the notification so it can be exercised
    /// without booting Umbraco.
    /// </summary>
    /// <returns>The problems found, so a caller can assert on them.</returns>
    public IReadOnlyList<string> Run()
    {
        var theme = themeOptions.Value;

        if (!theme.IsActive)
        {
            return [];
        }

        List<string> problems = [.. ReportResolutionOrder(theme.ThemeName!)];
        problems.AddRange(ReportCompleteness(theme.ThemeName!));

        return problems;
    }

    /// <summary>
    /// Asserts the guarantee — the theme's location resolves first — rather than the
    /// mechanism that is supposed to produce it.
    /// <para>
    /// It does not check that an expander is registered, or that it was registered last, or
    /// from where. It runs the chain the way the view engine runs it and looks at what comes
    /// out, because a check of the former kind passes on exactly the configuration this
    /// exists to catch.
    /// </para>
    /// </summary>
    private IReadOnlyList<string> ReportResolutionOrder(string themeName)
    {
        var expected = UBookItThemeViewLocationExpander.ViewLocationFormatFor(themeName);
        var actual = FirstViewLocation();

        if (string.Equals(actual, expected, StringComparison.Ordinal))
        {
            return [];
        }

        var problem =
            $"The uBookIt theme '{themeName}' is registered but does not resolve first. "
            + $"The first view location the engine will search is '{actual}', not "
            + $"'{expected}'.";

        logger.LogError(
            "The uBookIt theme '{ThemeName}' is registered but will not be used. The first "
            + "view location the view engine searches is '{ActualLocation}', not the theme's "
            + "'{ExpectedLocation}' — so the package's own views render and the site is "
            + "correct but entirely unthemed. Something in this application registered a "
            + "view-location expander after uBookIt's post-configure; nothing in the site's "
            + "own call order can cause this.",
            themeName,
            actual,
            expected);

        return [problem];
    }

    /// <summary>
    /// The first location the view engine would search, obtained by running the whole
    /// expander chain over the configured formats the way <c>RazorViewEngine</c> runs it.
    /// <para>
    /// The context names no controller, area or page, which is what the engine itself
    /// resolves to for a view-component lookup — <c>GetViewLocationFormats</c> returns
    /// <c>ViewLocationFormats</c> for exactly that case. The residue, stated rather than
    /// glossed: a third-party expander that prepended only for particular route values
    /// would not be caught by this context. Nothing in the current closure behaves that
    /// way, and the package's own expander prepends unconditionally, so the answer to "is
    /// the theme first" does not depend on the route.
    /// </para>
    /// </summary>
    private string? FirstViewLocation()
    {
        var options = viewEngineOptions.Value;

        var context = new ViewLocationExpanderContext(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
            viewName: "Components/Booking/Default",
            controllerName: null,
            areaName: null,
            pageName: null,
            isMainPage: false)
        {
            // The engine allocates this before calling PopulateValues; an expander that
            // writes to it would otherwise throw here and nowhere else.
            Values = new Dictionary<string, string?>(StringComparer.Ordinal),
        };

        // Two loops, not one interleaved loop, because that is what the engine does:
        // RazorViewEngine.LocatePageFromViewLocations calls PopulateValues on every
        // expander before OnCacheMiss calls ExpandViewLocations on every expander. No
        // expander in the current closure has a PopulateValues that does anything, so
        // interleaving them is unobservable today — but the entire value of this check is
        // that it runs the chain the way the engine runs it, and a check that claims
        // fidelity it does not have is the kind of thing this whole change exists to stop.
        foreach (var expander in options.ViewLocationExpanders)
        {
            expander.PopulateValues(context);
        }

        IEnumerable<string> locations = options.ViewLocationFormats;

        foreach (var expander in options.ViewLocationExpanders)
        {
            locations = expander.ExpandViewLocations(context, locations);
        }

        return locations.FirstOrDefault();
    }

    private IReadOnlyList<string> ReportCompleteness(string themeName)
    {
        var problems = UBookItThemeCompleteness.Check(themeName, partManager);

        if (problems.Count == 0)
        {
            return problems;
        }

        // One entry naming every missing view, rather than one entry per problem: a reader
        // deciding whether a theme is finished wants the list, and a log scraped for
        // "uBookIt theme" should not have to reassemble it.
        logger.LogError(
            "The uBookIt theme '{ThemeName}' is incomplete. {ProblemCount} of the "
            + "{RequiredCount} required views are not correctly supplied: {Problems} "
            + "The package's own view renders in place of each one, so the site is "
            + "operable but renders a mixture of two designs. Run "
            + "UBookItThemeCompleteness.Check from the theme's own tests to catch this "
            + "before it reaches a site.",
            themeName,
            problems.Count,
            UBookItThemeContract.RequiredViews.Count,
            string.Join(" ", problems));

        return problems;
    }
}
