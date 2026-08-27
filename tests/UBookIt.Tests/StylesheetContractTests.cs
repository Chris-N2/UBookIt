using System.Text.RegularExpressions;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The stylesheet's contract, asserted over the shipped file.
/// <para>
/// These are source-level rules because the properties are absences — that no
/// colour is decided, that no token default is declared on an element — and an
/// absence has no runtime representation to assert against. Rendering the flow
/// would prove that these rules hold for the states rendered, never that they hold.
/// </para>
/// <para>
/// Two of them guard failures that are <em>silent and point the wrong way</em>: a
/// token default on a wrapper makes every site override do nothing while every line
/// reads as correct, and a colour decision makes the accessibility statement
/// unfounded without anything looking broken. Each is therefore paired with a check
/// that the detector fires on a known-bad input, because a guard for a silent
/// failure that cannot itself fail is worse than no guard at all.
/// </para>
/// </summary>
public class StylesheetContractTests
{
    private const string StylesheetPath = "src/UBookIt.Web/wwwroot/ubookit.css";

    /// <summary>
    /// Every token the package publishes as its appearance-override contract. The
    /// list is here rather than derived from the file, so that the file and the
    /// published contract are checked against each other rather than against
    /// themselves.
    /// </summary>
    private static readonly string[] PublishedTokens =
    [
        "--ubookit-accent",
        "--ubookit-border-width",
        "--ubookit-color-border",
        "--ubookit-color-error",
        "--ubookit-color-muted",
        "--ubookit-color-surface",
        "--ubookit-field-gap",
        "--ubookit-font-family",
        "--ubookit-font-size",
        "--ubookit-line-height",
        "--ubookit-measure",
        "--ubookit-radius",
        "--ubookit-section-gap",
        "--ubookit-space",
    ];

    private static string Stylesheet() => RepoFiles.Read(StylesheetPath);

    /// <summary>
    /// The file with comments removed. Every rule below runs over this rather than
    /// the raw text: the comments explain the rules in prose, so they mention the
    /// very things the rules forbid. Scanning the raw file would fail on its own
    /// documentation — which is a false positive that teaches contributors to weaken
    /// the rule.
    /// </summary>
    private static string WithoutComments(string css)
        => Regex.Replace(css, @"/\*.*?\*/", " ", RegexOptions.Singleline);

    // ----------------------------------------------------------------- D2 ----

    /// <summary>
    /// A custom property NAME is followed by <c>:</c> only where it is being
    /// declared. Inside <c>var(--x, default)</c> it is followed by <c>,</c> or
    /// <c>)</c>. So this one pattern separates the two uses exactly, with no need to
    /// balance the nested parentheses that <c>color-mix()</c> fallbacks introduce.
    /// </summary>
    private static IReadOnlyList<string> TokenDeclarationsIn(string css)
        => [.. Regex.Matches(css, @"--ubookit-[A-Za-z-]+\s*:").Select(m => m.Value.Trim())];

    [Fact]
    public void No_token_default_is_declared_on_an_element()
    {
        // Design D2, and the change's single silent-failure point. Custom properties
        // inherit, so a default declared on a package wrapper sits closer to the
        // control than a consuming site's `:root` and BEATS it. The site's override
        // then does nothing, with every individual declaration looking correct and
        // nothing anywhere reporting a problem.
        var declared = TokenDeclarationsIn(WithoutComments(Stylesheet()));

        Assert.True(
            declared.Count == 0,
            "The stylesheet declares a uBookIt token on an element: "
            + string.Join(", ", declared)
            + ". Custom properties inherit, so this declaration is closer to the "
            + "control than a site's :root and silently defeats every override the "
            + "site can write. Express the default as a use-site fallback instead — "
            + "var(--ubookit-x, <default>).");
    }

    [Fact]
    public void The_token_declaration_detector_fires_on_a_wrapper_default()
    {
        // The mutation check, run against a synthetic sample rather than by editing
        // the shipped file. Without this, the rule above could pass because it finds
        // nothing anywhere — which is exactly how it would behave if the pattern were
        // wrong.
        const string Bad = ".ubookit-booking { --ubookit-color-error: currentColor; }";
        const string Good = ".ubookit-field-error { color: var(--ubookit-color-error, currentColor); }";
        const string GoodNested =
            ".ubookit-hint { color: var(--ubookit-color-muted, color-mix(in srgb, currentColor 75%, transparent)); }";

        Assert.NotEmpty(TokenDeclarationsIn(Bad));
        Assert.Empty(TokenDeclarationsIn(Good));

        // The nested-parenthesis case the naive "strip var(...)" approach gets wrong.
        Assert.Empty(TokenDeclarationsIn(GoodNested));
    }

    // -------------------------------------------------------------- colour ----

    private static readonly string[] ColourSyntaxes =
        ["rgb(", "rgba(", "hsl(", "hsla(", "hwb(", "lab(", "lch(", "oklab(", "oklch(", "light-dark("];

    private static IReadOnlyList<string> ColourSyntaxOffencesIn(string css)
    {
        var offences = new List<string>();

        // Hex, in every length CSS accepts.
        offences.AddRange(
            Regex.Matches(css, @"#(?:[0-9a-fA-F]{3,4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})\b")
                .Select(m => m.Value));

        offences.AddRange(
            ColourSyntaxes.Where(syntax => css.Contains(syntax, StringComparison.OrdinalIgnoreCase)));

        return offences;
    }

