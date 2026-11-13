using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// <c>scripts/ci/Assert-LineParity.ps1</c>, run for real through <c>pwsh</c> against a throwaway
/// git repository whose <c>refs/remotes/origin/&lt;line&gt;</c> refs stand for each published
/// line's tip.
/// </summary>
/// <remarks>
/// The check had no test before the third line arrived. It now compares every listed line, with
/// two exempt values (the package <c>&lt;Version&gt;</c> and <c>global.json</c>'s
/// <c>sdk.version</c>), so each exemption is tested from both sides: the exempt value may differ,
/// and its neighbours may not.
/// </remarks>
public sealed class LineParityScriptTests : IDisposable
{
    private static readonly string Script =
        Path.Combine(RepoFiles.Root, "scripts", "ci", "Assert-LineParity.ps1");

    private static readonly string[] ThreeLines = ["main", "dev/v18", "dev/v19"];

    private readonly GitScratchRepository _repo = new();
    private readonly string _listDirectory =
        Path.Combine(Path.GetTempPath(), "ubookit-lines-" + Guid.NewGuid().ToString("N"));

    public LineParityScriptTests() => Directory.CreateDirectory(_listDirectory);

    public void Dispose()
    {
        _repo.Dispose();
        Directory.Delete(_listDirectory, recursive: true);
    }

    [Fact]
    public void Identical_lines_pass()
    {
        PublishAll(Definitions("18.6.0"));
        CheckOut(Definitions("17.7.0"));

        var (exit, output) = Run("main");

        Assert.Equal(0, exit);
        Assert.Contains("agree with every other published line: dev/v18, dev/v19", output);
    }

    [Fact]
    public void A_workflow_differing_on_one_line_fails_naming_the_file_and_that_line()
    {
        PublishAll(Definitions("18.6.0"), ("dev/v19", Definitions("19.0.0-rc1", workflow: "old")));
        CheckOut(Definitions("17.7.0"));

        var (exit, output) = Run("main");

        Assert.Equal(1, exit);
        Assert.Contains(".github/workflows/ci.yml differs between main (this commit) and origin/dev/v19", output);
        Assert.DoesNotContain("and origin/dev/v18", output);
    }

    [Fact]
    public void A_file_differing_on_two_lines_names_both()
    {
        PublishAll(Definitions("18.6.0", script: "old"), ("main", Definitions("17.7.0", script: "old")));
        CheckOut(Definitions("19.0.0-rc1"));

        var (exit, output) = Run("dev/v19");

        Assert.Equal(1, exit);
        Assert.Contains("scripts/ci/Check.ps1 differs between dev/v19 (this commit) and origin/main", output);
        Assert.Contains("scripts/ci/Check.ps1 differs between dev/v19 (this commit) and origin/dev/v18", output);
    }

    [Fact]
    public void Only_the_sdk_version_differing_passes()
    {
        PublishAll(Definitions("18.6.0", sdk: "10.0.300"));
        CheckOut(Definitions("19.0.0-rc1", sdk: "11.0.100"));

        var (exit, output) = Run("dev/v19");

        Assert.Equal(0, exit);
        Assert.DoesNotContain("global.json differs", output);
    }

    [Fact]
    public void A_reformatted_global_json_with_the_same_settings_passes()
    {
        PublishAll(Definitions("18.6.0"));
        CheckOut(Definitions("17.7.0", globalJson: """{"sdk":{"rollForward":"latestPatch","version":"10.0.300"}}"""));

        var (exit, _) = Run("main");

        Assert.Equal(0, exit);
    }

    [Fact]
    public void A_different_roll_forward_fails()
    {
        PublishAll(Definitions("18.6.0"));
        CheckOut(Definitions("17.7.0", rollForward: "latestFeature"));

        var (exit, output) = Run("main");

        Assert.Equal(1, exit);
        Assert.Contains("global.json differs between main (this commit) and origin/dev/v18", output);
        Assert.Contains("global.json differs from origin/dev/v18 even with its sdk.version ignored", output);
    }

    [Fact]
    public void An_unparseable_global_json_on_another_line_fails()
    {
        PublishAll(Definitions("18.6.0"), ("dev/v18", Definitions("18.6.0", globalJson: "{ not json")));
        CheckOut(Definitions("17.7.0"));

        var (exit, output) = Run("main");

        Assert.Equal(1, exit);
        Assert.Contains("global.json could not be read or parsed as a JSON object at origin/dev/v18", output);
    }

