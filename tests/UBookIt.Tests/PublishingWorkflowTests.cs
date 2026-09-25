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
/// <b>They read every LINE, not the shape they expect.</b> The first version found grants by
/// structure: job bodies, plus the lines before <c>jobs:</c>. QA round 2 put a workflow-level
/// <c>permissions: id-token: write</c> AFTER <c>jobs:</c> (valid YAML), and a flow-style
/// <c>- { uses: actions/checkout@v4 }</c>, into a new file, and all four tests passed. A guard that
/// looks where grants usually are cannot see one placed elsewhere. So the rules are now stated
/// over every non-comment line of every workflow, and structure is used for one thing only: to
/// know which lines are the publish job's, where the single allowed grant lives.
/// </para>
/// <para>
/// <b>What these read, and what they cannot.</b> The workflow files, as text. There is no YAML
/// library here, and none is added for this. Comments are stripped as a <c>#</c> at line start or
/// after whitespace. The tests cannot see the repository's own settings (the <c>release</c>
/// environment's reviewers and tag rule) or the nuget.org policy. Those live outside the
/// repository, and <c>docs/publishing.md</c> records their values. The last test holds that record
/// to the workflow, which is as close as a test can get.
/// </para>
/// </remarks>
public class PublishingWorkflowTests
{
    private const string PublishWorkflow = ".github/workflows/publish.yml";
    private const string PublishJob = "publish";

    /// <summary>
    /// The only actions the publish job may use: none of them touches the repository's code.
    /// Anything else there (a checkout action of any make, a local action) is refused.
    /// </summary>
    private static readonly string[] PublishJobActions =
    [
        "actions/download-artifact",
        "actions/setup-dotnet",
        "NuGet/login",
    ];

