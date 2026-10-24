using System.Text.RegularExpressions;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Getting started says what the specs require a published page to say about installing, and
/// each of its steps leads to the page that covers it in full.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why these statements are here and not only in the README.</b> The maintainer's site renders
/// the pages <c>docs/docs.json</c> lists and nothing else, so a statement the specs require of
/// "the documentation" that lived only in the README never reached a reader of that site. QA
/// round 1 of <c>docs-single-source</c> found two: the database requirement (the
/// <c>persistence</c> spec's SQL Server 2019 floor was stated nowhere) and the versioning policy
/// (the <c>packaging</c> spec's, README only).
/// </para>
/// </remarks>
public class GettingStartedDocumentationTests
{
    private static string Page() => RepoFiles.Read("docs/getting-started.md");

    [Theory]
    [InlineData("docs/getting-started.md")]
    [InlineData("README.md")]
    public void The_database_requirement_is_stated_in_full(string page)
    {
        var text = RepoFiles.Read(page);

        // Two needles, because the version is in bold and the matcher does not cross a closing
        // emphasis marker before punctuation.
        DocumentationAssert.Says(text, "SQL Server 2019 or later");
        DocumentationAssert.Says(text, "including LocalDB and Azure SQL");
        DocumentationAssert.Says(text, "SQLite is not supported");
    }

    [Fact]
    public void The_versioning_policy_is_stated_where_a_site_owner_installs()
    {
        var text = Page();

        DocumentationAssert.Says(text, "The major follows the Umbraco major it is built for");
        DocumentationAssert.Says(text, "a new major is not a sign of a breaking change");
        DocumentationAssert.Says(text, "uBookIt departs from Semantic Versioning");
        DocumentationAssert.Says(text, "it lands in a minor version, never a patch");
        DocumentationAssert.Says(text, "ships with defaults or an upgrade path that keep an existing site working");
    }

    [Fact]
    public void The_steps_are_the_eight_the_spec_names_in_order()
    {
        var steps = DocsMarkdown.Headings(Page()).Where(h => h.Level == 2).Select(h => h.Text).ToList();

        Assert.Equal(
            [
                "1. Check the requirements",
                "2. Install the package",
                "3. Give people the uBookIt section",
                "4. Create a resource and its opening hours",
                "5. Create a service",
                "6. Create the booking page",
                "7. Turn emails on, and check one arrives",
                "8. Where next",
            ],
            steps);
    }

    /// <summary>
    /// Every step a reference page covers in more depth links one. Step 1 has none: the
    /// requirements are stated in full on this page.
    /// </summary>
    [Fact]
    public void Each_step_after_the_requirements_links_a_reference_page()
    {
        var page = Page();
        var headings = DocsMarkdown.Headings(page).Where(h => h.Level == 2).ToList();
        var links = DocsMarkdown.Links(page).Where(l => Regex.IsMatch(l.Target, @"^[a-z-]+\.md(#|$)")).ToList();

        for (var i = 1; i < headings.Count; i++)
        {
            var start = headings[i].Line;
            var end = i + 1 < headings.Count ? headings[i + 1].Line : int.MaxValue;

            Assert.True(
                links.Any(l => l.Line > start && l.Line < end),
                $"Getting started's step '{headings[i].Text}' links no reference page.");
        }
    }
}
