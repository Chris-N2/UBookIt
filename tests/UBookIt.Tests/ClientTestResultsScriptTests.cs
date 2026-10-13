using System.Diagnostics;
using System.Text.Json;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// <c>scripts/ci/Assert-ClientTestResults.ps1</c>, run for real through <c>pwsh</c> against fixture
/// vitest reports (continuous-integration spec, "A run proves every suite ran" — "A skipped test
/// fails the run" says ANY test, and vitest exits 0 with one).
/// </summary>
/// <remarks>
/// Every test asserts the exit code AND that the output names what it should, so that a test
/// cannot pass because the script failed for some other reason. The fixtures carry only the
/// members the script reads, in the shape vitest's JSON reporter writes them; the shape itself is
/// pinned against a real report by the change that introduced this (tidy-2026-10, task 5.1), not
/// here, because producing one needs node.
/// </remarks>
public sealed class ClientTestResultsScriptTests : IDisposable
{
    private static readonly string Script =
        Path.Combine(RepoFiles.Root, "scripts", "ci", "Assert-ClientTestResults.ps1");

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "ubookit-client-results-" + Guid.NewGuid().ToString("N"));

    public ClientTestResultsScriptTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void A_report_where_every_test_ran_passes()
    {
        var (exit, output) = Check(Report(Test("a", "passed"), Test("b", "passed")));

        Assert.Equal(0, exit);
        Assert.Contains("All 2 client test(s) ran.", output);
    }

    [Fact]
    public void A_failed_test_is_left_to_the_test_step()
    {
        // npm test has already failed the run for it; counting it twice would only add noise.
        var (exit, output) = Check(Report(Test("a", "passed"), Test("b", "failed")));

        Assert.Equal(0, exit);
        Assert.Contains("All 2 client test(s) ran.", output);
    }

    [Theory]
    [InlineData("skipped")]
    [InlineData("todo")]
    [InlineData("pending")]
    public void A_test_that_did_not_run_fails_naming_it_and_its_status(string status)
    {
        var (exit, output) = Check(Report(Test("ran", "passed"), Test("did not", status)));

        Assert.Equal(1, exit);
        Assert.Contains($"::error title=Client test not run::fixture.test.ts > suite did not [{status}]", output);
        Assert.Contains("1 of 2 client test(s) did not run", output);
    }

    [Fact]
    public void A_missing_report_fails()
    {
        var (exit, output) = Run(Path.Combine(_directory, "absent.json"));

        Assert.Equal(1, exit);
        Assert.Contains("No vitest report", output);
    }

    [Fact]
    public void A_report_that_ran_nothing_fails()
    {
        var (exit, output) = Check(new { numTotalTests = 0, testResults = Array.Empty<object>() });

        Assert.Equal(1, exit);
        Assert.Contains("ran zero tests", output);
    }

    [Theory]
    [InlineData("numTotalTests")]
    [InlineData("testResults")]
    public void A_report_in_a_shape_it_does_not_know_fails_closed(string missing)
    {
        var report = new Dictionary<string, object>
        {
            ["numTotalTests"] = 1,
            ["testResults"] = new[] { File(Test("a", "passed")) },
        };
        report.Remove(missing);

        var (exit, output) = Check(report);

        Assert.Equal(1, exit);
        Assert.Contains($"has no '{missing}'", output);
    }

    [Fact]
    public void A_listing_shorter_than_the_count_fails()
    {
        // The total says three ran and only two are listed: the loop judged less than ran.
        var (exit, output) = Check(new { numTotalTests = 3, testResults = new[] { File(Test("a", "passed"), Test("b", "passed")) } });

        Assert.Equal(1, exit);
        Assert.Contains("counts 3 test(s) but lists 2", output);
    }

    // --- Fixtures ----------------------------------------------------------------------------

    private static object Test(string title, string status) =>
        new { title, fullName = "suite " + title, status };

    private static object File(params object[] tests) =>
        new { name = "/repo/src/section/fixture.test.ts", assertionResults = tests };

    private static object Report(params object[] tests) =>
        new { numTotalTests = tests.Length, testResults = new[] { File(tests) } };

    private (int Exit, string Output) Check(object report)
    {
        var path = Path.Combine(_directory, "vitest.json");
        System.IO.File.WriteAllText(path, JsonSerializer.Serialize(report));
        return Run(path);
    }

    private (int Exit, string Output) Run(string report)
    {
        var info = new ProcessStartInfo("pwsh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File", Script, "-Report", report })
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
            throw new InvalidOperationException("The script did not finish within a minute.");
        }

        Task.WaitAll([stdout, stderr], TimeSpan.FromSeconds(10));
        return (process.ExitCode, stdout.Result + stderr.Result);
    }
}
