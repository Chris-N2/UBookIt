using System.Diagnostics;
using System.Text.Json;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// <c>scripts/ci/Assert-NoVulnerablePackages.ps1</c>, run for real through <c>pwsh</c> against
/// fixture reports (continuous-integration spec, "Vulnerability advisories are audited
/// separately from the build").
/// <para>
/// <b>The first tests in this repository to run a CI script.</b> The others are verified by
/// hand and by QA. This one earned tests when it gained an EXCEPTION: it is the gate that
/// turns an advisory red, and an exception inside a gate is exactly where a silent widening
/// would hide — a suppression accepted one project too far, or a stale one accepted forever,
/// looks identical to a correct green run.
/// </para>
/// <para>
/// Every test asserts the exit code AND that the output names what it should, so that a test
/// cannot pass because the script failed for some other reason — a fixture the script could
/// not read fails with exit 1 too.
/// </para>
/// <para>
/// The script reads <c>-Report</c> instead of running <c>dotnet list</c>; nothing else differs
/// between this and the workflow. Project files are written beside the report, because the
/// script reads each project's own file to learn what it suppresses and whether it is packable.
/// </para>
/// </summary>
public sealed class AuditScriptTests : IDisposable
{
    private const string Source = "https://api.nuget.org/v3/index.json";
    private const string AdvisoryA = "https://github.com/advisories/GHSA-aaaa-aaaa-aaaa";
    private const string AdvisoryB = "https://github.com/advisories/GHSA-bbbb-bbbb-bbbb";

