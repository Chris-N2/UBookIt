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
/// between this and the workflow. The fixture projects are real SDK projects written beside the
/// report, because the script asks MSBuild to EVALUATE each one for its suppressions and its
/// packability. Its first version read the files as XML instead, and QA round 1 showed that
/// reading accepting advisories MSBuild would not — the tests named
/// <c>…_as_MSBuild_does</c> below are those four cases, so the class of mistake stays caught.
/// </para>
/// <para>
/// Conditions use a property nothing sets (<c>UBookItNeverSet</c>), never something like
/// <c>$(CI)</c>: MSBuild reads environment variables as properties, so a condition on one would
/// evaluate differently on a CI runner than on a maintainer's machine.
/// </para>
/// </summary>
public sealed class AuditScriptTests : IDisposable
{
    private const string Source = "https://api.nuget.org/v3/index.json";
    private const string AdvisoryA = "https://github.com/advisories/GHSA-aaaa-aaaa-aaaa";
    private const string AdvisoryB = "https://github.com/advisories/GHSA-bbbb-bbbb-bbbb";

    private const string NotPackable = "<PropertyGroup><IsPackable>false</IsPackable></PropertyGroup>";
    private const string NeverTrue = "'$(UBookItNeverSet)' == 'yes'";

    private static readonly string Script =
        Path.Combine(RepoFiles.Root, "scripts", "ci", "Assert-NoVulnerablePackages.ps1");

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "ubookit-audit-" + Guid.NewGuid().ToString("N"));

    public AuditScriptTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    // --- The unchanged behaviour ------------------------------------------------------------

    [Fact]
    public void A_clean_report_passes()
    {
        var site = Project("Site", NotPackable);

        var (exit, output) = Audit([Source], Entry(site));

        Assert.Equal(0, exit);
        Assert.Contains("No known advisories in the graph.", output);
    }

    [Fact]
    public void A_report_that_consulted_no_vulnerability_source_fails()
    {
        var site = Project("Site", NotPackable);

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
        var site = Project("Site", NotPackable);

        var (exit, output) = Audit([Source], Entry(site, Finding("Some.Package", AdvisoryA)));

        Assert.Equal(1, exit);
        Assert.Contains(Vulnerable("Site", AdvisoryA), output);
    }

    // --- The exception ----------------------------------------------------------------------

    [Fact]
    public void An_advisory_its_own_non_packable_project_suppresses_is_accepted_and_reported()
    {
        var site = Project("Site", NotPackable + Suppress(AdvisoryA));

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
        var site = Project("Site", NotPackable + Suppress(AdvisoryA.ToUpperInvariant() + "/"));

        var (exit, output) = Audit([Source], Entry(site, Finding("Some.Package", AdvisoryA)));

        Assert.Equal(0, exit);
        Assert.Contains("1 finding(s) accepted", output);
    }

    [Fact]
    public void A_suppression_accepts_only_the_advisory_it_names()
    {
        var site = Project("Site", NotPackable + Suppress(AdvisoryA));

        var (exit, output) = Audit(
            [Source],
            Entry(site, Finding("Some.Package", AdvisoryA), Finding("Some.Package", AdvisoryB)));

        Assert.Equal(1, exit);
        Assert.Contains(Vulnerable("Site", AdvisoryB), output);
        Assert.Contains("1 finding(s) accepted", output);
    }

    [Fact]
    public void A_suppression_does_not_reach_another_project()
    {
        var site = Project("Site", NotPackable + Suppress(AdvisoryA));
        var tests = Project("Tests", NotPackable);

        var (exit, output) = Audit(
            [Source],
            Entry(site, Finding("Some.Package", AdvisoryA)),
            Entry(tests, Finding("Some.Package", AdvisoryA)));

        Assert.Equal(1, exit);
        Assert.Contains(Vulnerable("Tests", AdvisoryA), output);
        Assert.DoesNotContain("Vulnerable package: Site", output);
    }

    [Fact]
    public void A_stale_suppression_fails_naming_the_project_and_advisory()
    {
        var site = Project("Site", NotPackable + Suppress(AdvisoryA));

        var (exit, output) = Audit([Source], Entry(site));

        Assert.Equal(1, exit);
        Assert.Contains($"Site suppresses {AdvisoryA}, but no such advisory is in its graph any more. The suppression is stale", output);
    }

    [Fact]
    public void A_packable_project_cannot_suppress_even_a_matching_advisory()
    {
        // No IsPackable at all: the SDK's default, true.
        var library = Project("Library", Suppress(AdvisoryA));

        var (exit, output) = Audit([Source], Entry(library, Finding("Some.Package", AdvisoryA)));

        Assert.Equal(1, exit);
        Assert.Contains($"Library is packable and suppresses {AdvisoryA}", output);
        // And the suppression accepted nothing: the finding fails in its own right.
        Assert.Contains(Vulnerable("Library", AdvisoryA), output);
        Assert.Contains("0 finding(s) accepted", output);
    }

    [Fact]
    public void A_packable_project_cannot_suppress_an_advisory_it_does_not_even_have()
    {
        var library = Project("Library", "<PropertyGroup><IsPackable>true</IsPackable></PropertyGroup>" + Suppress(AdvisoryA));

        var (exit, output) = Audit([Source], Entry(library));

        Assert.Equal(1, exit);
        Assert.Contains($"Library is packable and suppresses {AdvisoryA}", output);
    }

    [Fact]
    public void A_suppression_whose_own_condition_is_false_accepts_nothing()
    {
        var site = Project(
            "Site",
            NotPackable + $"<ItemGroup><NuGetAuditSuppress Include=\"{AdvisoryA}\" Condition=\"{NeverTrue}\" /></ItemGroup>");

        var (exit, output) = Audit([Source], Entry(site, Finding("Some.Package", AdvisoryA)));

        Assert.Equal(1, exit);
        Assert.Contains(Vulnerable("Site", AdvisoryA), output);
    }

    [Fact]
    public void A_suppression_whose_ItemGroup_condition_is_false_accepts_nothing()
    {
        // QA round 1: the parent-ItemGroup half of the old condition check had no test.
        var site = Project(
            "Site",
            NotPackable + $"<ItemGroup Condition=\"{NeverTrue}\"><NuGetAuditSuppress Include=\"{AdvisoryA}\" /></ItemGroup>");

        var (exit, output) = Audit([Source], Entry(site, Finding("Some.Package", AdvisoryA)));

        Assert.Equal(1, exit);
        Assert.Contains(Vulnerable("Site", AdvisoryA), output);
    }

    [Fact]
    public void A_suppression_whose_condition_is_true_is_accepted()
    {
        // The other side of the two above: a condition is not refused for being a condition.
        // What decides is what it evaluates to — as it is for NuGet's audit.
        var site = Project(
            "Site",
            NotPackable + $"<ItemGroup Condition=\"'1' == '1'\"><NuGetAuditSuppress Include=\"{AdvisoryA}\" /></ItemGroup>");

        var (exit, output) = Audit([Source], Entry(site, Finding("Some.Package", AdvisoryA)));

        Assert.Equal(0, exit);
        Assert.Contains("1 finding(s) accepted", output);
    }

    [Fact]
    public void A_project_MSBuild_cannot_evaluate_fails_and_accepts_nothing()
    {
        var missing = Path.Combine(_directory, "Missing.csproj").Replace('\\', '/');

        var (exit, output) = Audit([Source], Entry(missing, Finding("Some.Package", AdvisoryA)));

        Assert.Equal(1, exit);
        Assert.Contains("MSBuild could not evaluate it", output);
        Assert.Contains(Vulnerable("Missing", AdvisoryA), output);
    }

    // --- QA round 1: each place the XML reading disagreed with MSBuild ------------------------

    [Fact]
    public void The_last_IsPackable_decides_as_MSBuild_does()
    {
        var project = Project(
            "Library",
            NotPackable + "<PropertyGroup><IsPackable>true</IsPackable></PropertyGroup>" + Suppress(AdvisoryA));

        var (exit, output) = Audit([Source], Entry(project, Finding("Some.Package", AdvisoryA)));

        Assert.Equal(1, exit);
        Assert.Contains($"Library is packable and suppresses {AdvisoryA}", output);
        Assert.Contains(Vulnerable("Library", AdvisoryA), output);
    }

    [Fact]
    public void A_condition_on_a_Choose_applies_as_MSBuild_does()
    {
        var site = Project(
            "Site",
            NotPackable
            + $"<Choose><When Condition=\"{NeverTrue}\"><ItemGroup><NuGetAuditSuppress Include=\"{AdvisoryA}\" /></ItemGroup></When></Choose>");

        var (exit, output) = Audit([Source], Entry(site, Finding("Some.Package", AdvisoryA)));

        Assert.Equal(1, exit);
        Assert.Contains(Vulnerable("Site", AdvisoryA), output);
    }

    [Fact]
    public void An_item_inside_a_Target_is_no_suppression_as_MSBuild_does()
    {
        var site = Project(
            "Site",
            NotPackable
            + $"<Target Name=\"Never\"><ItemGroup><NuGetAuditSuppress Include=\"{AdvisoryA}\" /></ItemGroup></Target>");

        var (exit, output) = Audit([Source], Entry(site, Finding("Some.Package", AdvisoryA)));

        Assert.Equal(1, exit);
        Assert.Contains(Vulnerable("Site", AdvisoryA), output);
    }

    [Fact]
    public void An_import_that_makes_the_project_packable_counts_as_MSBuild_does()
    {
        // Directory.Build.targets is imported AFTER the project body, so it overrides the
        // project's own IsPackable — invisible to anything reading only the project file.
        File.WriteAllText(
            Path.Combine(_directory, "Directory.Build.targets"),
            "<Project><PropertyGroup><IsPackable>true</IsPackable></PropertyGroup></Project>");
        var site = Project("Site", NotPackable + Suppress(AdvisoryA));

        var (exit, output) = Audit([Source], Entry(site, Finding("Some.Package", AdvisoryA)));

        Assert.Equal(1, exit);
        Assert.Contains($"Site is packable and suppresses {AdvisoryA}", output);
    }

    // --- QA round 2: asked MSBuild, but not under the conditions that decide ------------------

    [Fact]
    public void A_project_non_packable_only_in_Debug_counts_as_packable_because_Release_ships()
    {
        // publish.yml packs Release. Evaluated only as Debug (MSBuild's default), this project
        // looked non-packable and was let accept an advisory it would then have shipped.
        var library = Project(
            "Library",
            "<PropertyGroup Condition=\"'$(Configuration)' == 'Debug'\"><IsPackable>false</IsPackable></PropertyGroup>"
            + Suppress(AdvisoryA));

        var (exit, output) = Audit([Source], Entry(library, Finding("Some.Package", AdvisoryA)));

        Assert.Equal(1, exit);
        Assert.Contains($"Library is packable and suppresses {AdvisoryA}", output);
        Assert.Contains(Vulnerable("Library", AdvisoryA), output);
    }

    [Fact]
    public void A_suppression_in_only_one_configuration_accepts_nothing()
    {
        var site = Project(
            "Site",
            NotPackable
            + $"<ItemGroup Condition=\"'$(Configuration)' == 'Debug'\"><NuGetAuditSuppress Include=\"{AdvisoryA}\" /></ItemGroup>");

        var (exit, output) = Audit([Source], Entry(site, Finding("Some.Package", AdvisoryA)));

        Assert.Equal(1, exit);
        Assert.Contains(Vulnerable("Site", AdvisoryA), output);
        Assert.Contains("0 finding(s) accepted", output);
    }

    [Fact]
    public void A_suppression_in_only_one_configuration_fails_even_with_nothing_to_match()
    {
        // QA round 3: the round-2 fix accepted only suppressions present in every configuration,
        // and checked staleness only over those — so a Debug-only suppression with no finding was
        // judged by nothing. It silenced Debug's build warning while the audit stayed green. The
        // one-configuration test above has a finding, which is why it could not see this.
        var site = Project(
            "Site",
            NotPackable
            + $"<ItemGroup Condition=\"'$(Configuration)' == 'Debug'\"><NuGetAuditSuppress Include=\"{AdvisoryA}\" /></ItemGroup>");

        var (exit, output) = Audit([Source], Entry(site));

        Assert.Equal(1, exit);
        Assert.Contains($"Site suppresses {AdvisoryA} (declared in Debug only)", output);
    }

    [Fact]
    public void A_project_that_fails_to_evaluate_only_as_Release_fails_and_accepts_nothing()
    {
        // The Debug evaluation succeeds; Release imports a file that does not exist.
        var site = Project(
            "Site",
            NotPackable + Suppress(AdvisoryA)
            + "<Import Project=\"missing.props\" Condition=\"'$(Configuration)' == 'Release'\" />");

        var (exit, output) = Audit([Source], Entry(site, Finding("Some.Package", AdvisoryA)));

        Assert.Equal(1, exit);
        Assert.Contains("MSBuild could not evaluate it as Release", output);
        Assert.Contains(Vulnerable("Site", AdvisoryA), output);
    }

    [Fact]
    public void A_multi_targeted_project_may_not_suppress()
    {
        // NuGet reads suppressions per framework, from inner evaluations; the outer one this
        // script reads can disagree in both directions (QA round 2, measured with a real restore).
        var site = Project(
            "Site",
            "<PropertyGroup><TargetFrameworks>net10.0;net9.0</TargetFrameworks></PropertyGroup>"
            + NotPackable + Suppress(AdvisoryA));

        var (exit, output) = Audit([Source], Entry(site, Finding("Some.Package", AdvisoryA)));

        Assert.Equal(1, exit);
        Assert.Contains($"Site targets more than one framework and suppresses {AdvisoryA}", output);
        Assert.Contains(Vulnerable("Site", AdvisoryA), output);
    }

    // --- Findings from every configuration (tidy-2026-10) -----------------------------------

    [Fact]
    public void A_finding_only_the_Release_graph_holds_fails_naming_the_configuration()
    {
        // The case the audit used to be blind to: it restored, and `dotnet list` read, Debug only,
        // and Release is what ships.
        var site = Project("Site", NotPackable);

        var (exit, output) = AuditPerConfiguration(
            debug: [Entry(site)],
            release: [Entry(site, Finding("Some.Package", AdvisoryA))]);

        Assert.Equal(1, exit);
        Assert.Contains(Vulnerable("Site", AdvisoryA) + " (in Release only)", output);
    }

    [Fact]
    public void A_finding_only_the_Debug_graph_holds_still_fails()
    {
        var site = Project("Site", NotPackable);

        var (exit, output) = AuditPerConfiguration(
            debug: [Entry(site, Finding("Some.Package", AdvisoryA))],
            release: [Entry(site)]);

        Assert.Equal(1, exit);
        Assert.Contains(Vulnerable("Site", AdvisoryA) + " (in Debug only)", output);
    }

    [Fact]
    public void A_finding_every_configuration_holds_is_reported_once_without_a_qualifier()
    {
        var site = Project("Site", NotPackable);

        var (exit, output) = AuditPerConfiguration(
            debug: [Entry(site, Finding("Some.Package", AdvisoryA))],
            release: [Entry(site, Finding("Some.Package", AdvisoryA))]);

        Assert.Equal(1, exit);
        Assert.Equal(1, Occurrences(output, Vulnerable("Site", AdvisoryA)));
        Assert.DoesNotContain(" only)", output);
    }

    [Fact]
    public void A_suppression_matched_only_in_Release_is_not_stale()
    {
        // The suppression is judged against the union of the graphs: an advisory the shipped
        // configuration still holds is a live reason, wherever else it is absent.
        var site = Project("Site", NotPackable + Suppress(AdvisoryA));

        var (exit, output) = AuditPerConfiguration(
            debug: [Entry(site)],
            release: [Entry(site, Finding("Some.Package", AdvisoryA))]);

        Assert.Equal(0, exit);
        Assert.Contains($"::notice title=Accepted advisory::Site (net10.0): Some.Package 1.0.0 - High - {AdvisoryA} (in Release only)", output);
        Assert.DoesNotContain("stale", output);
    }

    [Fact]
    public void A_project_only_the_Release_report_names_is_audited()
    {
        var site = Project("Site", NotPackable);
        var tool = Project("Tool", NotPackable);

        var (exit, output) = AuditPerConfiguration(
            debug: [Entry(site)],
            release: [Entry(site), Entry(tool, Finding("Some.Package", AdvisoryB))]);

        Assert.Equal(1, exit);
        Assert.Contains(Vulnerable("Tool", AdvisoryB) + " (in Release only)", output);
        Assert.Contains("audited 2 project(s)", output);
    }

    [Fact]
    public void A_Release_report_that_consulted_no_vulnerability_source_fails()
    {
        // Non-vacuity holds per configuration: a Release graph checked against nothing is not
        // rescued by a Debug graph checked properly.
        var site = Project("Site", NotPackable);

        var (exit, output) = AuditPerConfiguration(
            debug: [Entry(site)],
            release: [Entry(site)],
            releaseSources: ["C:/local/feed/"]);

        Assert.Equal(1, exit);
        Assert.Contains("The Release report for Fixture.slnx consulted no source", output);
    }

    /// <summary>
    /// The real CLI, not a fixture: a package referenced only under Release is found.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The fixtures above cannot catch the defect this exists for, and did not.</b> They hand
    /// the script a report per configuration, so they prove what it does WITH two graphs and
    /// nothing about how it GETS them. Its first version restored as Release and then ran
    /// <c>dotnet list</c> — which restores again, implicitly, as Debug, so the Release pass listed
    /// the Debug graph. Six fixture tests and a mutation stayed green while a Release-only advisory
    /// went unseen (QA round 1, reproduced against the real CLI).
    /// </para>
    /// <para>
    /// <b>It needs nuget.org</b> — for the package, and for the advisory data the audit reads —
    /// as every restore in this repository already does. <c>System.Text.Json 8.0.0</c> carries
    /// published advisories (GHSA-8g4q-xg66-9fp4, GHSA-hh2w-p6rv-4g7w); an advisory is not
    /// withdrawn once published, so the expectation is stable. The project is written outside
    /// the repository, so nothing of this repository's build is imported into it.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_package_only_the_Release_configuration_references_is_found_by_the_real_listing()
    {
        File.WriteAllText(
            Path.Combine(_directory, "nuget.config"),
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><configuration><packageSources><clear />"
            + $"<add key=\"nuget.org\" value=\"{Source}\" /></packageSources></configuration>");

        var probe = Project(
            "Probe",
            "<PropertyGroup><NuGetAudit>false</NuGetAudit></PropertyGroup>"
            + "<ItemGroup Condition=\"'$(Configuration)' == 'Release'\">"
            + "<PackageReference Include=\"System.Text.Json\" Version=\"8.0.0\" /></ItemGroup>");

        var (exit, output) = Run(probe);

        Assert.Equal(1, exit);
        Assert.Contains("(Debug, Release)", output);
        Assert.Contains(
            "Vulnerable package: Probe (net10.0): System.Text.Json 8.0.0 - High - https://github.com/advisories/GHSA-8g4q-xg66-9fp4 (in Release only)",
            output);
    }

    private static int Occurrences(string haystack, string needle)
    {
        var count = 0;
        for (var at = haystack.IndexOf(needle, StringComparison.Ordinal); at >= 0; at = haystack.IndexOf(needle, at + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    // --- Fixtures ----------------------------------------------------------------------------

    private static string Suppress(string advisory) =>
        $"<ItemGroup><NuGetAuditSuppress Include=\"{advisory}\" /></ItemGroup>";

    private static string Vulnerable(string project, string advisory) =>
        $"Vulnerable package: {project} (net10.0): Some.Package 1.0.0 - High - {advisory}";

    private string Project(string name, string body)
    {
        var path = Path.Combine(_directory, name + ".csproj");
        File.WriteAllText(
            path,
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>"
            + body + "</Project>");

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

    /// <summary>One report standing for every configuration: the graphs are identical.</summary>
    private (int Exit, string Output) Audit(string[] sources, params object[] projects)
    {
        var report = Path.Combine(_directory, "report.json");
        File.WriteAllText(report, JsonSerializer.Serialize(new { version = 1, sources, projects }));

        return Run("Fixture.slnx", "-Report", report);
    }

    /// <summary>A Debug report and a Release report, so the two graphs can differ.</summary>
    private (int Exit, string Output) AuditPerConfiguration(
        object[] debug, object[] release, string[]? releaseSources = null)
    {
        var debugReport = Path.Combine(_directory, "debug.json");
        var releaseReport = Path.Combine(_directory, "release.json");
        File.WriteAllText(debugReport, JsonSerializer.Serialize(new { version = 1, sources = new[] { Source }, projects = debug }));
        File.WriteAllText(releaseReport, JsonSerializer.Serialize(new { version = 1, sources = releaseSources ?? [Source], projects = release }));

        return Run("Fixture.slnx", "-Report", debugReport, "-ReleaseReport", releaseReport);
    }

    private static (int Exit, string Output) Run(string target, params string[] arguments)
    {
        var info = new ProcessStartInfo("pwsh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File", Script, "-Target", target }.Concat(arguments))
        {
            info.ArgumentList.Add(argument);
        }

        // The script runs `dotnet msbuild` per project; without this its worker nodes outlive it
        // and hold the redirected pipes open (see PackedSolution.Run for the cost of learning that).
        info.Environment["MSBUILDDISABLENODEREUSE"] = "1";

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("Could not start pwsh.");

        // Both pipes drained concurrently, for the reason PackedSolution.Run gives.
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(milliseconds: 120_000))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException("The audit script did not finish within two minutes.");
        }

        Task.WaitAll([stdout, stderr], TimeSpan.FromSeconds(10));
        return (process.ExitCode, stdout.Result + stderr.Result);
    }
}
