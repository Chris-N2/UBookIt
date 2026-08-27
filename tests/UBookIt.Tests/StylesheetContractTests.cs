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
        => [.. Regex.Matches(css, @"--ubookit-[A-Za-z0-9-]+\s*:").Select(m => m.Value.Trim())];

    [Fact]
    public void The_token_pattern_matches_a_name_containing_a_digit()
    {
        // The character class was `[A-Za-z-]+` and could not reach the colon in
        // `--ubookit-space2:` — backtracking stops at the digit — so such a token was
        // invisible to BOTH halves of the D2 guard: the declaration rule above and the
        // read/documented cross-check below. No token has a digit today, which is
        // exactly why this needs asserting rather than observing: the hole opens the
        // day someone adds one, in the guard this change calls its single
        // silent-failure point.
        Assert.NotEmpty(TokenDeclarationsIn(".a { --ubookit-space2: 1rem; }"));
        Assert.NotEmpty(TokenDeclarationsIn(".a { --ubookit-space-2: 1rem; }"));
        Assert.Empty(TokenDeclarationsIn(".a { gap: var(--ubookit-space2, 1rem); }"));
    }

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

    /// <summary>
    /// Every property that can change the colour a visitor actually sees — both halves
    /// of the contrast pair, and the compositing operations that alter either without
    /// naming a colour at all.
    /// <para>
    /// This list is the rule. The first version of this guard fenced <c>color:</c>
    /// alone, which is the declaration the shipped defect happened to use — and QA
    /// pointed out that <c>opacity: 0.75</c> on the same element produces a
    /// byte-identical result while failing no test, as would a <c>background:</c>
    /// shorthand altering the other half of the pair. That is the *third* time in this
    /// change that a rule has been written against the mechanism someone imagined
    /// rather than the guarantee being made.
    /// </para>
    /// </summary>
    private static readonly string[] ColourAffectingProperties =
    [
        "color",
        "background",
        "background-color",
        "background-image",
        "opacity",
        "filter",
        "backdrop-filter",
        "mix-blend-mode",
        "text-shadow",
        "-webkit-text-fill-color",
    ];

    /// <summary>
    /// Every declaration in the stylesheet whose property can affect rendered colour,
    /// as (property, value) pairs.
    /// </summary>
    private static IReadOnlyList<(string Property, string Value)> ColourAffectingDeclarationsIn(string css)
    {
        var pattern = @"(?<![\w-])(?<property>"
            + string.Join("|", ColourAffectingProperties.Select(Regex.Escape))
            + @")\s*:(?<value>[^;}]+)";

        return
        [
            .. Regex
                .Matches(WithoutComments(css), pattern)
                .Select(m => (m.Groups["property"].Value.Trim(), m.Groups["value"].Value.Trim())),
        ];
    }

    [Fact]
    public void No_text_colour_is_derived_only_inherited()
    {
        // THE GUARD THE ORIGINAL SET COULD NOT PROVIDE, and the reason it could not.
        //
        // Every other colour rule here asks "is this a literal colour?". A derived
        // colour is not literal, so it passed all of them — and the mutation check on
        // the syntax rule explicitly asserts that
        // `color-mix(in srgb, currentColor 50%, transparent)` is clean. The guards
        // were not merely silent about the defect; one of them certified it.
        //
        // The defect: `.ubookit-hint` composited text at 75% alpha, which REDUCES
        // contrast. A host with AA-conformant body text at #767676 (4.54:1) rendered
        // the hint at 2.86:1. The host's own text had to be about 9:1 before the
        // derived hint reached AA. So the shipped default failed 1.4.3 on most real
        // sites, in the package whose stated differentiator is accessibility.
        //
        // The rule that replaces "no literal colour" for text: a `color:` declaration
        // may resolve only to `currentColor` or `inherit`. Both are contrast-neutral —
        // they are whatever the host already chose, which on a conformant host already
        // passes. `color-mix()` stays permitted for BORDERS, which carry no
        // information here and are decoration under 1.4.11.
        var offenders = ColourAffectingDeclarationsIn(Stylesheet())
            .Where(declaration => !IsContrastNeutral(declaration.Value))
            .Select(declaration => $"{declaration.Property}: {declaration.Value}")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "A declaration alters the colour a visitor sees: "
            + string.Join(" | ", offenders)
            + ". Every colour-affecting property must resolve, by default, to something "
            + "that leaves the host's own contrast exactly as it was — currentColor, "
            + "inherit, transparent, none, or a token whose fallback is one of those. "
            + "This covers BOTH halves of the pair and the compositing operations that "
            + "change either without naming a colour: `opacity: 0.75` reproduces the "
            + "defect this rule exists for while naming no colour at all. Recede with "
            + "font weight, or expose a token and let the site own the contrast of its "
            + "own choice.");
    }

    [Fact]
    public void The_derived_text_colour_detector_fires_on_the_defect_it_was_written_for()
    {
        // Every one of these renders text at reduced contrast. The first is the
        // declaration that actually shipped; the rest are the routes QA found to the
        // same outcome once the guard fenced only `color:`. A rule that catches only
        // the first is fencing the mechanism, not the guarantee.
        var defects = new[]
        {
            ".ubookit-hint { color: var(--ubookit-color-muted, color-mix(in srgb, currentColor 75%, transparent)); }",
            ".ubookit-hint { opacity: 0.75; }",
            ".ubookit-errors { background: color-mix(in srgb, currentColor 10%, transparent); }",
            ".ubookit-errors { background-color: color-mix(in srgb, currentColor 10%, transparent); }",
            ".ubookit-hint { filter: opacity(0.75); }",
            ".ubookit-hint { -webkit-text-fill-color: color-mix(in srgb, currentColor 75%, transparent); }",
            ".ubookit-hint { mix-blend-mode: multiply; }",
            ".ubookit-hint { text-shadow: 0 0 2px currentColor; }",
            ".ubookit-booking { background-image: linear-gradient(currentColor, transparent); }",
        };

        foreach (var defect in defects)
        {
            var found = ColourAffectingDeclarationsIn(defect);

            Assert.NotEmpty(found);

            // The PREDICATE rejects it, not merely that the scan sees it — a predicate
            // that degenerated to "always true" would otherwise pass this test.
            Assert.DoesNotContain(found, declaration => IsContrastNeutral(declaration.Value));
        }

        // Permitted forms stay permitted, including token fallbacks and the neutral
        // values the shipped file actually uses.
        foreach (var good in new[]
                 {
                     ".a { color: currentColor; }",
                     ".b { color: inherit; }",
                     ".c { color: var(--ubookit-color-error, currentColor); }",
                     ".d { color: var(--ubookit-color-muted, inherit); }",
                     ".e { background: var(--ubookit-color-surface, transparent); }",
                 })
        {
            Assert.All(
                ColourAffectingDeclarationsIn(good),
                declaration => Assert.True(IsContrastNeutral(declaration.Value), declaration.Value));
        }

        // Non-vacuity: the scan must find the shipped file's own declarations, and must
        // not be fooled by properties that merely CONTAIN a listed name into thinking
        // it has covered them.
        Assert.NotEmpty(ColourAffectingDeclarationsIn(Stylesheet()));
        Assert.Empty(ColourAffectingDeclarationsIn(".x { accent-color: auto; border-color: currentColor; }"));
    }

    /// <summary>
    /// Whether a text-colour value leaves the host's established contrast alone.
    /// <para>
    /// Only <c>currentColor</c> and <c>inherit</c> do — they are whatever the host
    /// already chose, which on a conformant host already passes. A token is neutral
    /// exactly when its own fallback is, since the fallback is what ships.
    /// </para>
    /// </summary>
    private static bool IsContrastNeutral(string value)
    {
        const string Neutral = @"currentColor|inherit|transparent|none|1";

        return Regex.IsMatch(
            value,
            $@"^({Neutral}|var\(\s*--ubookit-[A-Za-z0-9-]+\s*,\s*({Neutral})\s*\))$",
            RegexOptions.IgnoreCase);
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
                "inherit",      // font-family, font-size, box-sizing, muted text
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

    /// <summary>
    /// Selectors that reach a native control — by element name, and by the classes the
    /// package puts directly ON one. `.ubookit-submit` is on the `button` elements
    /// themselves, so a rule matching only element names never saw it and `padding` or
    /// `border-radius` added there passed both this rule and the colour rules.
    /// </summary>
    private static bool ReachesANativeControl(string selector)
        => Regex.IsMatch(selector, @"\b(button|select|input|textarea)\b")
        || selector.Contains("ubookit-submit", StringComparison.Ordinal);

    [Fact]
    public void The_native_control_rule_examines_something()
    {
        // The vacuity guard. This rule can only fail by finding an offending property,
        // so a selector pattern that matched nothing would report a clean bill of
        // health forever — and it is the one rule in this change that shipped without
        // either a vacuity guard or a mutation check.
        var blocks = Regex.Matches(WithoutComments(Stylesheet()), @"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}")
            .Select(m => m.Groups["selector"].Value)
            .Where(ReachesANativeControl)
            .ToList();

        Assert.NotEmpty(blocks);

        // And that it reaches the submit-button class, which it previously did not.
        Assert.Contains(blocks, selector => selector.Contains("ubookit-submit", StringComparison.Ordinal));

        // Mutation check on the predicate itself.
        Assert.True(ReachesANativeControl(".ubookit-booking button"));
        Assert.True(ReachesANativeControl(".ubookit-submit"));
        Assert.False(ReachesANativeControl(".ubookit-field > label"));
    }

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

            if (!ReachesANativeControl(selector))
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
    public void The_choice_rows_keep_both_halves_of_the_target_spacing()
    {
        // 2.5.8 conformance for the start times and the catalogue rests on the SPACING
        // exception — undersized targets whose centres are 24px apart — not on target
        // size, because the real targets are the radio (~13px) and its inline label,
        // and neither reaches 24px. The spacing comes from `min-height` AND the margins
        // together.
        //
        // So the margins are load-bearing for conformance rather than cosmetic, and the
        // original comment in the stylesheet said the opposite ("the row is the
        // target"). A future edit tidying away "spacing" margins would have silently
        // dropped 2.5.8 with every test green. Asserted here so it cannot.
        var css = WithoutComments(Stylesheet());

        foreach (var selector in new[] { ".ubookit-times-option", ".ubookit-catalogue-choice" })
        {
            var block = Regex.Match(css, Regex.Escape(selector) + @"\s*(,[^{]*)?\{(?<body>[^}]*)\}");

            Assert.True(block.Success, $"{selector} has no rule at all.");

            var body = block.Groups["body"].Value;

            Assert.Contains("min-height", body, StringComparison.Ordinal);
            Assert.True(
                Regex.IsMatch(body, @"\bmargin(-block-end|-inline-end|-block|-inline|)\s*:"),
                $"{selector} declares min-height but no margin. Both are needed: 2.5.8 is "
                + "met here by centre-to-centre spacing, not by the size of the target, so "
                + "removing the margin removes the conformance.");
        }
    }

    [Fact]
    public void The_documentation_publishes_exactly_the_tokens_that_exist()
    {
        // design.md recorded this as a mitigation — "tie the statement to the token
        // list, so that adding a colour token forces the statement to be revisited" —
        // and then it was not built. A risk logged as mitigated and left unmitigated is
        // worse than one logged as open, because the next reader stops looking.
        //
        // The token table is where a site author learns what it may set, and the
        // accessibility statement's whole boundary is drawn in terms of it: setting a
        // colour token moves that contrast to the site. So the table drifting from the
        // stylesheet does not merely mislead about layout — it silently misstates who
        // is responsible for a WCAG criterion.
        var docs = RepoFiles.Read("docs/booking-page.md");

        var documented = Regex
            .Matches(docs, @"`(?<token>--ubookit-[A-Za-z0-9-]+)`")
            .Select(m => m.Groups["token"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(PublishedTokens.Order(StringComparer.Ordinal), documented);

        // The DEFAULTS too, not only the names. Tying names alone leaves the column a
        // site author actually reads free to drift — and it is the column the
        // accessibility statement's boundary depends on, since "setting a colour token
        // moves that contrast to you" only makes sense against a stated default.
        //
        // The table therefore quotes each default exactly as the stylesheet declares
        // it, including the color-mix, with the friendly gloss in prose underneath
        // rather than in the cell.
        var css = WithoutComments(Stylesheet());

        foreach (var token in PublishedTokens)
        {
            var declared = FallbackOf(css, token);

            Assert.NotNull(declared);

            var row = Regex.Match(docs, @"\|\s*`" + Regex.Escape(token) + @"`\s*\|\s*`(?<default>[^`]+)`\s*\|");

            Assert.True(row.Success, $"{token} has no table row quoting its default as code.");

            Assert.Equal(Normalise(declared!), Normalise(row.Groups["default"].Value));
        }

        static string Normalise(string value) => Regex.Replace(value, @"\s+", " ").Trim();
    }

    /// <summary>
    /// The fallback a token is read with, extracted by counting parentheses rather than
    /// by regex.
    /// <para>
    /// A regex stopping at the first <c>)</c> truncates
    /// <c>var(--x, color-mix(in srgb, currentColor 35%, transparent))</c> to a value
    /// missing its closer — which is what the first version of this check did, and it
    /// failed loudly rather than silently only by luck of the comparison.
    /// </para>
    /// </summary>
    private static string? FallbackOf(string css, string token)
    {
        var start = css.IndexOf($"var({token}, ", StringComparison.Ordinal);

        if (start < 0)
        {
            return null;
        }

        var cursor = start + $"var({token}, ".Length;
        var depth = 0;

        for (var i = cursor; i < css.Length; i++)
        {
            switch (css[i])
            {
                case '(':
                    depth++;
                    break;

                // The close that brings us below the `var(` we opened is the end of the
                // fallback, however many nested functions sit inside it.
                case ')' when depth == 0:
                    return css[cursor..i];

                case ')':
                    depth--;
                    break;
            }
        }

        return null;
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
            .Matches(css, @"var\(\s*(?<token>--ubookit-[A-Za-z0-9-]+)")
            .Select(m => m.Groups["token"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(PublishedTokens.Order(StringComparer.Ordinal), read);
    }
}
