using Microsoft.AspNetCore.Mvc.Razor;

namespace UBookIt.Web.Theming;

/// <summary>
/// Puts the active theme's view location in front of every other location the
/// engine would search.
/// <para>
/// <b>The format takes <c>{0}</c> and nothing else.</b> View-component resolution
/// calls <c>GetView(executingFilePath, viewName)</c> first — which returns NotFound
/// for a bare name like <c>Catalogue</c>, since it only handles <c>~/</c>, <c>/</c>
/// or a <c>.cshtml</c> suffix — and then <c>FindView(context,
/// "Components/{Component}/{View}")</c>. Only that second call consults
/// <c>ViewLocationFormats</c>, so <c>{0}</c> already carries the
/// <c>Components/X/Y</c> segment. <c>{1}</c> is the <i>controller</i> name, which
/// under this path is whatever controller happens to be executing, and <c>{2}</c>
/// (area) is empty — which is also why Umbraco's own App_Plugins formats degrade to
/// <c>/App_Plugins//Views/...</c> here. Neither is usable and neither is used.
/// </para>
/// </summary>
/// <param name="themeName">
/// The registered theme's name, which is the path segment its views live under.
/// </param>
internal sealed class UBookItThemeViewLocationExpander(string themeName) : IViewLocationExpander
{
    /// <summary>
    /// The root every theme's views live under. Public through
    /// <see cref="UBookItThemeContract"/> so a theme author can be told where to put
    /// files rather than having to read this.
    /// </summary>
    internal const string ThemesRoot = "/Views/Shared/UBookIt/Themes";

    /// <summary>The view-location format for one theme, in MVC's <c>{0}</c> form.</summary>
    internal static string ViewLocationFormatFor(string themeName)
        => $"{ThemesRoot}/{themeName}/{{0}}.cshtml";

    /// <summary>
    /// Deliberately contributes nothing to the view-location cache key.
    /// <para>
    /// There is one theme per application and it is fixed at startup, so there is no
    /// dimension for the cache to vary over. The cost of that choice is stated on
    /// <see cref="UBookItThemeOptions"/> rather than left to be discovered: it is
    /// what makes changing the theme after startup unsupported.
    /// </para>
    /// </summary>
    public void PopulateValues(ViewLocationExpanderContext context)
    {
    }

    /// <summary>
    /// Prepends the theme's location to whatever the chain has produced so far.
    /// <para>
    /// Prepending is only half of what makes the theme win. The other half is
    /// <i>when</i> this expander runs: Umbraco registers two expanders of its own
    /// that prepend six locations, among them <c>/Views/Shared/{0}.cshtml</c>, where
    /// the package's own views are found. An expander that prepends but runs before
    /// theirs is prepended over, and the site then renders correctly and entirely
    /// unthemed. That is why registration is a post-configure — see
    /// <see cref="UBookItThemeBuilderExtensions"/> — and why
    /// <see cref="UBookItThemeBootCheck"/> checks the outcome rather than trusting it.
    /// </para>
    /// </summary>
    public IEnumerable<string> ExpandViewLocations(
        ViewLocationExpanderContext context,
        IEnumerable<string> viewLocations)
        => [ViewLocationFormatFor(themeName), .. viewLocations];
}
