using System.Text.RegularExpressions;
using UBookIt.Tests.Support;
using YamlDotNet.RepresentationModel;

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
/// <b>They read the YAML, not its text.</b> The first four versions of these tests read the
/// workflows line by line, and each QA round found a neighbouring YAML form the line scanner
/// misread:
/// <list type="bullet">
/// <item><description>round 2: a workflow-level grant placed after <c>jobs:</c>, and flow-style
/// <c>- { uses: … }</c>;</description></item>
/// <item><description>round 3: a quoted job name, and <c>"id\x2Dtoken"</c>;</description></item>
/// <item><description>round 4: a quoted key hidden inside what the scanner took for a block
/// scalar;</description></item>
/// <item><description>round 5: a <c>uses:</c> whose value sat on the next line, and the explicit-key
/// <c>?</c> indicator.</description></item>
/// </list>
/// Each fix taught the scanner one more form. So the rules are now asserted over the tree a real
/// YAML parser (YamlDotNet, test-only) builds. A quoted, escaped, tagged, flow-style, multi-line
/// or explicit key is just a key there.
/// </para>
/// <para>
/// <b>What the parser does not settle, and how that is closed.</b> YamlDotNet's representation
/// model shares an aliased node rather than copying it, and does not apply <c>&lt;&lt;</c> merge
/// keys. Rather than reason about either,
/// <see cref="Every_workflow_is_one_plain_YAML_document"/> refuses anchors (so there can be no
/// aliases), merge keys and a second document. It also keeps the round-3/4 text rule as a
/// BACKSTOP: no quoted keys, anchors, aliases or merge keys on any line, and no backslash outside a
/// block scalar. That rule is not what the other tests rely on. It is there in case GitHub's
/// parser and this one ever read a construct differently, which nothing here can observe.
/// </para>
/// <para>
/// <b>What these cannot see.</b> A publish-job step fetching code from a host other than this
/// repository's and running it; that rests on review, and the spec says so. The repository's own
/// settings (the <c>release</c> environment's reviewers and tag rule) and the nuget.org policy
/// live outside the repository. <c>docs/publishing.md</c> records their values, and the last test
/// holds that record to the workflow, which is as close as a test can get.
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
            var root = Root(workflow);

            Assert.True(
                Child(root, "permissions") is not null,
                $"{workflow} declares no workflow-level `permissions:`. Every workflow here states "
                + "its token's permissions, so that none inherits a repository default.");

            foreach (var (path, key, value) in Entries(root, workflow))
            {
                if (Is(key, "id-token"))
                {
                    var grant = $"{path} = {Describe(value)}";
                    if (workflow == PublishWorkflow && path == $"{PublishWorkflow}/jobs/{PublishJob}/permissions/id-token")
                    {
                        allowed.Add(grant);
                    }
                    else
                    {
                        offenders.Add(grant);
                    }
                }

                if (value is YamlScalarNode scalar && Is(scalar, "write-all"))
                {
                    offenders.Add($"{path} = write-all (which includes id-token)");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"Only the '{PublishJob}' job of {PublishWorkflow} may request an identity token. "
            + "These grant one elsewhere, or grant everything:\n  " + string.Join("\n  ", offenders));

        Assert.True(
            allowed.Count == 1 && allowed[0].EndsWith("= write", StringComparison.Ordinal),
            $"The '{PublishJob}' job must carry exactly one grant, `id-token: write`. Found:\n  "
            + (allowed.Count == 0 ? "(none)" : string.Join("\n  ", allowed)));

        // And nothing else: the job's comment promises "the Trusted Publishing exchange, and
        // nothing else", so a second permission beside it is a widening too.
        var publishPermissions = Child(PublishJobNode(), "permissions") as YamlMappingNode;
        Assert.True(
            publishPermissions is not null && publishPermissions.Children.Count == 1,
            $"The '{PublishJob}' job's permissions must be exactly `id-token: write`. Found: "
            + Describe(Child(PublishJobNode(), "permissions")));
    }

    [Fact]
    public void The_publish_job_runs_no_repository_code_and_waits_for_approval()
    {
        var job = PublishJobNode();
        var offenders = new List<string>();

        foreach (var (path, key, value) in Entries(job, $"{PublishWorkflow}/jobs/{PublishJob}"))
        {
            if (Is(key, "uses"))
            {
                var reference = (value as YamlScalarNode)?.Value ?? Describe(value);
                var action = Regex.Replace(reference, "@.*$", string.Empty);
                if (!PublishJobActions.Contains(action, StringComparer.Ordinal))
                {
                    offenders.Add($"{path}: uses {reference}");
                }
            }
        }

        foreach (var (path, text) in Scalars(job, $"{PublishWorkflow}/jobs/{PublishJob}"))
        {
            // A checkout need not be an action: git or gh fetches the repository just as well,
            // after anything that can start a command word.
            if (Regex.IsMatch(text, @"(^|[\s;&|(`$""'/])(git|gh)\s", RegexOptions.Multiline))
            {
                offenders.Add($"{path}: runs git or gh");
            }

            // Nor may it fetch this repository's content over HTTP.
            if (Regex.IsMatch(text, @"github\.com|githubusercontent\.com|codeload\.github|github\.repository\b|github\.server_url", RegexOptions.IgnoreCase))
            {
                offenders.Add($"{path}: references the repository's own addresses");
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"The '{PublishJob}' job may use only {string.Join(", ", PublishJobActions)}, must not "
            + "run git or gh, and must not reference the repository's own URLs, so no code from "
            + "the repository runs while the one-hour key exists. Found:\n  "
            + string.Join("\n  ", offenders));

        Assert.Equal("release", ScalarChild(job, "environment"));
        Assert.Equal("${{ github.event_name == 'push' }}", ScalarChild(job, "if"));

        var needs = Child(job, "needs") as YamlSequenceNode;
        Assert.True(
            needs is not null && needs.Children.Select(Describe).SequenceEqual(["check", "pack"]),
            $"The '{PublishJob}' job must need exactly [check, pack]. Found: {Describe(Child(job, "needs"))}");
    }

    [Fact]
    public void Every_action_is_pinned_to_a_full_commit()
    {
        var offenders = new List<string>();
        var pinned = 0;

        foreach (var workflow in Workflows())
        {
            foreach (var (path, key, value) in Entries(Root(workflow), workflow))
            {
                if (!Is(key, "uses"))
                {
                    continue;
                }

                var reference = (value as YamlScalarNode)?.Value;
                if (reference is not null && Regex.IsMatch(reference, @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+(/[^@\s]+)?@[0-9a-f]{40}$"))
                {
                    pinned++;
                }
                else
                {
                    offenders.Add($"{path}: {Describe(value)}");
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
    public void Every_workflow_is_one_plain_YAML_document()
    {
        var offenders = new List<string>();

        foreach (var workflow in Workflows())
        {
            var stream = Parse(workflow);
            if (stream.Documents.Count != 1)
            {
                offenders.Add($"{workflow}: {stream.Documents.Count} YAML documents; exactly one is allowed");
                continue;
            }

            // In the tree: an anchor (so no alias can exist) or a merge key is refused, because
            // the representation model shares aliased nodes and does not apply merges.
            foreach (var (path, node) in Nodes(stream.Documents[0].RootNode, workflow))
            {
                if (!node.Anchor.IsEmpty)
                {
                    offenders.Add($"{path}: anchor &{node.Anchor}");
                }
            }

            foreach (var (path, key, _) in Entries(stream.Documents[0].RootNode, workflow))
            {
                if (Is(key, "<<"))
                {
                    offenders.Add($"{path}: merge key");
                }
            }

            // The text BACKSTOP (see the remarks): kept from rounds 3 and 4 in case GitHub's
            // parser and YamlDotNet ever disagree about a construct.
            var lines = Code(RepoFiles.Read(workflow));
            var yamlLines = OutsideBlockScalars(lines).ToHashSet();
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];

                if (Regex.IsMatch(line, @"^\s*(-\s*)?[""'][^""']*[""']\s*:") || Regex.IsMatch(line, @"[{,]\s*[""'][^""']*[""']\s*:"))
                {
                    offenders.Add($"{workflow}:{index + 1}: a quoted key: {line.Trim()}");
                }

                if (Regex.IsMatch(line, @"(^|[:\-\[,{]\s)\s*[&*][A-Za-z0-9_-]") || Regex.IsMatch(line, @"<<\s*:"))
                {
                    offenders.Add($"{workflow}:{index + 1}: an anchor, alias or merge key: {line.Trim()}");
                }

                if (yamlLines.Contains(index) && line.Contains('\\', StringComparison.Ordinal))
                {
                    offenders.Add($"{workflow}:{index + 1}: a backslash outside a block scalar: {line.Trim()}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Workflows here are one YAML document, with no anchors, aliases or merge keys, and with "
            + "plain keys and no backslashes outside `run: |` style blocks, so that what these "
            + "guards read is what GitHub runs. Write it plainly:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void The_runbook_records_what_the_nuget_org_policy_must_match()
    {
        var job = PublishJobNode();
        var environment = ScalarChild(job, "environment");
        var user = ScalarChild(Child(job, "env"), "NUGET_USER");
        var fileName = Path.GetFileName(PublishWorkflow);

        Assert.False(string.IsNullOrWhiteSpace(environment), $"The '{PublishJob}' job names no environment.");
        Assert.False(string.IsNullOrWhiteSpace(user), $"The '{PublishJob}' job sets no NUGET_USER.");

        var runbook = RepoFiles.Read("docs/publishing.md");

        // The policy table's rows, derived from the workflow rather than restated: renaming the
        // environment or the file, or changing the account, fails here until the runbook (and so
        // whoever recreates the policy from it) says the same.
        DocumentationAssert.SaysOnce(runbook, $"| Owner | `{user}`");
        DocumentationAssert.SaysOnce(runbook, $"| Workflow file | `{fileName}`");
        DocumentationAssert.SaysOnce(runbook, $"| Environment | `{environment}` |");
        DocumentationAssert.SaysOnce(runbook, $"**GitHub → Settings → Environments → `{environment}`:**");
    }

    // --- Reading the workflows -------------------------------------------------------------

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

    private static YamlStream Parse(string workflow)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(RepoFiles.Read(workflow)));
        return stream;
    }

    private static YamlMappingNode Root(string workflow)
    {
        var stream = Parse(workflow);
        Assert.True(stream.Documents.Count >= 1, $"{workflow} contains no YAML document.");
        return Assert.IsType<YamlMappingNode>(stream.Documents[0].RootNode);
    }

    private static YamlMappingNode PublishJobNode()
    {
        var jobs = Assert.IsType<YamlMappingNode>(Child(Root(PublishWorkflow), "jobs"));
        return Assert.IsType<YamlMappingNode>(Child(jobs, PublishJob));
    }

    private static bool Is(YamlNode node, string name) =>
        node is YamlScalarNode scalar && string.Equals(scalar.Value, name, StringComparison.OrdinalIgnoreCase);

    private static YamlNode? Child(YamlNode? node, string key) =>
        node is YamlMappingNode mapping
            ? mapping.Children.FirstOrDefault(pair => Is(pair.Key, key)).Value
            : null;

    private static string? ScalarChild(YamlNode? node, string key) => (Child(node, key) as YamlScalarNode)?.Value;

    private static string Describe(YamlNode? node) => node switch
    {
        null => "(absent)",
        YamlScalarNode scalar => scalar.Value ?? "(null)",
        YamlSequenceNode sequence => "[" + string.Join(", ", sequence.Children.Select(Describe)) + "]",
        YamlMappingNode mapping => "{" + string.Join(", ", mapping.Children.Select(pair => $"{Describe(pair.Key)}: {Describe(pair.Value)}")) + "}",
        _ => node.ToString(),
    };

    /// <summary>Every node in the tree, keys included, with a readable path.</summary>
    private static IEnumerable<(string Path, YamlNode Node)> Nodes(YamlNode node, string path)
    {
        yield return (path, node);

        switch (node)
        {
            case YamlMappingNode mapping:
                foreach (var (key, value) in mapping.Children)
                {
                    var childPath = $"{path}/{Describe(key)}";
                    foreach (var inner in Nodes(key, childPath + "(key)")) yield return inner;
                    foreach (var inner in Nodes(value, childPath)) yield return inner;
                }
                break;

            case YamlSequenceNode sequence:
                for (var index = 0; index < sequence.Children.Count; index++)
                {
                    foreach (var inner in Nodes(sequence.Children[index], $"{path}[{index}]")) yield return inner;
                }
                break;
        }
    }

    /// <summary>Every mapping entry in the tree, at any depth, with the path to its value.</summary>
    private static IEnumerable<(string Path, YamlNode Key, YamlNode Value)> Entries(YamlNode node, string path) =>
        Nodes(node, path)
            .Where(pair => pair.Node is YamlMappingNode)
            .SelectMany(pair => ((YamlMappingNode)pair.Node).Children
                .Select(child => ($"{pair.Path}/{Describe(child.Key)}", child.Key, child.Value)));

    /// <summary>Every scalar value in the tree (not keys), with its path.</summary>
    private static IEnumerable<(string Path, string Text)> Scalars(YamlNode node, string path) =>
        Nodes(node, path)
            .Where(pair => pair.Node is YamlScalarNode && !pair.Path.EndsWith("(key)", StringComparison.Ordinal))
            .Select(pair => (pair.Path, ((YamlScalarNode)pair.Node).Value ?? string.Empty));

    // --- The text backstop -------------------------------------------------------------------

    /// <summary>The file's lines with comments removed (a `#` at line start or after whitespace).</summary>
    private static IReadOnlyList<string> Code(string text) =>
        text.Split('\n')
            .Select(line => Regex.Replace(line.TrimEnd('\r'), @"(^|\s)#.*$", string.Empty))
            .ToList();

    /// <summary>
    /// The indexes of lines that are YAML, not the body of a block scalar. Used only to exempt
    /// shell backslashes in the backstop; for `- key: |` the block indent is the key's column.
    /// </summary>
    private static IEnumerable<int> OutsideBlockScalars(IReadOnlyList<string> lines)
    {
        var blockIndent = -1;

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var indent = line.Length - line.TrimStart(' ').Length;

            if (blockIndent >= 0)
            {
                if (line.Trim().Length == 0 || indent > blockIndent)
                {
                    continue;
                }

                blockIndent = -1;
            }

            if (Regex.IsMatch(line, @"(:|^\s*-)\s*[|>][-+0-9]*\s*$"))
            {
                var sequenceItemKey = Regex.Match(line, @"^\s*-\s+(?=\S+\s*:)");
                blockIndent = sequenceItemKey.Success ? sequenceItemKey.Length : indent;
            }

            yield return index;
        }
    }
}
