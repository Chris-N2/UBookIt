using System.Text.RegularExpressions;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The class vocabulary as a published contract.
/// <para>
/// The classes the views render are the surface a consuming site writes CSS
/// against, so they are a compatibility promise from this change onward. Renaming
/// one after release breaks every site that styled it — which is why the
/// vocabulary was corrected in this change, while correcting it was still free.
/// </para>
/// <para>
/// Ids are deliberately not covered here. They are the accessibility contract —
/// aria targets and the error summary's link targets — governed by the rules about
/// resolving references, and they are not appearance.
/// </para>
/// </summary>
public class ClassVocabularyTests
{
    private const string ViewsRoot = "src/UBookIt.Web/Views";

    /// <summary>
    /// Views outside the styling contract, with the reason.
    /// </summary>
    /// <remarks>
    /// <b>The cancellation pages are standalone documents the package serves itself.</b> They set
    /// <c>Layout = null</c>, link no stylesheet and are not composed into a site's page, so
    /// <b>nothing a site writes can reach them</b> — no host layout, no cascade, no token override.
    /// <b>So they render no classes at all</b>, and the published vocabulary still describes,
    /// exactly and completely, everything the package renders.
    /// <para>
    /// They were briefly added to that vocabulary under a comment saying "a site styling the flow
    /// can style these too". QA established that was false, and it is the same shape as the defect
    /// this change already caught itself on: a readout describing something the site does not have.
    /// The first correction dropped the <c>ubookit-</c> prefix, which answered the wrong question —
    /// a class named <c>cancel-booking</c> is still a class this package renders, so
    /// <c>default-frontend</c>'s "exactly and completely" was false either way. The sync-time sweep
    /// caught that; the guard written for the first correction did not, because it forbade the
    /// prefix rather than the thing. Recorded as a removal rather than left silent, so a reader can
    /// tell a decision from an oversight.
    /// </para>
    /// <para>
    /// They are also outside the theme-view set — a theme RCL supplies views under
    /// <c>Views/Shared/UBookIt/Themes/</c>, and these live at <c>Views/Cancellation/</c> — so a
    /// theme cannot replace them either. Both facts are stated in the proposal and the docs.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Whether a shipped view is outside the styling contract — <b>by directory, once.</b>
    /// </summary>
    /// <remarks>
    /// It was specified twice and differently: the class scan filtered by directory while the
    /// button count filtered by FILENAME, so a fourth cancellation view would have been excluded
    /// from one and not the other. Worse, the filename list contained <c>Index.cshtml</c> — the
    /// likeliest filename in any future <c>Views/&lt;Something&gt;/</c> folder — whose buttons
    /// would then have been silently exempt. One predicate, and it names a place rather than a
    /// file.
    /// </remarks>
    private static bool OutsideTheStylingContract(string path)
        => path.Contains(
            $"{Path.DirectorySeparatorChar}Cancellation{Path.DirectorySeparatorChar}",
            StringComparison.Ordinal);

    /// <summary>Every <c>ubookit-</c> class the shipped views render, with the view that renders it.</summary>
    private static IReadOnlyList<(string View, string Class)> RenderedClasses()
    {
        var found = new List<(string, string)>();

        foreach (var path in RepoFiles.Paths(ViewsRoot, "*.cshtml"))
        {
            var name = Path.GetFileName(path);
            var source = Regex.Replace(File.ReadAllText(path), @"@\*.*?\*@", " ", RegexOptions.Singleline);

            foreach (Match attribute in Regex.Matches(source, @"class=""(?<value>[^""]*)"""))
            {
                found.AddRange(
                    attribute.Groups["value"].Value
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Where(token => token.StartsWith("ubookit-", StringComparison.Ordinal))
                        .Select(token => (name, token)));
            }
        }

        Assert.NotEmpty(found);

        return found;
    }

