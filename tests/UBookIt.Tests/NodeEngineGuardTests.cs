using System.Text.Json;
using System.Text.RegularExpressions;
using UBookIt.Tests.Support;
using YamlDotNet.RepresentationModel;

namespace UBookIt.Tests;

/// <summary>
/// The Node that CI builds the backoffice client with satisfies what the client's own
/// dependencies declare they need.
/// <para>
/// npm only <em>warns</em> about an unsupported engine (<c>engine-strict</c> is off), so a
/// dependency raising its floor changes nothing anyone sees. That gap was open for five months:
/// <c>@umbraco-cms/backoffice</c> 18.0.0 declared Node <c>&gt;=24.13</c> while CI ran 22. This
/// guard turns the warning into a failing test.
/// </para>
/// <para>
/// <b>What it reads.</b> Every <c>actions/setup-node</c> step's <c>node-version</c> in
/// <c>.github/workflows</c>, parsed as YAML. And the <c>engines.node</c> of each of the client's
/// <b>direct</b> dependencies, taken from <c>package-lock.json</c>, which records the versions
/// actually installed rather than the ranges asked for. Direct only, because those are the ones
/// we chose and can act on.
/// </para>
/// <para>
/// <b>How it decides.</b> <c>setup-node</c> resolves a bare major <c>M</c> to that major's
/// latest release, so each range is asked whether the latest <c>M.x.y</c> satisfies it. A range is
/// alternatives joined by <c>||</c>; <c>&gt;=a</c> admits <c>M</c> when <c>M &gt;= a</c>, and
/// <c>^a</c> (with <c>a &gt; 0</c>) when <c>M == a</c>.
/// </para>
/// <para>
/// <b>It fails on what it cannot read, never passes.</b> No <c>setup-node</c> step, no direct
/// dependency with an <c>engines.node</c>, a pin that is not a bare major, or an alternative in any
/// other form are all failures: each would otherwise read as "nothing to object to".
/// </para>
/// </summary>
public class NodeEngineGuardTests
{
    private const string WorkflowDirectory = ".github/workflows";
    private const string ClientLockfile = "src/UBookIt.Backoffice/Client/package-lock.json";

    /// <summary>The workflows that build the client. Each must be among the pins read.</summary>
    private static readonly string[] ClientBuildingWorkflows =
    [
        ".github/workflows/ci.yml",
        ".github/workflows/publish.yml",
    ];

    public sealed record NodePin(string Workflow, string Job, string? Version);

    public sealed record EngineRange(string Package, string Version, string Range);

    private static readonly Regex BareMajor = new(@"^\d+$", RegexOptions.CultureInvariant);

    private static readonly Regex Alternative = new(
        @"^(?<op>>=|\^)\s*(?<major>\d+)(?:\.\d+){0,2}$",
        RegexOptions.CultureInvariant);

