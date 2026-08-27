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
        var html = (await _renderer.RenderAsync(StylesPartial, null)).Trim();

        Assert.Single(Regex.Matches(html, "<link", RegexOptions.IgnoreCase));
        Assert.StartsWith("<link", html, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("/>", html, StringComparison.Ordinal);
    }
}