    [Fact]
    public void Only_the_publish_job_can_request_an_identity_token()
    {
        var allowed = new List<string>();
        var offenders = new List<string>();

        foreach (var workflow in Workflows())
        {
            var lines = Code(RepoFiles.Read(workflow));

            // A workflow-level permissions block in every workflow, WHEREVER it sits in the file.
            // Without one, jobs get the repository's default token permissions.
            Assert.True(
                lines.Any(line => Regex.IsMatch(line, @"^permissions:")),
                $"{workflow} declares no workflow-level `permissions:`. Every workflow here states "
                + "its token's permissions, so that none inherits a repository default.");

            var publishSpan = workflow == PublishWorkflow
                ? Jobs(workflow, lines).Single(job => job.Name == PublishJob)
                : null;

            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];

                if (Regex.IsMatch(line, @"\bwrite-all\b"))
                {
                    offenders.Add($"{workflow}:{index + 1}: {line.Trim()}  (write-all includes id-token)");
                }

                if (!line.Contains("id-token", StringComparison.Ordinal))
                {
                    continue;
                }

                if (publishSpan is not null && index >= publishSpan.Start && index < publishSpan.End)
                {
                    allowed.Add($"{workflow}:{index + 1}: {line.Trim()}");
                }
                else
                {
                    offenders.Add($"{workflow}:{index + 1}: {line.Trim()}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Only the '" + PublishJob + "' job of " + PublishWorkflow + " may request an identity "
            + "token. These lines grant one elsewhere, or grant everything:\n  "
            + string.Join("\n  ", offenders));

        Assert.True(
            allowed.Count == 1 && Regex.IsMatch(allowed[0], @": id-token:\s*write\s*$"),
            $"The '{PublishJob}' job must carry exactly one grant, `id-token: write`. Found:\n  "
            + (allowed.Count == 0 ? "(none)" : string.Join("\n  ", allowed)));
    }

    [Fact]
    public void The_publish_job_runs_no_repository_code_and_waits_for_approval()
    {
        var lines = Code(RepoFiles.Read(PublishWorkflow));
        var job = Jobs(PublishWorkflow, lines).Single(candidate => candidate.Name == PublishJob);
        var body = lines.Skip(job.Start).Take(job.End - job.Start).ToList();

        var offenders = new List<string>();

        foreach (var reference in body.SelectMany(Uses))
        {
            var action = Regex.Replace(reference, "@.*$", string.Empty);
            if (!PublishJobActions.Contains(action, StringComparer.Ordinal))
            {
                offenders.Add($"uses {reference}");
            }
        }

        // A checkout need not be an action: git or gh on a run line fetches the repository just
        // as well.
        offenders.AddRange(body
            .Where(line => Regex.IsMatch(line, @"(^|[\s;&|(`$])(git|gh)\s"))
            .Select(line => $"runs {line.Trim()}"));

        Assert.True(
            offenders.Count == 0,
            $"The '{PublishJob}' job may use only {string.Join(", ", PublishJobActions)} and must "
            + "not run git or gh, so no code from the repository runs while the one-hour key "
            + "exists. Found:\n  " + string.Join("\n  ", offenders));

        Assert.Contains(body, line => Regex.IsMatch(line, @"^\s+environment:\s*release\s*$"));
        Assert.Contains(body, line => Regex.IsMatch(line, @"^\s+if:\s*\$\{\{\s*github\.event_name == 'push'\s*\}\}\s*$"));
        Assert.Contains(body, line => Regex.IsMatch(line, @"^\s+needs:\s*\[\s*check\s*,\s*pack\s*\]\s*$"));
    }

    [Fact]
    public void Every_action_is_pinned_to_a_full_commit()
    {
        var offenders = new List<string>();
        var pinned = 0;

        foreach (var workflow in Workflows())
        {
            foreach (var reference in Code(RepoFiles.Read(workflow)).SelectMany(Uses))
            {
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
        Assert.True(pinned > 0, "No `uses:` was found in any workflow. The guard parsed nothing.");
    }

    [Fact]
    public void The_runbook_records_what_the_nuget_org_policy_must_match()
    {
        var lines = Code(RepoFiles.Read(PublishWorkflow));
        var job = Jobs(PublishWorkflow, lines).Single(candidate => candidate.Name == PublishJob);
        var body = lines.Skip(job.Start).Take(job.End - job.Start).ToList();

        var environment = Value(body, @"^\s+environment:\s*(?<value>\S+)\s*$", "environment");
        var user = Value(body, @"^\s+NUGET_USER:\s*(?<value>\S+)\s*$", "NUGET_USER");
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

    /// <summary>
    /// Every action a line references, in block style (`uses: x`, `- uses: x`) or flow style
    /// (`- { uses: x }`), quoted or not.
    /// </summary>
    private static IEnumerable<string> Uses(string line) =>
        Regex.Matches(line, @"\buses:\s*['""]?(?<ref>[^\s'"",}]+)")
            .Select(match => match.Groups["ref"].Value);

    private sealed record JobSpan(string Name, int Start, int End);

    /// <summary>
    /// Each job under `jobs:`, as its name and the half-open range of line indexes its body
    /// occupies. Used only to know WHICH lines belong to a job; rules about what may appear in a
    /// workflow are applied to every line, not just to these.
    /// </summary>
    private static IReadOnlyList<JobSpan> Jobs(string workflow, IReadOnlyList<string> lines)
    {
        var start = lines.ToList().FindIndex(line => Regex.IsMatch(line, @"^jobs:\s*$"));
        Assert.True(start >= 0, $"{workflow} has no `jobs:` key, so its jobs cannot be read.");

        var jobs = new List<JobSpan>();
        string? current = null;
        var bodyStart = 0;
        var index = start + 1;

        for (; index < lines.Count; index++)
        {
            var line = lines[index];
            if (Regex.IsMatch(line, @"^\S"))
            {
                break; // a later top-level key ends `jobs:`
            }

            var header = Regex.Match(line, @"^  (?<name>[A-Za-z0-9_-]+):\s*$");
            if (header.Success)
            {
                if (current is not null)
                {
                    jobs.Add(new JobSpan(current, bodyStart, index));
                }

                current = header.Groups["name"].Value;
                bodyStart = index + 1;
            }
        }

        if (current is not null)
        {
            jobs.Add(new JobSpan(current, bodyStart, index));
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
