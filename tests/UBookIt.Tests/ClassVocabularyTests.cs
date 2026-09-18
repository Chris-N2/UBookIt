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

    /// <summary>Every class the shipped views render, with the view that renders it.</summary>
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

            // self-service-cancellation: the cancellation pages. A NEW BLOCK on a published
            // contract, so it is enumerated here deliberately — a site styling the flow can style
            // these too, and the names will not move under it.
            "ubookit-cancel",
            "ubookit-cancel-booking",
            "ubookit-cancel-resources",
            "ubookit-cancel-zone",

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
            "ubookit-cancel",
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
    public void The_layout_hooks_are_on_every_field_and_every_submit_control()
    {
        // Pinned counts, so removing a hook fails rather than quietly leaving a row
        // or a button unstyleable. Eight fields: five in _DateAndLength — including
        // the two wrappers that stand in when a control is replaced by settled text —
        // and three in _YourDetails. Four submit controls: one per booking step, plus
        // the cancellation page's, which is a submit control for the same reason the
        // others are — the action it performs must not be reachable by a GET.
        var classes = RenderedClasses();

        Assert.Equal(8, classes.Count(found => found.Class == "ubookit-field"));
        Assert.Equal(4, classes.Count(found => found.Class == "ubookit-submit"));

        // Every button in the package is a submit control and carries the hook, so a
        // button added later without one fails here.
        var buttons = RepoFiles
            .Paths(ViewsRoot, "*.cshtml")
            .Sum(path => Regex.Matches(File.ReadAllText(path), @"<button\b").Count);

        Assert.Equal(4, buttons);
    }
}
