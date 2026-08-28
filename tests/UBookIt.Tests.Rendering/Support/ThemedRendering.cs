using System.Reflection;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UBookIt.Web.Theming;
using Umbraco.Cms.Web.Website.ViewEngines;

namespace UBookIt.Tests.Rendering.Support;

/// <summary>
/// Where the package's registration sits relative to the host's own, and by which
/// mechanism.
/// <para>
/// The first two are both supported and both must win — that is the point of the
/// post-configure. The third is the mechanism this change rejected, kept so the guard can
/// be shown to fail on a chain the theme loses, rather than passing because it cannot tell
/// the difference.
/// </para>
/// </summary>
public enum ThemeRegistrationOrder
{
    /// <summary>
    /// The site calls <c>AddUBookItTheme</c> after <c>AddWebsite()</c>.
    /// </summary>
    PackageAfterHost,

    /// <summary>
    /// The site calls <c>AddUBookItTheme</c> before <c>AddWebsite()</c>. A perfectly
    /// ordinary thing to write, and it must work identically: an
    /// <c>IPostConfigureOptions</c> runs after every <c>IConfigureOptions</c> whatever
    /// order they were registered in.
    /// </summary>
    PackageBeforeHost,

    /// <summary>
    /// <b>Not a supported configuration.</b> Registers the expander through a plain
    /// <c>Configure</c> before the host's setups — the mechanism the composer version used,
    /// and the one that silently lost. It exists so the guard can be proved sensitive: a
    /// guard that cannot tell this chain from a working one is not a guard.
    /// </summary>
    RejectedConfigureBeforeHost,
}

/// <summary>
/// A theme, configured into the rendering rig the way a host configures one.
/// <para>
/// <b>It calls the package's real entry point.</b> `AddUBookItTheme(IServiceCollection, …)`
/// is the whole of the resolution mechanism, and it is what runs here — not a copy of it,
/// and not a hand-assembled chain. The host's side is Umbraco's real
/// <see cref="RenderRazorViewEngineOptionsSetup"/> and
/// <see cref="PluginRazorViewEngineOptionsSetup"/>, registered as <c>IConfigureOptions</c>
/// exactly as <c>AddWebsite()</c> registers them.
/// </para>
/// <para>
/// The previous version of this type asserted the production ordering in a doc comment and
/// measured only what followed from it. The ordering claim was false, and this rig could
/// not have noticed — which is why the order is now a dimension the tests sweep rather than
/// a premise they inherit.
/// </para>
/// </summary>
public sealed class ThemedRendering
{
    public ThemedRendering(
        string themeName,
        ThemeRegistrationOrder order = ThemeRegistrationOrder.PackageAfterHost,
        bool usePackageStylesheet = false)
    {
        ThemeName = themeName;
        Order = order;
        UsePackageStylesheet = usePackageStylesheet;
    }

    /// <summary>Every supported call order. Swept, not sampled.</summary>
    public static IReadOnlyList<ThemeRegistrationOrder> SupportedOrders { get; } =
    [
        ThemeRegistrationOrder.PackageAfterHost,
        ThemeRegistrationOrder.PackageBeforeHost,
    ];

    /// <summary>The assembly the fixture themes are precompiled into.</summary>
    public static Assembly ThemeAssemblyUnderTest { get; } =
        typeof(global::UBookIt.Tests.ThemeFixture.ThemeFixture).Assembly;

    public string ThemeName { get; }

    public ThemeRegistrationOrder Order { get; }

    public bool UsePackageStylesheet { get; }

    public Assembly ThemeAssembly => ThemeAssemblyUnderTest;

    /// <summary>
    /// A themed rig for the stylesheet cases, where what matters is that a theme is
    /// <i>active</i> rather than what it supplies.
    /// </summary>
    public static ThemedRendering StylesheetOnly(bool usePackageStylesheet)
        => new(global::UBookIt.Tests.ThemeFixture.ThemeFixture.Complete,
            usePackageStylesheet: usePackageStylesheet);

    internal void ApplyTo(IServiceCollection services)
    {
        switch (Order)
        {
            case ThemeRegistrationOrder.PackageAfterHost:
                AddHostSetups(services);
                services.AddUBookItTheme(ThemeName, UsePackageStylesheet);
                break;

            case ThemeRegistrationOrder.PackageBeforeHost:
                services.AddUBookItTheme(ThemeName, UsePackageStylesheet);
                AddHostSetups(services);
                break;

            case ThemeRegistrationOrder.RejectedConfigureBeforeHost:
                AddRejectedRegistration(services);
                AddHostSetups(services);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(Order), Order, "Unhandled order.");
        }
    }

    /// <summary>
    /// Umbraco's own view-engine setups, registered as <c>AddWebsite()</c> registers them
    /// (<c>Services.ConfigureOptions&lt;T&gt;()</c>, i.e. as <c>IConfigureOptions</c>).
    /// Between them they prepend six locations, among them
    /// <c>/Views/Shared/{0}.cshtml</c> — where the package's own views are found, and the
    /// reason ordering ever mattered.
    /// </summary>
    private static void AddHostSetups(IServiceCollection services)
    {
        services.AddSingleton<
            IConfigureOptions<RazorViewEngineOptions>, RenderRazorViewEngineOptionsSetup>();
        services.AddSingleton<
            IConfigureOptions<RazorViewEngineOptions>, PluginRazorViewEngineOptionsSetup>();
    }

    /// <summary>
    /// The rejected mechanism, reproduced faithfully: the theme options configured, and the
    /// expander added through a plain <c>Configure</c> ahead of the host's setups. This is
    /// what a site got when it called <c>AddUBookItTheme</c> before <c>AddComposers()</c>
    /// under the composer version — an unthemed rendering with the stylesheet suppressed.
    /// </summary>
    private void AddRejectedRegistration(IServiceCollection services)
    {
        services.Configure<UBookItThemeOptions>(options =>
        {
            options.ThemeName = ThemeName;
            options.UsePackageStylesheet = UsePackageStylesheet;
        });

        services.Configure<RazorViewEngineOptions>(options =>
            options.ViewLocationExpanders.Add(new RejectedThemeExpander(ThemeName)));
    }

    /// <summary>
    /// A stand-in for the package's expander in the rejected chain.
    /// <para>
    /// It exists because the package no longer offers a <c>Configure</c>-based
    /// registration to call — not because its internals are out of reach; they are
    /// reachable through <c>InternalsVisibleTo</c>, which is how the supported paths call
    /// the real entry point. The rejected chain therefore differs from the supported ones
    /// in exactly the two things under test: <c>Configure</c> instead of
    /// <c>PostConfigure</c>, and this inert expander instead of the package's.
    /// </para>
    /// <para>
    /// The location it prepends is built from the package's own constant rather than
    /// copied, so a change to the theme root cannot leave this negative test quietly
    /// asserting against the old path.
    /// </para>
    /// </summary>
    private sealed class RejectedThemeExpander(string themeName) : IViewLocationExpander
    {
        public void PopulateValues(ViewLocationExpanderContext context)
        {
        }

        public IEnumerable<string> ExpandViewLocations(
            ViewLocationExpanderContext context,
            IEnumerable<string> viewLocations)
            => [$"{UBookItThemeContract.RootFor(themeName)}/{{0}}.cshtml", .. viewLocations];
    }
}
