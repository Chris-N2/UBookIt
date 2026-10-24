using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Links between the published pages resolve wherever the pages are rendered: on GitHub, and on
/// a site that renders only what <c>docs/docs.json</c> lists.
/// </summary>
/// <remarks>
/// <para>
/// <b>A link that works on GitHub can be dead on the consumer's site.</b> GitHub serves every file
/// in the repository; the site serves the listed pages and the files beneath <c>docs/</c>. So a
/// relative link may name a listed page, optionally with a fragment, or a non-Markdown file
/// beneath <c>docs/</c> such as an image, and nothing else.
/// </para>
/// <para>
/// <b>A fragment is checked against the target page's headings</b>, converted to identifiers by
/// GitHub's rule (pinned in <c>DocsMarkdownTests</c>), which the manifest's <c>headingIds</c>
/// field tells the consumer to follow. Moving a section is the usual way to break one, and this
/// change moved twelve.
/// </para>
/// </remarks>
public class DocumentationLinkTests
{
    /// <summary>The problems with every relative link on one page, given the published set.</summary>
    public static IReadOnlyList<string> ProblemsOn(string file, string markdown, IReadOnlySet<string> listed, Func<string, string?> readPage, Func<string, bool> assetExists)
    {
        var problems = new List<string>();

        foreach (var link in DocsMarkdown.Links(markdown))
        {
            var target = link.Target;

            if (target.Contains("://", StringComparison.Ordinal) || target.StartsWith("mailto:", StringComparison.Ordinal))
            {
                continue;
            }

            var hash = target.IndexOf('#', StringComparison.Ordinal);
            var path = hash < 0 ? target : target[..hash];
            var fragment = hash < 0 ? null : target[(hash + 1)..];
            var where = $"docs/{file} line {link.Line}: '{target}'";

            if (path.Contains("..", StringComparison.Ordinal) || path.StartsWith('/') || path.Contains('\\', StringComparison.Ordinal))
            {
                problems.Add($"{where} leaves docs/.");
                continue;
            }

            string? targetMarkdown;

            if (path.Length == 0)
            {
                targetMarkdown = markdown;
            }
            else if (path.EndsWith(".md", StringComparison.Ordinal))
            {
                if (!listed.Contains(path))
                {
                    problems.Add($"{where} names a page docs/docs.json does not publish.");
                    continue;
                }

                targetMarkdown = readPage(path);
            }
            else
            {
                if (!assetExists(path))
                {
                    problems.Add($"{where} names a file that does not exist.");
                }

                continue;
            }

            if (fragment is not null && targetMarkdown is not null && !DocsMarkdown.HeadingIds(targetMarkdown).Contains(fragment, StringComparer.Ordinal))
            {
                problems.Add($"{where} names a heading that is not on {(path.Length == 0 ? "this page" : path)}.");
            }
        }

        return problems;
    }

    private static IReadOnlySet<string> ListedFiles()
        => DocumentationManifestTests.Listed().Select(p => p.File).ToHashSet(StringComparer.Ordinal);

    private static string? ReadPage(string file) => RepoFiles.Read($"docs/{file}");

    private static bool AssetExists(string path) => File.Exists(Path.Combine(RepoFiles.Root, "docs", path.Replace('/', Path.DirectorySeparatorChar)));

    [Fact]
    public void Every_relative_link_on_a_published_page_resolves()
    {
        var listed = ListedFiles();
        var problems = listed.SelectMany(file => ProblemsOn(file, RepoFiles.Read($"docs/{file}"), listed, ReadPage, AssetExists)).ToList();

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void The_published_pages_link_each_other_at_all()
    {
        // Anti-vacuity: a reader that found no links would pass the test above over nothing.
        var count = ListedFiles().Sum(file => DocsMarkdown.Links(RepoFiles.Read($"docs/{file}")).Count(l => !l.Target.Contains("://", StringComparison.Ordinal)));

        Assert.True(count >= 30, $"Only {count} relative links found across the published pages.");
    }

    [Theory]
    [InlineData("See [the sets](notifications.md#template-sets).", "a heading that is not on notifications.md")]
    [InlineData("See [v1](mvp.md).", "does not publish")]
    [InlineData("See [the runbook](publishing.md#tag-the-release).", "does not publish")]
    [InlineData("See [the readme](../README.md).", "leaves docs/")]
    [InlineData("![A picture](images/no-such-picture.png)", "does not exist")]
    [InlineData("See [below](#no-such-heading).", "not on this page")]
    public void A_broken_link_is_reported(string markdown, string expected)
    {
        var problems = ProblemsOn("scratch.md", markdown, ListedFiles(), ReadPage, AssetExists);

        Assert.Contains(problems, p => p.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void A_good_link_of_each_kind_is_not_reported()
    {
        const string markdown = "[a](email-templates.md#template-sets) [b](booking-page.md) [c](#top) ![d](images/booking-flow.png) [e](https://example.com/x.md)\n\n# Top\n";

        Assert.Empty(ProblemsOn("scratch.md", markdown, ListedFiles(), ReadPage, AssetExists));
    }
}
