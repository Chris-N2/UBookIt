using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Extensions;

namespace UBookIt.Web.Theming;

/// <summary>
/// How a consuming site registers a theme.
/// <para>
/// <b>The theme's view location is registered as an
/// <see cref="Microsoft.Extensions.Options.IPostConfigureOptions{TOptions}"/>, and that is
/// the whole of why a theme cannot be silently ordered out of existence.</b>
/// <c>OptionsFactory.Create</c> runs every <c>IConfigureOptions&lt;T&gt;</c> before any
/// <c>IPostConfigureOptions&lt;T&gt;</c>, whatever order they were registered in. Umbraco
/// registers its two view-location setups as <c>IConfigureOptions</c>
/// (<c>AddWebsite()</c> calls <c>Services.ConfigureOptions&lt;RenderRazorViewEngineOptionsSetup&gt;()</c>
/// and the same for <c>PluginRazorViewEngineOptionsSetup</c>), so the package's expander is
/// appended after theirs no matter where in the chain the site calls this.
/// </para>
/// <para>
/// <b>What this replaced, and why it matters.</b> The first implementation registered from
/// an <c>IComposer</c>, on the reasoning that composers run at <c>IUmbracoBuilder.Build()</c>
/// and therefore after <c>AddWebsite()</c>. **That reasoning was wrong.**
/// <c>AddComposers()</c> constructs a <c>ComposerGraph</c> and calls <c>Compose()</c>
/// immediately, so composition happens at the point the site calls <c>AddComposers()</c> —
/// not at <c>Build()</c>. A site writing the natural
/// <c>.AddWebsite().AddComposers().AddUBookItTheme("x").Build()</c> got a composer that had
/// already run, no expander, no boot check, and — because the theme options were still
/// configured — a suppressed stylesheet. The site rendered the package's own views with the
/// package's CSS switched off, silently. Measured on a running site, then confirmed against
/// the Umbraco 17.8.0-rc source (<c>UmbracoBuilder.Composers.cs</c>, where
/// <c>AddComposers()</c> calls <c>new ComposerGraph(...).Compose()</c> inline).
/// </para>
/// <para>
/// <b>The field the post-configure has to win against was enumerated, not assumed.</b>
/// Against both the pinned 17.6.2 binaries and the 17.8.0-rc source: <b>no</b>
/// <c>IPostConfigureOptions&lt;RazorViewEngineOptions&gt;</c> exists anywhere in Umbraco, and
/// <c>ViewLocationExpanders</c> is mutated in exactly <b>two</b> places — the two setups
/// above, both through <c>IConfigureOptions</c>. That establishes "true today", which is
/// all an enumeration can establish: <c>RazorViewEngineOptions</c> is a public MVC options
/// type, so any other package in the site can post-configure it and land after this one.
/// Hence the boot check, which is about the outcome rather than the field.
/// </para>
/// <para>
/// Composer ordering could not have rescued it. <c>ComposeAfterAttribute</c> only accepts a
/// type implementing <c>IComposer</c>, and Umbraco's view-engine setups are not composers —
/// they are registered inline by <c>AddWebsite()</c>. There was nothing to order against.
/// </para>
/// <para>
/// The mechanism is still not trusted. <see cref="UBookItThemeBootCheck"/> runs the real
/// expander chain at boot and reports if the theme's location is not first.
/// </para>
/// </summary>
public static class UBookItThemeBuilderExtensions
{
    /// <summary>
    /// Registers one theme, package-wide, for this application.
    /// <code>
    ///   builder.CreateUmbracoBuilder()
    ///       .AddBackOffice()
    ///       .AddWebsite()
    ///       .AddDeliveryApi()
    ///       .AddComposers()
    ///       .AddUBookItTheme("mytheme")
    ///       .Build();
    /// </code>
    /// <para>
    /// Where this call sits in the chain does not affect whether the theme wins — the
    /// post-configure guarantees that — and this is asserted across every call order rather
    /// than argued. It must still be called before <c>Build()</c>, like any other
    /// registration.
    /// </para>
    /// </summary>
    /// <param name="builder">The Umbraco builder.</param>
    /// <param name="themeName">
    /// The theme's name, which is the path segment its views live under:
    /// <c>/Views/Shared/UBookIt/Themes/&lt;themeName&gt;/Components/...</c>.
    /// </param>
    /// <param name="usePackageStylesheet">
    /// Whether the theme wants the package's own stylesheet emitted. Default <c>false</c>;
    /// a theme that reuses the package's shared partials as building blocks sets it to
    /// <c>true</c>.
    /// </param>
    public static IUmbracoBuilder AddUBookItTheme(
        this IUmbracoBuilder builder,
        string themeName,
        bool usePackageStylesheet = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddUBookItTheme(themeName, usePackageStylesheet);

        // The boot check is the only part that needs Umbraco, because it hangs off an
        // Umbraco notification. Umbraco's IServiceCollection-level AddNotificationHandler
        // is internal, so this cannot move down to the service-collection overload — which
        // is why that overload documents the check as absent rather than pretending to it.
        builder.AddNotificationHandler<UmbracoApplicationStartedNotification, UBookItThemeBootCheck>();

        return builder;
    }

    /// <summary>
    /// Registers the theme against a bare service collection: the view location and the
    /// options the styling partial reads.
    /// <para>
    /// This is the whole of the resolution mechanism, and it is a separately callable seam
    /// so that the guarantee — the theme's view resolves first — can be exercised directly,
    /// in every order relative to the host's own registrations, rather than through a chain
    /// a test assembled to suit itself. That the package's real entry point is what a test
    /// calls is the difference between measuring this and asserting it.
    /// </para>
    /// <para>
    /// <b>Internal, deliberately.</b> It is reachable from the rendering suite through
    /// <c>InternalsVisibleTo</c>, so the guard loses nothing — and keeping it off the public
    /// surface avoids publishing the one registration path that gets no boot check, in a
    /// capability whose whole subject is failures that do not announce themselves. A site
    /// uses the <see cref="IUmbracoBuilder"/> overload and gets both.
    /// </para>
    /// </summary>
    internal static IServiceCollection AddUBookItTheme(
        this IServiceCollection services,
        string themeName,
        bool usePackageStylesheet = false)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(themeName);

        // One registration, read by two places — the expander below and
        // `_Styles.cshtml` — so the stylesheet cannot be suppressed for a theme that is
        // not actually resolving. That failure was real: it is what the composer version
        // did, and it turned a missing theme into a missing stylesheet as well.
        services.Configure<UBookItThemeOptions>(options =>
        {
            options.ThemeName = themeName;
            options.UsePackageStylesheet = usePackageStylesheet;
        });

        services.PostConfigure<RazorViewEngineOptions>(options =>
            options.ViewLocationExpanders.Add(new UBookItThemeViewLocationExpander(themeName)));

        return services;
    }
}