    [Fact]
    public void The_vocabulary_is_exactly_what_was_published()
    {
        // Enumerated, so a rename or an addition is a decision someone makes
        // deliberately rather than a change a site discovers when its CSS stops
        // matching. This is the list the documentation publishes.
        string[] published =
        [
            "ubookit-booked-resources",
            "ubookit-booking",
            "ubookit-booking--service",
            "ubookit-catalogue",
            "ubookit-catalogue-choice",
            "ubookit-catalogue-choices",
            "ubookit-catalogue-form",
            "ubookit-confirmation",
            "ubookit-date-form",
            "ubookit-dates",
            "ubookit-dates-option",
            "ubookit-dates-showing",
            "ubookit-details",
            "ubookit-errors",
            "ubookit-field",
            "ubookit-field-error",
            "ubookit-fixed-length",
            "ubookit-hint",
            "ubookit-no-choices",
            "ubookit-no-dates",
            "ubookit-no-times",
            "ubookit-notice",
            "ubookit-privacy",
            "ubookit-submit",
            "ubookit-times",
            "ubookit-times-option",
        ];

        Assert.Equal(
            published.Order(StringComparer.Ordinal),
            RenderedClasses().Select(found => found.Class).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Every_class_is_a_block_a_part_or_a_variant()
    {
        // The shape rule: lowercase kebab throughout, and at most one variant
        // separator, which may not be leading or trailing.
        foreach (var (view, name) in RenderedClasses())
        {
            Assert.True(
                Regex.IsMatch(name, @"^ubookit-[a-z0-9]+(?:-[a-z0-9]+)*(?:--[a-z0-9]+(?:-[a-z0-9]+)*)?$"),
                $"{view}: '{name}' is not a block, a part of a block, or a variant of one.");
        }
    }

    [Fact]
    public void A_variant_never_appears_without_the_block_it_varies()
    {
        // A variant class styles a block differently; on its own it styles nothing,
        // and a site's `.ubookit-booking` rules would silently not apply to it. So
        // the element carrying `x--y` must carry `x` too.
        foreach (var path in RepoFiles.Paths(ViewsRoot, "*.cshtml"))
        {
            var source = Regex.Replace(File.ReadAllText(path), @"@\*.*?\*@", " ", RegexOptions.Singleline);

            foreach (Match attribute in Regex.Matches(source, @"class=""(?<value>[^""]*)"""))
            {
                var classes = attribute.Groups["value"].Value
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .ToHashSet(StringComparer.Ordinal);

                foreach (var variant in classes.Where(name => name.Contains("--", StringComparison.Ordinal)))
                {
                    var block = variant[..variant.IndexOf("--", StringComparison.Ordinal)];

                    Assert.True(
                        classes.Contains(block),
                        $"{Path.GetFileName(path)}: '{variant}' varies '{block}', which is not on "
                        + "the same element — so every rule written against the block misses it.");
                }
            }
        }
    }

    [Fact]
    public void Every_part_is_prefixed_by_a_block_that_exists()
    {
        // The rule that found `ubookit-time`. It was the item inside `ubookit-times`
        // but was not prefixed by it, so it read as a second block whose name
        // differed from its own container's by one letter — precisely the ambiguity
        // `ubookit-service-booking` had beside `ubookit-booking`.
        //
        // A block is declared here rather than inferred, because "is this a block or
        // an unprefixed part?" is not answerable from the name alone — which is the
        // whole reason the ambiguity existed.
        string[] blocks =
        [
            "ubookit-booked-resources",
            "ubookit-booking",
            "ubookit-catalogue",
            "ubookit-confirmation",
            "ubookit-date-form",
            "ubookit-dates",
            "ubookit-details",
            "ubookit-errors",
            "ubookit-field",
            "ubookit-fixed-length",
            "ubookit-hint",
            "ubookit-no-choices",
            "ubookit-no-dates",
            "ubookit-no-times",
            "ubookit-notice",
            "ubookit-privacy",
            "ubookit-submit",
            "ubookit-times",
        ];

        var rendered = RenderedClasses().Select(found => found.Class).Distinct(StringComparer.Ordinal).ToList();

        // The loophole this closes, found while writing the rule: a part with no
        // block passes the moment someone declares its prefix as a "block", whether
        // or not anything renders it. I did exactly that with `ubookit-date` before
        // noticing. So a declared block must itself be a class the views render —
        // otherwise the list is a place to make failures disappear rather than a
        // record of what the vocabulary is.
        foreach (var block in blocks)
        {
            Assert.True(
                rendered.Contains(block, StringComparer.Ordinal),
                $"'{block}' is declared a block but no view renders it as a class, so it "
                + "cannot be what any part belongs to. Either render it or stop declaring it.");
        }

        foreach (var name in rendered)
        {
            var bare = name.Contains("--", StringComparison.Ordinal)
                ? name[..name.IndexOf("--", StringComparison.Ordinal)]
                : name;

            Assert.True(
                blocks.Contains(bare, StringComparer.Ordinal)
                || blocks.Any(block => bare.StartsWith(block + "-", StringComparison.Ordinal)),
                $"'{name}' is neither a declared block nor prefixed by one, so nothing says "
                + "which block it belongs to. If it is a part, prefix it with its block; if it "
                + "is a block, declare it here.");
        }
    }

    [Fact]
    public void A_view_outside_the_contract_really_is_outside_the_cascade()
    {
        // THE COUNTERPART TO THE EXEMPTION, and without it this is simply a line you can add to
        // switch the button-hook rule off. `ModelReferences.StaticViews` was accepted in the
        // previous round BECAUSE its own guard constrains what it exempts; this one had none.
        //
        // "Outside the cascade" is three facts about the view, all checkable: it sets its own
        // layout to null, it links no stylesheet, and it pulls in no shared styles partial. A view
        // that did any of those would be reachable by a site's CSS, and exempting it would be
        // hiding a contract class rather than recording a page that has none.
        var exempted = RepoFiles.Paths(ViewsRoot, "*.cshtml").Where(OutsideTheStylingContract).ToList();

        Assert.NotEmpty(exempted);

        foreach (var path in exempted)
        {
            var source = File.ReadAllText(path);
            var name = Path.GetFileName(path);

            // The view's NAME travels in the failure messages rather than in an assertion of its
            // own. `Assert.NotEqual(string.Empty, name)` could never fail — a filler assertion
            // inside a guard whose entire subject is assertions that cannot fail, which is the
            // shape this change has now produced five times.
            Assert.True(
                source.Contains("Layout = null", StringComparison.Ordinal),
                $"{name} is exempted from the styling contract but does not set Layout = null.");

            Assert.False(
                source.Contains("<link", StringComparison.OrdinalIgnoreCase),
                $"{name} is exempted from the styling contract but links a stylesheet, so a site's "
                + "CSS reaches it after all.");

            Assert.False(
                source.Contains("_Styles", StringComparison.Ordinal),
                $"{name} is exempted from the styling contract but pulls in the shared styles partial.");

            // AND IT EMITS NO CLASS AT ALL — not merely no `ubookit-` one.
            //
            // `default-frontend` says the vocabulary "continues to describe, exactly and
            // completely, whatever the package itself renders". These ARE the package's own views,
            // so ANY class here is a class the package renders and the vocabulary does not
            // describe: the sibling requirement is falsified by `cancel-booking` exactly as it
            // would be by `ubookit-booking`. Dropping the prefix answered "does this read as a
            // contract class?" and left "is it in the contract?" unanswered.
            //
            // A guard forbidding only the prefix therefore passed while the thing it existed to
            // prevent was true — the shape this change has now produced six times. The classes are
            // gone rather than renamed again, because no stylesheet can reach this page, so they
            // were hooks for nothing.
            Assert.False(
                source.Contains("class=", StringComparison.OrdinalIgnoreCase),
                $"{name} is exempted from the styling contract but renders a class, which the "
                + "published vocabulary then does not describe.");
        }
    }

    [Fact]
    public void The_layout_hooks_are_on_every_field_and_every_submit_control()
    {
        // Pinned counts, so removing a hook fails rather than quietly leaving a row
        // or a button unstyleable. Eight fields: five in _DateAndLength — including
        // the two wrappers that stand in when a control is replaced by settled text —
        // and three in _YourDetails. Three submit controls, one per booking step.
        //
        // The cancellation page's button is deliberately NOT among them: that page is outside the
        // styling contract (see OutsideTheStylingContract), so a layout hook on it would be a hook
        // no stylesheet can use — which is the claim QA found to be false.
        var classes = RenderedClasses();

        Assert.Equal(8, classes.Count(found => found.Class == "ubookit-field"));
        Assert.Equal(3, classes.Count(found => found.Class == "ubookit-submit"));

        // Every button in the package is a submit control and carries the hook, so a
        // button added later without one fails here.
        var buttons = RepoFiles
            .Paths(ViewsRoot, "*.cshtml")
            .Where(path => !OutsideTheStylingContract(path))
            .Sum(path => Regex.Matches(File.ReadAllText(path), @"<button\b").Count);

        Assert.Equal(3, buttons);
    }
}