    [Fact]
    public void The_stylesheet_decides_no_colour()
    {
        // Not modesty. A colour chosen here sits on a background this package has
        // never seen, so its contrast is not computable and any claim about it would
        // be unfounded. Choosing none is what lets the accessibility statement say
        // the shipped default cannot fail contrast — so this rule is load-bearing for
        // a published claim, not a style preference.
        var offences = ColourSyntaxOffencesIn(WithoutComments(Stylesheet()));

        Assert.True(
            offences.Count == 0,
            "The stylesheet names a literal colour: " + string.Join(", ", offences)
            + ". The shipped default decides no colour, because a colour it picks "
            + "sits on a background it cannot see. Derive from currentColor, or "
            + "expose a token the site sets.");
    }

    [Fact]
    public void The_colour_syntax_detector_fires_on_each_forbidden_form()
    {
        // Mutation check. `color: red` is deliberately NOT covered here — a named
        // colour is an identifier, and the vocabulary rule below is what catches it.
        Assert.NotEmpty(ColourSyntaxOffencesIn("a { color: #b00020; }"));
        Assert.NotEmpty(ColourSyntaxOffencesIn("a { color: #fff; }"));
        Assert.NotEmpty(ColourSyntaxOffencesIn("a { color: rgb(1 2 3); }"));
        Assert.NotEmpty(ColourSyntaxOffencesIn("a { color: oklch(0.7 0.1 200); }"));
        Assert.Empty(ColourSyntaxOffencesIn("a { color: currentColor; }"));
        Assert.Empty(ColourSyntaxOffencesIn("a { color: color-mix(in srgb, currentColor 50%, transparent); }"));
    }

    [Fact]
    public void The_stylesheet_selects_on_no_id()
    {
        // Ids in this package are the accessibility contract — aria targets and the
        // error summary's link targets. Styling one would put a guarantee about
        // screen-reader behaviour at the mercy of a restyle. With hex colours already
        // forbidden above, any '#' left in the file is an id selector.
        Assert.DoesNotContain('#', WithoutComments(Stylesheet()));
    }

    // ---------------------------------------------------------- vocabulary ----

    /// <summary>
    /// Every identifier appearing in a declaration value, which is where a named
    /// colour would hide. Enumerating the permitted vocabulary rather than listing
    /// the 148 CSS colour names makes the rule complete instead of partial: any new
    /// identifier fails and has to be justified, whether it names a colour or not.
    /// </summary>
    private static IReadOnlyList<string> ValueIdentifiersIn(string css)
    {
        var values = Regex
            .Matches(WithoutComments(css), @":(?<value>[^;{}]+)[;}]")
            .Select(m => m.Groups["value"].Value);

        return
        [
            .. values
                .SelectMany(value => Regex.Matches(value, @"[A-Za-z][A-Za-z0-9-]*").Select(m => m.Value))
                .Where(identifier => !identifier.StartsWith("ubookit", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

    [Fact]
    public void The_value_vocabulary_is_exactly_what_was_approved()
    {
        // The complete guard on named colours, and on appearance creeping in through
        // a value rather than a property. A new identifier here is a decision someone
        // has to make deliberately.
        string[] approved =
            [
                "auto",         // accent-color: let the browser choose
                "border-box",
                "column",       // flex-direction on a field
                "currentColor",
                "flex",
                "in",           // the interpolation-space keyword of color-mix
                "inherit",      // font-family, font-size, box-sizing, color-scheme
                "inline-block", // the wrapping run of start times
                "px",           // the 24px target-size floor
                "rem",
                "solid",        // border styles, which carry emphasis without colour
                "srgb",         // the colour space of every color-mix
                "transparent",  // the far end of every color-mix, and the panel default
                "var",
                "color-mix",
                "block",        // display on the field-error span
            ];

        Assert.Equal(approved.Order(StringComparer.Ordinal), ValueIdentifiersIn(Stylesheet()));
    }

    [Fact]
    public void The_value_vocabulary_scan_is_not_vacuous()
    {
        // A scan that found nothing would approve everything.
        Assert.NotEmpty(ValueIdentifiersIn(Stylesheet()));
        Assert.Contains("currentColor", ValueIdentifiersIn(Stylesheet()));
        Assert.Contains("red", ValueIdentifiersIn("a { color: red; }"));
    }

    // ------------------------------------------------------ native controls ----

    [Fact]
    public void Native_controls_carry_nothing_but_a_target_size()
    {
        // Platform-rendered inputs, selects and buttons are accessible by
        // construction, respect the user's own preferences and behave correctly in
        // forced-colors mode. Restyling them is the most common way a booking UI
        // loses its accessibility, so the package applies a floor and nothing else.
        var blocks = Regex.Matches(
            WithoutComments(Stylesheet()),
            @"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}");

        var offenders = new List<string>();

        foreach (Match block in blocks)
        {
            var selector = block.Groups["selector"].Value;

            if (!Regex.IsMatch(selector, @"\b(button|select|input)\b"))
            {
                continue;
            }

            var properties = Regex
                .Matches(block.Groups["body"].Value, @"(?<property>[a-z-]+)\s*:")
                .Select(m => m.Groups["property"].Value)
                .Where(property => property is not ("min-height" or "min-width"));

            offenders.AddRange(properties.Select(property => $"{selector.Trim()} → {property}"));
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void Every_published_token_is_read_and_nothing_undocumented_is()
    {
        // Both directions. A documented token nothing reads is a promise the
        // stylesheet does not keep; a token the stylesheet reads that is not
        // documented is a contract surface nobody was told about, and renaming it
        // later would break sites silently.
        var css = WithoutComments(Stylesheet());

        var read = Regex
            .Matches(css, @"var\(\s*(?<token>--ubookit-[A-Za-z-]+)")
            .Select(m => m.Groups["token"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(PublishedTokens.Order(StringComparer.Ordinal), read);
    }
}
