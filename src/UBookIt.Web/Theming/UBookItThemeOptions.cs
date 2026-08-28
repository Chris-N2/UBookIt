namespace UBookIt.Web.Theming;

/// <summary>
/// Which theme is active, and whether it wants the package's stylesheet.
/// <para>
/// <b>One theme, package-wide, fixed at startup.</b> That is a constraint rather
/// than an omission. The theme name is not part of the view-location cache key —
/// <see cref="UBookItThemeViewLocationExpander.PopulateValues"/> contributes
/// nothing — so a location resolved under one theme would be served under the next.
/// Changing the value after the application has started therefore leaves a
/// populated cache keyed without it, and views resolved before the change keep
/// resolving to the old theme. Set it once, at registration, through
/// <c>AddUBookItTheme</c>.
/// </para>
/// <para>
/// It is an options type rather than a service so that
/// <c>~/Views/Shared/UBookIt/_Styles.cshtml</c> can inject it in a host that never
/// registered a theme: <c>IOptions&lt;T&gt;</c> always resolves, and an unregistered
/// theme is simply the default value — no theme, stylesheet emitted.
/// </para>
/// </summary>
public sealed class UBookItThemeOptions
{
    /// <summary>
    /// The registered theme's name, which is also the path segment its views live
    /// under. <c>null</c> or blank means no theme: the package's own views render
    /// and the package's stylesheet is emitted as before.
    /// </summary>
    public string? ThemeName { get; set; }

    /// <summary>
    /// Whether the theme wants the package's stylesheet emitted.
    /// <para>
    /// Default <c>false</c>: a theme owns the markup, so the package's classes
    /// largely do not exist in the rendered document and the stylesheet would style
    /// nothing while still being able to collide with the theme's own. A theme that
    /// reuses the package's shared partials as building blocks sets this and gets
    /// exactly what an unthemed site gets.
    /// </para>
    /// </summary>
    public bool UsePackageStylesheet { get; set; }

    /// <summary>Whether a theme is registered at all.</summary>
    public bool IsActive => !string.IsNullOrWhiteSpace(ThemeName);
}