    [Fact]
    public void Only_the_package_version_differing_passes()
    {
        PublishAll(Definitions("18.6.0"));
        CheckOut(Definitions("17.7.0"));

        var (exit, output) = Run("main");

        Assert.Equal(0, exit);
        Assert.DoesNotContain("Directory.Build.props differs", output);
    }

    [Fact]
    public void Another_build_props_line_differing_fails()
    {
        PublishAll(Definitions("18.6.0"));
        CheckOut(Definitions("17.7.0", warnings: "false"));

        var (exit, output) = Run("main");

        Assert.Equal(1, exit);
        Assert.Contains("Directory.Build.props differs between main (this commit) and origin/dev/v18", output);
    }

    [Fact]
    public void An_unlisted_branch_is_not_parity_checked()
    {
        PublishAll(Definitions("18.6.0"));
        CheckOut(Definitions("16.0.0", workflow: "different"));

        var (exit, output) = Run("dev/v16");

        Assert.Equal(0, exit);
        Assert.Contains("dev/v16 is not a published line (main, dev/v18, dev/v19)", output);
    }

    [Fact]
    public void A_listed_line_missing_from_the_remote_fails_naming_it()
    {
        // dev/v19 is listed but has never been pushed: exactly the state before its first push.
        var tip = _repo.Commit(Definitions("18.6.0"));
        _repo.SetRemoteLine("dev/v18", tip);
        CheckOut(Definitions("17.7.0"));

        var (exit, output) = Run("main");

        Assert.Equal(1, exit);
        Assert.Contains("origin/dev/v19 is not available in this clone", output);
    }

    [Fact]
    public void An_empty_list_fails()
    {
        PublishAll(Definitions("18.6.0"));
        CheckOut(Definitions("17.7.0"));

        var (exit, output) = Run("main", "[]");

        Assert.Equal(1, exit);
        Assert.Contains("is empty", output);
    }

    [Fact]
    public void A_list_naming_a_line_twice_fails()
    {
        PublishAll(Definitions("18.6.0"));
        CheckOut(Definitions("17.7.0"));

        var (exit, output) = Run("main", """["main", "dev/v18", "dev/v18"]""");

        Assert.Equal(1, exit);
        Assert.Contains("names 'dev/v18' more than once", output);
    }

    // ---- fixture ----

    /// <summary>A line's CI definitions, varied one value at a time.</summary>
    private static Dictionary<string, string> Definitions(
        string version,
        string sdk = "10.0.300",
        string rollForward = "latestPatch",
        string workflow = "name: ci",
        string script = "exit 0",
        string warnings = "true",
        string? globalJson = null) => new()
    {
        [".github/workflows/ci.yml"] = workflow + "\n",
        ["scripts/ci/Check.ps1"] = script + "\n",
        ["global.json"] = globalJson ?? $$"""
            {
              "sdk": {
                "version": "{{sdk}}",
                "rollForward": "{{rollForward}}"
              }
            }
            """,
        ["Directory.Build.props"] = $"""
            <Project>
              <PropertyGroup>
                <Version>{version}</Version>
                <TreatWarningsAsErrors>{warnings}</TreatWarningsAsErrors>
              </PropertyGroup>
            </Project>
            """,
    };

    /// <summary>
    /// Every listed line's tip holds <paramref name="definitions"/> (with its own version), except
    /// where an override gives a line its own.
    /// </summary>
    private void PublishAll(
        Dictionary<string, string> definitions,
        params (string Line, Dictionary<string, string> Definitions)[] overrides)
    {
        foreach (var line in ThreeLines)
        {
            var own = overrides.FirstOrDefault(o => o.Line == line).Definitions ?? definitions;
            _repo.SetRemoteLine(line, _repo.Commit(own));
        }
    }

    /// <summary>The commit under test: HEAD.</summary>
    private void CheckOut(Dictionary<string, string> definitions) => _repo.Commit(definitions);

    private (int Exit, string Output) Run(string line, string? list = null)
    {
        var listFile = Path.Combine(_listDirectory, "published-lines.json");
        File.WriteAllText(listFile, list ?? """["main", "dev/v18", "dev/v19"]""");

        return PwshScript.Run(Script, _repo.Root, "-Line", line, "-LinesFile", listFile);
    }
}
