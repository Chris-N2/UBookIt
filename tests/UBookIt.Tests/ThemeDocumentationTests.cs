using UBookIt.Tests.Support;
using UBookIt.Web.Theming;

namespace UBookIt.Tests;

/// <summary>
/// What the documentation promises about theming, tied to what the code does.
/// <para>
/// A published table drifts, and this one drifts into a specific harm: the required
/// view set is what a theme author builds against, so a table that has fallen behind
/// the contract sends someone to write a theme that fails the completeness check for
/// reasons the documentation caused. The stylesheet's token table is asserted the same
/// way, for the same reason.
/// </para>
/// </summary>
public class ThemeDocumentationTests
{
    private static string Theming() => RepoFiles.Read("docs/theming.md");

    private static string BookingPage() => RepoFiles.Read("docs/booking-page.md");

    [Fact]
    public void The_theming_guide_names_every_required_view_with_its_model()
    {
        var docs = Theming();

        foreach (var required in UBookItThemeContract.RequiredViews)
        {
            var path = $"Components/{required.ComponentName}/{required.ViewName}.cshtml";

            Assert.Contains(path, docs, StringComparison.Ordinal);
            Assert.Contains(required.ModelType.Name, docs, StringComparison.Ordinal);
        }

        // And nothing beyond them, so a view removed from the contract cannot be left
        // in the table as an instruction to write a file nothing will ever render.
        var documented = System.Text.RegularExpressions.Regex
            .Matches(docs, @"`Components/(?<component>\w+)/(?<view>\w+)\.cshtml`")
            .Select(match => $"{match.Groups["component"].Value}/{match.Groups["view"].Value}")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        Assert.Equal(
            UBookItThemeContract.RequiredViews
                .Select(view => $"{view.ComponentName}/{view.ViewName}")
                .Order(StringComparer.Ordinal),
            documented);
    }

    [Fact]
    public void The_theming_guide_names_the_path_convention_and_the_completeness_check()
    {
        var docs = Theming();

        Assert.Contains(UBookItThemeContract.ThemesRoot.TrimStart('/'), docs, StringComparison.Ordinal);
        Assert.Contains(nameof(UBookItThemeCompleteness), docs, StringComparison.Ordinal);
        Assert.Contains("AddUBookItTheme", docs, StringComparison.Ordinal);
        Assert.Contains("usePackageStylesheet", docs, StringComparison.Ordinal);
    }

    [Fact]
    public void The_theming_guide_names_every_shared_partial_a_theme_may_call()
    {
        var docs = Theming();

        foreach (var partial in UBookItThemeContract.SharedPartials)
        {
            Assert.Contains(partial, docs, StringComparison.Ordinal);
        }

        Assert.Contains(UBookItThemeContract.SharedPartialModel.Name, docs, StringComparison.Ordinal);
        Assert.Contains("breaking change", docs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_ownership_documentation_names_the_theme_route_that_works()
    {
        // `packaging` requires the ownership documentation to say how a site changes
        // the markup inside the flow. It now can, so it must say how.
        var docs = BookingPage();

        Assert.Contains("theming.md", docs, StringComparison.Ordinal);
        Assert.Contains("customisable", docs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_ownership_documentation_still_says_the_site_file_override_does_not_work()
    {
        // The route that works existing does not make the trap less likely — it is
        // still the first thing a site author tries, and it still silently does
        // nothing. Dropping this warning when the theme route was documented would
        // have removed the only thing that catches the failed attempt.
        var docs = BookingPage();

        Assert.Contains("does *not* work", docs, StringComparison.Ordinal);
        Assert.Contains("UBookIt.Web.dll", docs, StringComparison.Ordinal);
        Assert.Contains("source checksums", docs, StringComparison.Ordinal);
    }

    [Fact]
    public void The_documentation_distinguishes_measurement_from_expectation()
    {
        // `packaging` forbids asserting more than has been measured, and binds the
        // theme route to the same standard as the override failure — because the
        // theme was first measured on a development site too.
        var docs = BookingPage();

        Assert.Contains("Measured", docs, StringComparison.Ordinal);
        Assert.Contains("Not measured", docs, StringComparison.Ordinal);
        Assert.Contains("RuntimeCompilation", docs, StringComparison.Ordinal);
        Assert.Contains("development site", docs, StringComparison.Ordinal);
    }

    [Fact]
    public void The_accessibility_statement_names_the_rendering_it_describes()
    {
        // The statement must not keep reading as though the shipped markup always
        // renders. Both directions: no claim that a theme is accessible, and no
        // requirement placed on one.
        var docs = BookingPage();

        DocumentationAssert.Says(docs, "These claims describe the views uBookIt ships");
        DocumentationAssert.Says(docs, "no claim about a theme in either direction");

        // The narrowing reaches themed views and nothing else — the clause that keeps
        // this a narrowing rather than an escape hatch.
        DocumentationAssert.Says(docs, "for every view a theme does not supply, everything below holds exactly as written");
    }

    [Fact]
    public void The_no_author_stylesheet_anchor_survives()
    {
        // The clause both narrowings rest on. It is what makes them defensible rather
        // than excuses, and it is exactly the clause a narrowing would plausibly drop.
        // Matched across a line break, because a wrapped sentence defeats a
        // single-line search and reads exactly like a dropped guarantee.
        var docs = BookingPage();

        DocumentationAssert.Says(docs, "with no stylesheet applied at all");
        DocumentationAssert.Says(docs, "which is why no stylesheet can make the flow inoperable");
    }

    [Fact]
    public void Theme_authors_are_told_the_privacy_notice_is_theirs_to_render()
    {
        // FOUND BY MUTATION, not by writing the test first. Listing `_PrivacyNotice` in the
        // building-blocks table was asserted; the paragraph explaining that a theme replacing
        // `_YourDetails` decides whether the notice appears at all was not — so the whole
        // narrative could have been deleted with a green suite, leaving only a table row.
        //
        // It matters more than an ordinary documentation clause because the omission it warns
        // about is invisible: a theme that drops the time picker produces a site that visibly
        // does not work, and a theme that drops the privacy notice produces a site that works
        // perfectly and tells its visitors nothing.
        var docs = Theming();

        DocumentationAssert.Says(docs, "whether that notice appears is your decision");
        DocumentationAssert.Says(docs, "the failure is invisible");
        DocumentationAssert.Says(docs, "either call `_PrivacyNotice` from it or render the same four facts yourself");

        // And that the facts are handed over, which is what makes the choice a real one rather
        // than an instruction to reinvent them.
        DocumentationAssert.Says(docs, "hands your view the facts on");
    }
}