    private static readonly string Script =
        Path.Combine(RepoFiles.Root, "scripts", "ci", "Assert-NoVulnerablePackages.ps1");

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "ubookit-audit-" + Guid.NewGuid().ToString("N"));

    public AuditScriptTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void A_clean_report_passes()
    {
        var site = Project("Site", packable: false);

        var (exit, output) = Audit([Source], Entry(site));

        Assert.Equal(0, exit);
        Assert.Contains("No known advisories in the graph.", output);
    }

    [Fact]
    public void A_report_that_consulted_no_vulnerability_source_fails()
    {
        var site = Project("Site", packable: false);

        var (exit, output) = Audit(["C:/local/feed/"], Entry(site));

        Assert.Equal(1, exit);
        Assert.Contains("consulted no source that provides vulnerability data", output);
    }

    [Fact]
    public void A_report_naming_no_projects_fails()
    {
        var (exit, output) = Audit([Source]);

        Assert.Equal(1, exit);
        Assert.Contains("names no projects", output);
    }

    [Fact]
    public void A_finding_nobody_suppressed_fails_naming_the_project_and_package()
    {
        var site = Project("Site", packable: false);

        var (exit, output) = Audit([Source], Entry(site, Finding("Some.Package", AdvisoryA)));

        Assert.Equal(1, exit);
        Assert.Contains($"Vulnerable package: Site (net10.0): Some.Package 1.0.0 - High - {AdvisoryA}", output);
    }

    [Fact]
    public void An_advisory_its_own_non_packable_project_suppresses_is_accepted_and_reported()
    {
        var site = Project("Site", packable: false, suppressions: [AdvisoryA]);

        var (exit, output) = Audit([Source], Entry(site, Finding("Some.Package", AdvisoryA)));

        Assert.Equal(0, exit);
        // Reported, never silent: the notice names advisory, package and project, and the summary
        // counts it, so a green run with an accepted finding cannot read as "no advisories".
        Assert.Contains($"::notice title=Accepted advisory::Site (net10.0): Some.Package 1.0.0 - High - {AdvisoryA}", output);
        Assert.Contains("1 finding(s) accepted by a suppression", output);
        Assert.Contains("No unaccepted advisories in the graph.", output);
        Assert.DoesNotContain("No known advisories", output);
    }

    [Fact]
    public void A_suppression_matches_regardless_of_case_and_a_trailing_slash()
    {
        var site = Project("Site", packable: false, suppressions: [AdvisoryA.ToUpperInvariant() + "/"]);

        var (exit, output) = Audit([Source], Entry(site, Finding("Some.Package", AdvisoryA)));

        Assert.Equal(0, exit);
        Assert.Contains("1 finding(s) accepted", output);
    }

    [Fact]
    public void A_suppression_accepts_only_the_advisory_it_names()
    {
        var site = Project("Site", packable: false, suppressions: [AdvisoryA]);

        var (exit, output) = Audit(
            [Source],
            Entry(site, Finding("Some.Package", AdvisoryA), Finding("Some.Package", AdvisoryB)));

        Assert.Equal(1, exit);
        Assert.Contains($"Vulnerable package: Site (net10.0): Some.Package 1.0.0 - High - {AdvisoryB}", output);
        Assert.Contains("1 finding(s) accepted", output);
    }

    [Fact]
    public void A_suppression_does_not_reach_another_project()
    {
        var site = Project("Site", packable: false, suppressions: [AdvisoryA]);
        var tests = Project("Tests", packable: false);

        var (exit, output) = Audit(
            [Source],
            Entry(site, Finding("Some.Package", AdvisoryA)),
            Entry(tests, Finding("Some.Package", AdvisoryA)));

        Assert.Equal(1, exit);
        Assert.Contains($"Vulnerable package: Tests (net10.0): Some.Package 1.0.0 - High - {AdvisoryA}", output);
        Assert.DoesNotContain("Vulnerable package: Site", output);
    }

    [Fact]
    public void A_stale_suppression_fails_naming_the_project_and_advisory()
    {
        var site = Project("Site", packable: false, suppressions: [AdvisoryA]);

        var (exit, output) = Audit([Source], Entry(site));

        Assert.Equal(1, exit);
        Assert.Contains($"Site suppresses {AdvisoryA}, but no such advisory is in its graph any more. The suppression is stale", output);
    }

    [Fact]
    public void A_packable_project_cannot_suppress_even_a_matching_advisory()
    {
        // No IsPackable at all is packable: the SDK default, and the script's fail-closed reading.
        var library = Project("Library", packable: null, suppressions: [AdvisoryA]);

        var (exit, output) = Audit([Source], Entry(library, Finding("Some.Package", AdvisoryA)));

        Assert.Equal(1, exit);
        Assert.Contains($"Library is packable and suppresses {AdvisoryA}", output);
        // And the suppression accepted nothing: the finding fails in its own right.
        Assert.Contains($"Vulnerable package: Library (net10.0): Some.Package 1.0.0 - High - {AdvisoryA}", output);
        Assert.Contains("0 finding(s) accepted", output);
    }

    [Fact]
    public void A_packable_project_cannot_suppress_an_advisory_it_does_not_even_have()
    {
        var library = Project("Library", packable: true, suppressions: [AdvisoryA]);

        var (exit, output) = Audit([Source], Entry(library));

        Assert.Equal(1, exit);
        Assert.Contains($"Library is packable and suppresses {AdvisoryA}", output);
    }

    [Fact]
    public void A_conditional_IsPackable_counts_as_packable()
    {
        var site = Project("Site", packable: false, conditionalPackable: true, suppressions: [AdvisoryA]);

        var (exit, output) = Audit([Source], Entry(site, Finding("Some.Package", AdvisoryA)));

        Assert.Equal(1, exit);
        Assert.Contains($"Site is packable and suppresses {AdvisoryA}", output);
    }

    [Fact]
    public void A_conditional_suppression_fails_and_accepts_nothing()
    {
        var site = Project("Site", packable: false, conditionalSuppressions: [AdvisoryA]);

        var (exit, output) = Audit([Source], Entry(site, Finding("Some.Package", AdvisoryA)));

        Assert.Equal(1, exit);
        Assert.Contains($"Site declares a CONDITIONAL suppression of {AdvisoryA}", output);
        Assert.Contains($"Vulnerable package: Site (net10.0): Some.Package 1.0.0 - High - {AdvisoryA}", output);
    }

    [Fact]
    public void A_project_whose_file_cannot_be_found_suppresses_nothing()
    {
        var missing = Path.Combine(_directory, "Missing.csproj").Replace('\\', '/');

        var (exit, output) = Audit([Source], Entry(missing, Finding("Some.Package", AdvisoryA)));

        Assert.Equal(1, exit);
        Assert.Contains($"Vulnerable package: Missing (net10.0): Some.Package 1.0.0 - High - {AdvisoryA}", output);
    }

    // --- Fixtures ----------------------------------------------------------------------------

    private string Project(
        string name,
        bool? packable,
        bool conditionalPackable = false,
        string[]? suppressions = null,
        string[]? conditionalSuppressions = null)
    {
        var condition = conditionalPackable ? " Condition=\"'$(Configuration)' == 'Release'\"" : string.Empty;
        var isPackable = packable is { } value
            ? $"<PropertyGroup{condition}><IsPackable>{(value ? "true" : "false")}</IsPackable></PropertyGroup>"
            : string.Empty;

        var items = string.Concat((suppressions ?? []).Select(url => $"<NuGetAuditSuppress Include=\"{url}\" />"));
        var conditionalItems = string.Concat(
            (conditionalSuppressions ?? []).Select(url => $"<NuGetAuditSuppress Include=\"{url}\" Condition=\"'$(CI)' == 'true'\" />"));

        var path = Path.Combine(_directory, name + ".csproj");
        File.WriteAllText(
            path,
            $"<Project Sdk=\"Microsoft.NET.Sdk\">{isPackable}<ItemGroup>{items}</ItemGroup>"
            + $"<ItemGroup>{conditionalItems}</ItemGroup></Project>");

        // dotnet list reports forward slashes on every platform; so do the fixtures.
        return path.Replace('\\', '/');
    }

    private static object Finding(string package, string advisory) => new
    {
        id = package,
        resolvedVersion = "1.0.0",
        vulnerabilities = new[] { new { severity = "High", advisoryurl = advisory } },
    };

    private static object Entry(string path, params object[] findings) => findings.Length == 0
        ? new { path }
        : new
        {
            path,
            frameworks = new[] { new { framework = "net10.0", transitivePackages = findings } },
        };

    private (int Exit, string Output) Audit(string[] sources, params object[] projects)
    {
        var report = Path.Combine(_directory, "report.json");
        File.WriteAllText(report, JsonSerializer.Serialize(new { version = 1, sources, projects }));

        var info = new ProcessStartInfo("pwsh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File", Script, "-Target", "Fixture.slnx", "-Report", report })
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("Could not start pwsh.");

        // Both pipes drained concurrently, for the reason PackedSolution.Run gives.
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(milliseconds: 60_000))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException("The audit script did not finish within a minute.");
        }

        Task.WaitAll([stdout, stderr], TimeSpan.FromSeconds(10));
        return (process.ExitCode, stdout.Result + stderr.Result);
    }
}
