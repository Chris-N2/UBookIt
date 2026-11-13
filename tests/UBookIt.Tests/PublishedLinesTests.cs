using System.Text.Json;
using System.Text.RegularExpressions;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The published lines are named once, in <c>scripts/ci/published-lines.json</c>
/// (<c>continuous-integration</c>, <i>The published lines are listed once</i>).
/// </summary>
/// <remarks>
/// <para>
/// The list is the single point of truth for the parity check, the release-tag check and the
/// audit, so a mistake in it drops a line from all of them at once. And <c>ci.yml</c> cannot read
/// it: GitHub filters are static, so the workflow matches published lines by pattern. These guards
/// hold the two together and keep the names from being copied anywhere else.
/// </para>
/// <para>
/// What is NOT guarded here, deliberately: that a <c>dev/vNN</c> line lists itself. A test cannot
/// tell which line it is running on (CI checks out a detached commit). The list is itself a
/// parity-checked file, so a line that drops itself from its own copy differs from every other
/// line's copy, and the next push to any of them fails parity.
/// </para>
/// </remarks>
public class PublishedLinesTests
{
    private static readonly string ListPath =
        Path.Combine(RepoFiles.Root, "scripts", "ci", "published-lines.json");

    private static string[] Lines() =>
        JsonSerializer.Deserialize<string[]>(File.ReadAllText(ListPath))
        ?? throw new InvalidOperationException("published-lines.json is not a JSON array.");

    [Fact]
    public void Every_listed_line_is_one_the_workflows_treat_as_published()
    {
        // ci.yml matches published lines as `main` or startsWith('refs/heads/dev/v'). A name that
        // does not fit would be listed for the scripts and invisible to the workflow: no
        // published-line concurrency, no pull-request trigger, no parity step. `dev/v` plus
        // digits is the stricter half, so a typo such as `dev/19` or `dev/v19-rc` is refused
        // here too.
        var lines = Lines();

        Assert.NotEmpty(lines);
        Assert.All(lines, line => Assert.Matches(@"^(main|dev/v\d+)$", line));
    }

    [Fact]
    public void No_line_is_listed_twice()
    {
        var lines = Lines();

        Assert.Equal(lines.Length, lines.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Main_is_always_a_published_line()
    {
        // main tracks the current LTS (CLAUDE.md, invariant 1); it is never dropped.
        Assert.Contains("main", Lines());
    }

    [Fact]
    public void No_CI_definition_names_a_dev_line_except_the_list()
    {
        // The point of the list: adding or dropping a line is one edit. A literal `dev/v18` left
        // in a workflow or script is a second copy, which drifts. Patterns (`dev/v*`,
        // `dev/v<major>`) are how the workflows refer to the lines; only a concrete number is
        // refused.
        var named = new List<string>();
        foreach (var directory in new[] { ".github", Path.Combine("scripts", "ci") })
        {
            foreach (var file in Directory.EnumerateFiles(
                         Path.Combine(RepoFiles.Root, directory), "*", SearchOption.AllDirectories))
            {
                if (Path.GetFullPath(file) == Path.GetFullPath(ListPath))
                {
                    continue;
                }

                foreach (Match match in Regex.Matches(File.ReadAllText(file), @"dev/v\d+"))
                {
                    named.Add($"{Path.GetRelativePath(RepoFiles.Root, file)}: {match.Value}");
                }
            }
        }

        Assert.Empty(named);
    }
}
