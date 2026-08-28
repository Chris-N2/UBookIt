using System.Text.RegularExpressions;
using UBookIt.Tests.Rendering.Support;

namespace UBookIt.Tests.Rendering;

/// <summary>
/// What the style-emitting partial actually renders.
/// <para>
/// This is the rule that covers the one view held out of <see cref="ViewInventory.All"/>.
/// It is rendered through the package's own rig, which registers no tag helpers and
/// boots no Umbraco — so what it produces here is what it produces in a consuming
/// site, rather than what the TestSite's own tag-helper registration makes of it.
/// </para>
/// </summary>
public class StyleEmissionTests
{
    private readonly ViewRenderer _renderer = new();

    private const string StylesPartial = "~/Views/Shared/UBookIt/_Styles.cshtml";

    [Fact]
    public async Task The_stylesheet_href_is_resolved_and_carries_no_literal_tilde()
    {
        // Why this is worth a test rather than an eyeball. The partial is authored
        // with `href="~/_content/..."`, which is correct: a site hosted under a
        // virtual application path needs the emitted href to carry that PathBase, and
        // a hardcoded "/_content/..." would 404 there. But `~/` resolution is a
        // compile-time concern, and this package deliberately has no
        // `_ViewImports.cshtml` and registers no tag helpers — the standing rule that
        // a tag helper here degrades to visible text rather than to a build error.
        //
        // So "does the tilde actually resolve, on this package's own terms?" is a real
        // question, and the answer must not come from a host that registers all of
        // MVC's tag helpers for its own views.
        var html = await _renderer.RenderAsync(StylesPartial, null);

        Assert.DoesNotContain('~', html);
        Assert.Contains("_content/UBookIt.Web/ubookit.css", html, StringComparison.Ordinal);
        Assert.Contains("rel=\"stylesheet\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task It_emits_exactly_one_link_and_nothing_else()
    {
        // A second link, or any stray markup, would survive a theme's suppression of
        // the package's CSS and fight it — which is the reason the spec requires one
        // emission route rather than merely a tidy one.
        //
        // Asserted UNTRIMMED, and that is the point of this version. Razor emits literal
        // markup including its leading whitespace, so indenting the link element inside
        // the theme condition in `_Styles.cshtml` adds four spaces to every unthemed page
        // — which was measured against a running site and is the one thing this change
        // promised not to do. A trimmed assertion passed either way, so the file's comment
        // saying "column 0 on purpose" was the only thing protecting it.
        var html = await _renderer.RenderAsync(StylesPartial, null);

        Assert.Single(Regex.Matches(html, "<link", RegexOptions.IgnoreCase));
        Assert.StartsWith("<link", html.TrimStart('\r', '\n'), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("    <link", html, StringComparison.Ordinal);
        Assert.EndsWith("/>", html.TrimEnd('\r', '\n'), StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_a_theme_active_it_emits_nothing()
    {
        // A theme owns the markup, so the package's classes largely do not exist in
        // the rendered document: the stylesheet would style nothing while still being
        // able to collide with the theme's own. This partial was built as exactly
        // this off-switch, and its own comments said so before the switch existed.
        var renderer = new ViewRenderer(ThemedRendering.StylesheetOnly(usePackageStylesheet: false));

        var html = (await renderer.RenderAsync(StylesPartial, null)).Trim();

        Assert.Equal(string.Empty, html);
    }

    [Fact]
    public async Task A_theme_may_opt_back_in_and_gets_exactly_what_an_unthemed_site_gets()
    {
        // A theme reusing the package's shared partials as building blocks wants their
        // styling. Byte-for-byte the same as the unthemed emission, rather than
        // "something similar" — a second, nearly-identical emission path is how one
        // route quietly becomes two.
        // Compared untrimmed, so "exactly what an unthemed site emits" is the whole
        // emission and not its trimmed shadow.
        var unthemed = await new ViewRenderer().RenderAsync(StylesPartial, null);

        var themed = await new ViewRenderer(ThemedRendering.StylesheetOnly(usePackageStylesheet: true))
            .RenderAsync(StylesPartial, null);

        Assert.Equal(unthemed, themed);
        Assert.Contains("_content/UBookIt.Web/ubookit.css", themed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_other_view_emits_package_styling_with_a_theme_active()
    {
        // The single-emission-route obligation, STRENGTHENED by theming rather than
        // qualified by it. `ViewInventoryTests` asserts over the shipped source that
        // exactly one view carries a link or style element; this asserts the same
        // thing about what is actually rendered with a theme active, where a second
        // emission route would survive the theme and fight it.
        //
        // Rendered rather than scanned, because the condition added by this change is
        // a runtime condition: a view could emit a link only when a theme is active
        // and the source scan would read identically.
        var renderer = new ViewRenderer(ThemedRendering.StylesheetOnly(usePackageStylesheet: false));

        foreach (var rendered in ViewFixtures.All)
        {
            var html = await renderer.RenderAsync(rendered.ViewPath, rendered.Model);

            Assert.False(
                Regex.IsMatch(html, "<link\\b|<style\\b", RegexOptions.IgnoreCase),
                $"{rendered} emitted package styling with a theme active.");
        }
    }
}
