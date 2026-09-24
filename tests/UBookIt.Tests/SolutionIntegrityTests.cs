using System.Xml.Linq;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// That the solution still builds everything it contains.
/// <para>
/// This project is not the subject of these tests — it is the <b>host</b> for them,
/// deliberately. A test project excluded from the build cannot report its own
/// absence: it does not run, so its every assertion is silent, and the suite it
/// belongs to reports nothing rather than reporting failure. The check has to live
/// somewhere that still runs.
/// </para>
/// <para>
/// Not hypothetical. <c>0e79a03</c>, a Visual Studio 2026 <c>.slnx</c> conversion,
/// added <c>&lt;Build Solution="Debug|*" Project="false" /&gt;</c> to
/// <c>UBookIt.Tests.Rendering</c>. Four hundred and fifteen tests stopped running,
/// and stayed stopped through an archive and a merge to <c>main</c>, because a
/// solution-level <c>dotnet test</c> reported success the whole time — it had simply
/// stopped being asked. It was found by a human reading a test count, which is not
/// a control.
/// </para>
/// <para>
/// CI now runs this on every push, and it still belongs here rather than in the
/// pipeline: a solution-level <c>dotnet test</c> in CI is blind in exactly the same
/// way a local one is, because an excluded project is never asked. CI's own results
/// check (<c>scripts/ci/Assert-TestResults.ps1</c>) also catches a project that stops
/// reporting; this catches the cause, in the one file that produces it, on a
/// maintainer's machine before anything is pushed.
/// </para>
/// </summary>
public class SolutionIntegrityTests
{
    [Fact]
    public void No_project_is_excluded_from_the_solution_build()
    {
        var solution = XDocument.Parse(RepoFiles.Read("UBookIt.slnx"));

        var projects = solution.Descendants("Project").ToList();

        // Non-vacuity first: a scan that stopped finding projects would report no
        // exclusions for the same reason it would report nothing at all.
        Assert.True(
            projects.Count >= 8,
            $"only {projects.Count} project(s) found in UBookIt.slnx — the scan has "
            + "stopped seeing the solution, so its 'no exclusions' answer means nothing.");

        var excluded = projects
            .Where(project => project
                .Elements("Build")
                .Any(build => string.Equals(
                    (string?)build.Attribute("Project"), "false", StringComparison.OrdinalIgnoreCase)))
            .Select(project => (string?)project.Attribute("Path") ?? "(unnamed)")
            .ToList();

        Assert.True(
            excluded.Count == 0,
            "excluded from the solution build, so nothing it contains runs: "
            + string.Join(", ", excluded)
            + ". A test project in this state reports success by silence.");
    }

    [Fact]
    public void Every_test_project_on_disk_is_in_the_solution()
    {
        // The other way a suite goes quiet: never being added at all. Same
        // consequence — tests that exist, pass locally, and are asked nothing by a
        // solution-level run.
        var solution = RepoFiles.Read("UBookIt.slnx");

        var onDisk = Directory
            .EnumerateFiles(
                Path.Combine(RepoFiles.Root, "tests"), "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(onDisk);

        foreach (var project in onDisk)
        {
            Assert.True(
                solution.Contains(project!, StringComparison.Ordinal),
                $"{project} exists on disk and is not in UBookIt.slnx, so a "
                + "solution-level test run never asks it anything.");
        }
    }
}
