using System.Text.Json;
using System.Text.RegularExpressions;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// <c>docs/docs.json</c> names the published documentation, and it and the pages agree.
/// </summary>
/// <remarks>
/// <para>
/// <b>The manifest is a contract with a consumer outside this repository</b>: the maintainer's
/// site renders the documentation from it at each release tag, and renders nothing it does not
/// list. So a page left out of it is unpublished, a title that disagrees with its page is wrong on
/// that site, and a shape change breaks that site. The <c>documentation</c> spec's first two
/// requirements are what these tests hold.
/// </para>
/// </remarks>
public class DocumentationManifestTests
{
    /// <summary>
    /// Markdown files in <c>docs/</c> that are deliberately not published. Each is for the
    /// maintainer, not for a site using uBookIt; adding one here is a decision, which is why the
    /// list lives in a test rather than in the manifest the consumer reads.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Unlisted = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["mvp.md"] = "The record of what v1 was and why: history for the maintainer, not instructions for a site.",
        ["publishing.md"] = "The release runbook: how the maintainer publishes a version.",
    };

    private static readonly string[] Audiences = ["site", "developer"];

    public sealed record ManifestPage(string File, string Title, string Summary);

    public sealed record ManifestSection(string Title, string Audience, IReadOnlyList<ManifestPage> Pages);

    private static JsonElement Root() => JsonDocument.Parse(RepoFiles.Read("docs/docs.json")).RootElement;

    /// <summary>The manifest's sections, read strictly: a missing or mistyped field fails here.</summary>
    public static IReadOnlyList<ManifestSection> Sections()
        => Root().GetProperty("sections").EnumerateArray()
            .Select(section => new ManifestSection(
                section.GetProperty("title").GetString()!,
                section.GetProperty("audience").GetString()!,
                section.GetProperty("pages").EnumerateArray()
                    .Select(page => new ManifestPage(
                        page.GetProperty("file").GetString()!,
                        page.GetProperty("title").GetString()!,
                        page.GetProperty("summary").GetString()!))
                    .ToList()))
            .ToList();

    /// <summary>Every page the manifest lists, in order.</summary>
    public static IReadOnlyList<ManifestPage> Listed() => Sections().SelectMany(s => s.Pages).ToList();

    /// <summary>
    /// The problems with one page entry, if any. Separate from the test so the scenarios that
    /// say a malformed entry fails can be shown to, without editing the real manifest.
    /// </summary>
    public static IEnumerable<string> ProblemsWith(ManifestPage page)
    {
        if (page.File.Contains('/', StringComparison.Ordinal) || page.File.Contains('\\', StringComparison.Ordinal))
        {
            yield return $"'{page.File}': a file is a bare name in docs/, with no path separator.";
        }

        if (!page.File.EndsWith(".md", StringComparison.Ordinal))
        {
            yield return $"'{page.File}': a file must end in .md.";
        }

        if (string.IsNullOrWhiteSpace(page.Title))
        {
            yield return $"'{page.File}': the title is empty.";
        }

        if (string.IsNullOrWhiteSpace(page.Summary) || page.Summary.Contains('\n', StringComparison.Ordinal) || page.Summary.Contains('\r', StringComparison.Ordinal))
        {
            yield return $"'{page.File}': the summary must be one non-empty line.";
        }
    }

    /// <summary>The problems with one section's audience, if any.</summary>
    public static IEnumerable<string> ProblemsWith(ManifestSection section)
    {
        if (!Audiences.Contains(section.Audience, StringComparer.Ordinal))
        {
            yield return $"Section '{section.Title}': audience '{section.Audience}' is not one of {string.Join(", ", Audiences)}.";
        }

        if (section.Pages.Count == 0)
        {
            yield return $"Section '{section.Title}' lists no pages.";
        }
    }

    [Fact]
    public void The_manifest_declares_its_contract()
    {
        var root = Root();

        Assert.Equal(1, root.GetProperty("schema").GetInt32());
        Assert.Equal("github", root.GetProperty("headingIds").GetString());
        Assert.NotEmpty(Sections());
    }

    [Fact]
    public void Every_entry_is_well_formed()
    {
        var problems = Sections().SelectMany(ProblemsWith)
            .Concat(Listed().SelectMany(ProblemsWith))
            .ToList();

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Theory]
    [InlineData("guides/getting-started.md", "Getting started", "One line.")]
    [InlineData("getting-started", "Getting started", "One line.")]
    [InlineData("getting-started.md", "Getting started", "Two\nlines.")]
    [InlineData("getting-started.md", "", "One line.")]
    public void A_malformed_page_entry_is_reported(string file, string title, string summary)
        => Assert.NotEmpty(ProblemsWith(new ManifestPage(file, title, summary)));

    [Fact]
    public void An_unknown_audience_is_reported()
        => Assert.NotEmpty(ProblemsWith(new ManifestSection("Partners", "partner", [new ManifestPage("a.md", "A", "A.")])));

    [Fact]
    public void Every_listed_file_exists_and_is_listed_once()
    {
        var files = Listed().Select(p => p.File).ToList();

        Assert.Empty(files.GroupBy(f => f, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key));

        foreach (var file in files)
        {
            Assert.True(File.Exists(Path.Combine(RepoFiles.Root, "docs", file)), $"docs/docs.json lists {file}, which is not in docs/.");
        }
    }

    [Fact]
    public void Every_page_in_docs_is_listed_or_deliberately_unlisted()
    {
        var listed = Listed().Select(p => p.File).ToHashSet(StringComparer.Ordinal);
        var onDisk = Directory.EnumerateFiles(Path.Combine(RepoFiles.Root, "docs"), "*.md", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .ToList();

        var neither = onDisk.Where(f => !listed.Contains(f!) && !Unlisted.ContainsKey(f!)).ToList();

        Assert.True(
            neither.Count == 0,
            $"In docs/ but neither listed in docs/docs.json nor named in {nameof(Unlisted)}: {string.Join(", ", neither)}");
    }

    [Fact]
    public void Markdown_below_docs_is_not_hidden_in_a_subfolder()
    {
        // The manifest names bare files in docs/, so a page in a subfolder could never be published.
        var nested = Directory.EnumerateFiles(Path.Combine(RepoFiles.Root, "docs"), "*.md", SearchOption.AllDirectories)
            .Where(path => Path.GetDirectoryName(path) != Path.Combine(RepoFiles.Root, "docs"))
            .ToList();

        Assert.Empty(nested);
    }

    [Fact]
    public void The_maintainer_pages_are_not_published()
    {
        var listed = Listed().Select(p => p.File).ToList();

        foreach (var file in Unlisted.Keys)
        {
            Assert.DoesNotContain(file, listed);
        }
    }

    [Fact]
    public void Each_title_is_its_pages_level_one_heading()
    {
        foreach (var page in Listed())
        {
            var levelOne = DocsMarkdown.Headings(RepoFiles.Read($"docs/{page.File}")).Where(h => h.Level == 1).ToList();

            Assert.True(levelOne.Count == 1, $"docs/{page.File} has {levelOne.Count} level-one headings; it needs exactly one.");
            Assert.True(
                levelOne[0].Text == page.Title,
                $"docs/docs.json titles {page.File} \"{page.Title}\", but its heading reads \"{levelOne[0].Text}\".");
        }
    }

    [Fact]
    public void The_readme_documentation_list_links_every_listed_page()
    {
        var readme = RepoFiles.Read("README.md").Replace("\r\n", "\n", StringComparison.Ordinal);
        var start = readme.IndexOf("\n## Documentation\n", StringComparison.Ordinal);
        Assert.True(start >= 0, "README.md has no ## Documentation section.");

        var end = readme.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        var section = end < 0 ? readme[start..] : readme[start..end];

        var linked = Regex.Matches(section, @"/docs/([A-Za-z0-9._-]+\.md)\)")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var missing = Listed().Select(p => p.File).Where(f => !linked.Contains(f)).ToList();

        Assert.True(missing.Count == 0, $"README.md's Documentation list does not link: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Getting_started_is_first_in_the_site_section()
    {
        var site = Sections().First(s => s.Audience == "site");

        Assert.Equal("getting-started.md", site.Pages[0].File);
        Assert.Equal("site", Sections()[0].Audience);
    }

    [Fact]
    public void No_listed_page_uses_syntax_the_structure_reader_cannot_see()
    {
        foreach (var page in Listed())
        {
            var unsupported = DocsMarkdown.UnsupportedSyntax(RepoFiles.Read($"docs/{page.File}"));

            Assert.True(unsupported.Count == 0, $"docs/{page.File}: {string.Join("; ", unsupported)}");
        }
    }
}