    [Fact]
    public void Every_pinned_Node_satisfies_every_direct_client_dependency()
    {
        var pins = RepoFiles.Paths(WorkflowDirectory, "*.yml")
            .Select(path => Path.GetRelativePath(RepoFiles.Root, path).Replace('\\', '/'))
            .SelectMany(workflow => SetupNodePins(workflow, RepoFiles.Read(workflow)))
            .ToList();

        foreach (var workflow in ClientBuildingWorkflows)
        {
            Assert.True(
                pins.Any(pin => pin.Workflow == workflow),
                $"{workflow} has no actions/setup-node step, but it builds the backoffice client.");
        }

        var ranges = DirectEngineRanges(RepoFiles.Read(ClientLockfile));

        var problems = Problems(pins, ranges);

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void The_lockfile_reading_finds_the_backoffice_package()
    {
        // The package whose floor this guard exists for. If the reading ever stops seeing it, the
        // main test would still be green on the remaining dependencies.
        var ranges = DirectEngineRanges(RepoFiles.Read(ClientLockfile));

        Assert.Contains(ranges, range => range.Package == "@umbraco-cms/backoffice");
    }

    [Fact]
    public void A_pin_below_a_dependencys_floor_is_reported_with_the_workflow_the_pin_and_the_requirement()
    {
        var problems = Problems(
            [new NodePin(".github/workflows/ci.yml", "build", "22")],
            [new EngineRange("@umbraco-cms/backoffice", "17.8.1", ">=24.13")]);

        var problem = Assert.Single(problems);
        Assert.Contains(".github/workflows/ci.yml", problem);
        Assert.Contains("22", problem);
        Assert.Contains("@umbraco-cms/backoffice", problem);
        Assert.Contains(">=24.13", problem);
    }

    [Fact]
    public void The_ranges_the_client_uses_today_admit_24()
    {
        var problems = Problems(
            [new NodePin("ci.yml", "build", "24")],
            [
                new EngineRange("@umbraco-cms/backoffice", "17.8.1", ">=24.13"),
                new EngineRange("@hey-api/openapi-ts", "0.99.0", ">=22.18.0"),
                new EngineRange("vite", "7.3.6", "^20.19.0 || >=22.12.0"),
                new EngineRange("vitest", "4.1.10", "^20.0.0 || ^22.0.0 || >=24.0.0"),
                new EngineRange("chalk", "5.6.2", "^12.17.0 || ^14.13 || >=16.0.0"),
            ]);

        Assert.Empty(problems);
    }

    [Theory]
    [InlineData(24, ">=24.13", true)]
    [InlineData(24, ">=24", true)]
    [InlineData(23, ">=24.13", false)]
    [InlineData(26, ">=24.13", true)]
    [InlineData(22, "^20.0.0 || ^22.0.0", true)]
    [InlineData(24, "^20.0.0 || ^22.0.0", false)]
    [InlineData(24, "^20.19.0 || >=22.12.0", true)]
    public void Admits_reads_greater_or_equal_and_caret_alternatives(int major, string range, bool expected)
    {
        Assert.Equal(expected, Admits(major, range, out var readable));
        Assert.True(readable);
    }

    [Theory]
    [InlineData("~22.0.0")]
    [InlineData("<26")]
    [InlineData("22.x")]
    [InlineData("20 - 24")]
    [InlineData("^0.12.0")]
    [InlineData(">=24.13 <26")]
    [InlineData("")]
    public void A_range_in_any_other_form_is_unreadable_and_fails(string range)
    {
        var problems = Problems(
            [new NodePin("ci.yml", "build", "24")],
            [new EngineRange("some-package", "1.0.0", range)]);

        var problem = Assert.Single(problems);
        Assert.Contains("cannot read", problem);
    }

    [Theory]
    [InlineData("24.x")]
    [InlineData("lts/*")]
    [InlineData("24.13.0")]
    [InlineData(null)]
    public void A_pin_that_is_not_a_bare_major_is_unreadable_and_fails(string? version)
    {
        var problems = Problems(
            [new NodePin("ci.yml", "build", version)],
            [new EngineRange("@umbraco-cms/backoffice", "17.8.1", ">=24.13")]);

        var problem = Assert.Single(problems);
        Assert.Contains("cannot read", problem);
    }

    [Fact]
    public void No_pins_fails()
    {
        var problems = Problems([], [new EngineRange("@umbraco-cms/backoffice", "17.8.1", ">=24.13")]);

        Assert.Contains(problems, problem => problem.Contains("No actions/setup-node step"));
    }

    [Fact]
    public void No_engine_ranges_fails()
    {
        var problems = Problems([new NodePin("ci.yml", "build", "24")], []);

        Assert.Contains(problems, problem => problem.Contains("No direct client dependency"));
    }

    [Fact]
    public void The_workflow_reading_finds_a_pinned_setup_node_step_and_ignores_other_steps()
    {
        const string yaml = """
            on: push
            jobs:
              build:
                runs-on: ubuntu-latest
                steps:
                  - uses: actions/checkout@0000000000000000000000000000000000000000 # v5
                  - name: Set up Node
                    uses: actions/setup-node@1111111111111111111111111111111111111111 # v7.0.0
                    with:
                      node-version: 24
                      cache: npm
              other:
                runs-on: ubuntu-latest
                steps:
                  - uses: actions/setup-node@v7
                    with:
                      node-version-file: .nvmrc
            """;

        var pins = SetupNodePins("x.yml", yaml);

        Assert.Equal(
            new[] { new NodePin("x.yml", "build", "24"), new NodePin("x.yml", "other", null) },
            pins);
    }

    [Fact]
    public void The_lockfile_reading_takes_direct_dependencies_only_and_skips_those_without_an_engine()
    {
        const string lockfile = """
            {
              "packages": {
                "": {
                  "devDependencies": { "direct-a": "^1.0.0", "direct-b": "^2.0.0" },
                  "dependencies": { "direct-c": "^3.0.0" }
                },
                "node_modules/direct-a": { "version": "1.2.0", "engines": { "node": ">=24.13", "npm": ">=11" } },
                "node_modules/direct-b": { "version": "2.0.0" },
                "node_modules/direct-c": { "version": "3.1.0", "engines": { "node": "^20.0.0 || >=22.0.0" } },
                "node_modules/transitive": { "version": "9.0.0", "engines": { "node": ">=99" } }
              }
            }
            """;

        var ranges = DirectEngineRanges(lockfile);

        Assert.Equal(
            new[]
            {
                new EngineRange("direct-a", "1.2.0", ">=24.13"),
                new EngineRange("direct-c", "3.1.0", "^20.0.0 || >=22.0.0"),
            },
            ranges);
    }

    /// <summary>Every <c>actions/setup-node</c> step in a workflow, with its <c>node-version</c> if it has one.</summary>
    public static IReadOnlyList<NodePin> SetupNodePins(string workflow, string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        Assert.True(stream.Documents.Count >= 1, $"{workflow} contains no YAML document.");

        var pins = new List<NodePin>();

        if (Child(stream.Documents[0].RootNode, "jobs") is not YamlMappingNode jobs)
        {
            return pins;
        }

        foreach (var (jobKey, job) in jobs.Children)
        {
            if (Child(job, "steps") is not YamlSequenceNode steps)
            {
                continue;
            }

            foreach (var step in steps.Children)
            {
                var uses = (Child(step, "uses") as YamlScalarNode)?.Value;
                if (uses is null || !IsSetupNode(uses))
                {
                    continue;
                }

                var version = (Child(Child(step, "with"), "node-version") as YamlScalarNode)?.Value;
                pins.Add(new NodePin(workflow, ((YamlScalarNode)jobKey).Value ?? "", version));
            }
        }

        return pins;
    }

    /// <summary>The <c>engines.node</c> of each direct dependency, as installed according to the lockfile.</summary>
    public static IReadOnlyList<EngineRange> DirectEngineRanges(string lockfileJson)
    {
        using var document = JsonDocument.Parse(lockfileJson);
        var packages = document.RootElement.GetProperty("packages");
        var root = packages.GetProperty("");

        var names = new[] { "devDependencies", "dependencies" }
            .Where(section => root.TryGetProperty(section, out _))
            .SelectMany(section => root.GetProperty(section).EnumerateObject().Select(property => property.Name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var ranges = new List<EngineRange>();

        foreach (var name in names)
        {
            Assert.True(
                packages.TryGetProperty($"node_modules/{name}", out var installed),
                $"Direct dependency {name} is not in the lockfile's installed packages.");

            if (!installed.TryGetProperty("engines", out var engines)
                || engines.ValueKind != JsonValueKind.Object
                || !engines.TryGetProperty("node", out var node))
            {
                continue;
            }

            var version = installed.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";

            // A non-string engines.node is carried as an empty range, which Problems reports as unreadable.
            ranges.Add(new EngineRange(name, version, node.ValueKind == JsonValueKind.String ? node.GetString()! : ""));
        }

        return ranges;
    }

    /// <summary>Every way the pins fail the ranges. Empty means every pin satisfies every range.</summary>
    public static IReadOnlyList<string> Problems(IReadOnlyList<NodePin> pins, IReadOnlyList<EngineRange> ranges)
    {
        var problems = new List<string>();

        if (pins.Count == 0)
        {
            problems.Add("No actions/setup-node step was found in the workflows, so there is no pin to check.");
        }

        if (ranges.Count == 0)
        {
            problems.Add("No direct client dependency declares engines.node, so there is no requirement to check. "
                + "The lockfile reading is more likely broken than the requirement gone.");
        }

        foreach (var pin in pins)
        {
            if (pin.Version is null || !BareMajor.IsMatch(pin.Version))
            {
                problems.Add($"{pin.Workflow} job '{pin.Job}': cannot read node-version '{pin.Version ?? "(absent)"}'. "
                    + "Pin a bare major (for example 24), which is what this guard and the continuous-integration spec expect.");
                continue;
            }

            var major = int.Parse(pin.Version, System.Globalization.CultureInfo.InvariantCulture);

            foreach (var range in ranges)
            {
                var admitted = Admits(major, range.Range, out var readable);

                if (!readable)
                {
                    problems.Add($"{range.Package} {range.Version}: cannot read engines.node '{range.Range}'. "
                        + "This guard understands alternatives joined by '||', each '>=a[.b[.c]]' or '^a[.b[.c]]' with a > 0.");
                }
                else if (!admitted)
                {
                    problems.Add($"{pin.Workflow} job '{pin.Job}' pins node-version {pin.Version}, "
                        + $"but {range.Package} {range.Version} requires Node '{range.Range}'.");
                }
            }
        }

        // An unreadable range is reported once, not once per pin.
        return problems.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Whether the latest release of Node <paramref name="major"/> satisfies <paramref name="range"/>.
    /// <paramref name="readable"/> is false if any alternative is in a form this guard does not interpret.
    /// </summary>
    public static bool Admits(int major, string range, out bool readable)
    {
        var admitted = false;
        readable = true;

        foreach (var raw in range.Split("||"))
        {
            var match = Alternative.Match(raw.Trim());
            if (!match.Success)
            {
                readable = false;
                continue;
            }

            var floor = int.Parse(match.Groups["major"].Value, System.Globalization.CultureInfo.InvariantCulture);

            if (match.Groups["op"].Value == "^")
            {
                if (floor == 0)
                {
                    // ^0.x pins a minor, not a major: not a Node range this guard can answer.
                    readable = false;
                    continue;
                }

                admitted |= major == floor;
            }
            else
            {
                admitted |= major >= floor;
            }
        }

        return admitted && readable;
    }

    private static bool IsSetupNode(string uses) =>
        uses.Equals("actions/setup-node", StringComparison.OrdinalIgnoreCase)
        || uses.StartsWith("actions/setup-node@", StringComparison.OrdinalIgnoreCase);

    private static YamlNode? Child(YamlNode? node, string key) =>
        node is YamlMappingNode mapping
            ? mapping.Children.FirstOrDefault(pair => pair.Key is YamlScalarNode scalar
                && string.Equals(scalar.Value, key, StringComparison.Ordinal)).Value
            : null;
}
