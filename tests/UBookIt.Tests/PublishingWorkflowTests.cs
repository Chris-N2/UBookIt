using System.Text.RegularExpressions;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The publishing credential stays confined (`release-publishing` — "Publishing holds no
/// long-lived credential and confines the short-lived one"), and the runbook names what the
/// nuget.org policy must match.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why these are tests and not a reviewer's habit.</b> The guarantees are about EVERY workflow,
/// including ones not yet written: only the publish job may request an identity token, that job
/// runs no repository code, and every action is pinned to a commit. When QA round 1 checked them,
/// all held, and nothing would have noticed the day a new workflow declared
/// <c>id-token: write</c> or used a tag-pinned action. A guarantee that quantifies over files
/// nobody has written yet needs a guard that reads every file, every run.
/// </para>
/// <para>
/// <b>What these read, and what they cannot.</b> The workflow files, as text, split into jobs by
/// indentation. There is no YAML library here, and none is added for this. The files are written
/// in one regular style, and each test fails loudly if it cannot find the structure it expects,
/// rather than passing over nothing. They cannot see the repository's own settings (the
/// <c>release</c> environment's reviewers and tag rule) or the nuget.org policy. Those live
/// outside the repository, and <c>docs/publishing.md</c> records their values. The last test
/// holds that record to the workflow, which is as close as a test can get.
/// </para>
/// </remarks>
public class PublishingWorkflowTests
{
    private const string PublishWorkflow = ".github/workflows/publish.yml";
    private const string PublishJob = "publish";

    [Fact]
    public void Only_the_publish_job_can_request_an_identity_token()
    {
        var grants = new List<string>();

        foreach (var workflow in Workflows())
        {
            var lines = Code(RepoFiles.Read(workflow));

            // A top-level permissions block in every workflow. Without one, jobs get the
            // repository's default token permissions, which this repository does not control
            // from here.
            Assert.True(
                lines.Any(line => Regex.IsMatch(line, @"^permissions:")),
                $"{workflow} declares no top-level `permissions:`. Every workflow here states its "
                + "token's permissions, so that none inherits a repository default.");

            foreach (var line in lines)
            {
                Assert.False(
                    Regex.IsMatch(line, @"permissions:\s*write-all"),
                    $"{workflow} grants `write-all`, which includes id-token: write. Only the "
                    + "publish job may request an identity token.");
            }

            foreach (var (job, body) in Jobs(workflow, lines))
            {
                foreach (var line in body.Where(line => line.Contains("id-token", StringComparison.Ordinal)))
                {
                    grants.Add($"{workflow} job '{job}': {line.Trim()}");
                }
            }

            // id-token at WORKFLOW level would reach every job in the file.
            foreach (var line in TopLevel(lines).Where(line => line.Contains("id-token", StringComparison.Ordinal)))
            {
                grants.Add($"{workflow} (workflow level): {line.Trim()}");
            }
        }

        Assert.True(
            grants.Count == 1 && grants[0].StartsWith($"{PublishWorkflow} job '{PublishJob}': id-token: write", StringComparison.Ordinal),
            "Exactly one identity-token grant may exist in this repository's workflows: "
            + $"`id-token: write` on the '{PublishJob}' job of {PublishWorkflow}. Found:\n"
            + (grants.Count == 0 ? "  (none)" : string.Join("\n", grants.Select(grant => "  " + grant))));
    }

    [Fact]
    public void The_publish_job_runs_no_repository_code_and_waits_for_approval()
    {
        var lines = Code(RepoFiles.Read(PublishWorkflow));
        var job = Jobs(PublishWorkflow, lines).Single(pair => pair.Job == PublishJob).Body;

        var offenders = job
            .Where(line => Regex.IsMatch(line, @"uses:\s*(actions/checkout@|\./)"))
            .Select(line => line.Trim())
            .ToList();
        Assert.True(
            offenders.Count == 0,
            $"The '{PublishJob}' job must not check out the repository or run a local action, so "
            + "no code from the repository runs while the one-hour key exists. Found:\n  "
            + string.Join("\n  ", offenders));

        Assert.Contains(job, line => Regex.IsMatch(line, @"^\s+environment:\s*release\s*$"));
        Assert.Contains(job, line => Regex.IsMatch(line, @"^\s+if:\s*\$\{\{\s*github\.event_name == 'push'\s*\}\}\s*$"));
        Assert.Contains(job, line => Regex.IsMatch(line, @"^\s+needs:\s*\[\s*check\s*,\s*pack\s*\]\s*$"));
    }

    [Fact]
    public void Every_action_is_pinned_to_a_full_commit()
    {
        var offenders = new List<string>();
        var pinned = 0;

        foreach (var workflow in Workflows())
        {
            foreach (var line in Code(RepoFiles.Read(workflow)))
            {
                var match = Regex.Match(line, @"^\s*(?:-\s*)?uses:\s*(?<ref>\S+)");
                if (!match.Success)
                {
                    continue;
                }

                var reference = match.Groups["ref"].Value;
                if (Regex.IsMatch(reference, @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+(/[^@\s]+)?@[0-9a-f]{40}$"))
                {
                    pinned++;
                }
                else
                {
                    offenders.Add($"{workflow}: {reference}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Every action must be referenced by a full commit SHA, so a moved tag cannot change "
            + "what runs. Resolve the tag to its commit and keep the tag in a trailing comment:\n  "
            + string.Join("\n  ", offenders));

        // Not vacuous: the three workflows use actions, so a parse that found none is broken.
        Assert.True(pinned > 0, "No `uses:` lines were found in any workflow. The guard parsed nothing.");
    }

    [Fact]
    public void The_runbook_records_what_the_nuget_org_policy_must_match()
    {
        var lines = Code(RepoFiles.Read(PublishWorkflow));
        var job = Jobs(PublishWorkflow, lines).Single(pair => pair.Job == PublishJob).Body;

        var environment = Value(job, @"^\s+environment:\s*(?<value>\S+)\s*$", "environment");
        var user = Value(job, @"^\s+NUGET_USER:\s*(?<value>\S+)\s*$", "NUGET_USER");
        var fileName = Path.GetFileName(PublishWorkflow);

        var runbook = RepoFiles.Read("docs/publishing.md");

        // The policy table's rows, derived from the workflow rather than restated: renaming the
        // environment or the file, or changing the account, fails here until the runbook (and so
        // whoever recreates the policy from it) says the same.
        DocumentationAssert.SaysOnce(runbook, $"| Owner | `{user}`");
        DocumentationAssert.SaysOnce(runbook, $"| Workflow file | `{fileName}`");
        DocumentationAssert.SaysOnce(runbook, $"| Environment | `{environment}` |");
        DocumentationAssert.SaysOnce(runbook, $"**GitHub → Settings → Environments → `{environment}`:**");
    }

    private static IReadOnlyList<string> Workflows()
    {
        // `*.y*ml` takes .yml and .yaml alike: GitHub runs both, so a new workflow must not escape
        // these guards by its extension. Paths are made repo-relative, forward-slashed.
        var workflows = RepoFiles.Paths(".github/workflows", "*.y*ml")
            .Select(path => Path.GetRelativePath(RepoFiles.Root, path).Replace('\\', '/'))
            .ToList();

        Assert.Contains(PublishWorkflow, workflows);
        return workflows;
    }

    /// <summary>The file's lines with comments removed (a `#` at line start or after whitespace).</summary>
    private static IReadOnlyList<string> Code(string text) =>
        text.Split('\n')
            .Select(line => Regex.Replace(line.TrimEnd('\r'), @"(^|\s)#.*$", string.Empty))
            .ToList();

    /// <summary>The lines before `jobs:`, i.e. the workflow-level keys.</summary>
    private static IEnumerable<string> TopLevel(IReadOnlyList<string> lines) =>
        lines.TakeWhile(line => !Regex.IsMatch(line, @"^jobs:\s*$"));

    /// <summary>Each job under `jobs:`, as its name and the lines of its body.</summary>
    private static IReadOnlyList<(string Job, IReadOnlyList<string> Body)> Jobs(string workflow, IReadOnlyList<string> lines)
    {
        var jobs = new List<(string, IReadOnlyList<string>)>();
        var start = lines.ToList().FindIndex(line => Regex.IsMatch(line, @"^jobs:\s*$"));
        Assert.True(start >= 0, $"{workflow} has no `jobs:` key, so its jobs cannot be read.");

        string? current = null;
        var body = new List<string>();
        foreach (var line in lines.Skip(start + 1))
        {
            if (Regex.IsMatch(line, @"^\S"))
            {
                break; // a later top-level key ends `jobs:`
            }

            var header = Regex.Match(line, @"^  (?<name>[A-Za-z0-9_-]+):\s*$");
            if (header.Success)
            {
                if (current is not null)
                {
                    jobs.Add((current, body));
                }

                current = header.Groups["name"].Value;
                body = [];
            }
            else if (current is not null)
            {
                body.Add(line);
            }
        }

        if (current is not null)
        {
            jobs.Add((current, body));
        }

        Assert.True(jobs.Count > 0, $"{workflow} has a `jobs:` key but no job could be read under it.");
        return jobs;
    }

    private static string Value(IEnumerable<string> lines, string pattern, string what)
    {
        var values = lines
            .Select(line => Regex.Match(line, pattern))
            .Where(match => match.Success)
            .Select(match => match.Groups["value"].Value)
            .ToList();

        Assert.True(values.Count == 1, $"Expected exactly one `{what}` in the '{PublishJob}' job; found {values.Count}.");
        return values[0];
    }
}
