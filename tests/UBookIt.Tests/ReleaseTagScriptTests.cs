using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// <c>scripts/ci/Assert-ReleaseTag.ps1</c>, run for real through <c>pwsh</c> against a throwaway
/// git repository standing for the release checkout.
/// </summary>
/// <remarks>
/// <para>
/// Steps 1–3 (shape, declared version, line) are local, and are what these tests exercise. Step 4
/// asks the GitHub API whether ci succeeded. Here the API base is a closed loopback port and the
/// wait is zero minutes, so a case that passes steps 1–3 fails at once at step 4, on the API, and
/// never reaches the network outside the machine. That failure is itself the proof that steps 1–3
/// passed: the script's output names the line the commit is contained in before it gets there.
/// </para>
/// <para>
/// The history: a root commit, and each published line's tip forked from it, declaring its own
/// major (main 17.7.0, dev/v18 18.6.0, dev/v19 19.0.0-rc1). Main's history also passes through a
/// commit that declared 19.0.0-rc2, so there is a 19 commit that main contains and dev/v19 does not.
/// </para>
/// </remarks>
public sealed class ReleaseTagScriptTests : IDisposable
{
    private static readonly string Script =
        Path.Combine(RepoFiles.Root, "scripts", "ci", "Assert-ReleaseTag.ps1");

    private const string ClosedLoopbackPort = "http://127.0.0.1:1";

    private readonly GitScratchRepository _repo = new();
    private readonly string _listDirectory =
        Path.Combine(Path.GetTempPath(), "ubookit-lines-" + Guid.NewGuid().ToString("N"));

    private readonly string _v19Tip;
    private readonly string _nineteenOnlyInMain;

    public ReleaseTagScriptTests()
    {
        Directory.CreateDirectory(_listDirectory);

        var root = _repo.Commit(Declaring("17.6.0"));

        _nineteenOnlyInMain = _repo.Commit(Declaring("19.0.0-rc2"));
        _repo.SetRemoteLine("main", _repo.Commit(Declaring("17.7.0")));

        _repo.Checkout(root);
        _repo.SetRemoteLine("dev/v18", _repo.Commit(Declaring("18.6.0")));

        _repo.Checkout(root);
        _v19Tip = _repo.Commit(Declaring("19.0.0-rc1"));
        _repo.SetRemoteLine("dev/v19", _v19Tip);
    }

    public void Dispose()
    {
        _repo.Dispose();
        Directory.Delete(_listDirectory, recursive: true);
    }

    [Theory]
    [InlineData("v17.2.2")]
    [InlineData("17.2")]
    [InlineData("17.2.2-")]
    [InlineData("17.2.2-rc..1")]
    [InlineData("17.2.2-rc1.")]
    [InlineData("17.2.2+build.5")]
    [InlineData("17.2.2-rc1+build.5")]
    [InlineData("17.2.2-rc_1")]
    public void A_tag_that_is_not_a_release_tag_is_refused_on_its_shape(string tag)
    {
        var (exit, output) = Run(tag);

        Assert.Equal(1, exit);
        Assert.Contains("Release tag - shape", output);
    }

    [Fact]
    public void A_release_tag_on_a_prerelease_commit_is_refused()
    {
        _repo.Tag("19.0.0", _v19Tip);

        var (exit, output) = Run("19.0.0");

        Assert.Equal(1, exit);
        Assert.Contains("Release tag - version", output);
        Assert.Contains("declares <Version>19.0.0-rc1</Version>", output);
    }

    [Fact]
    public void A_prerelease_tag_on_a_release_commit_is_refused()
    {
        _repo.Checkout(_v19Tip);
        var release = _repo.Commit(Declaring("19.0.0"));
        _repo.SetRemoteLine("dev/v19", release);
        _repo.Tag("19.0.0-rc1", release);

        var (exit, output) = Run("19.0.0-rc1");

        Assert.Equal(1, exit);
        Assert.Contains("Release tag - version", output);
        Assert.Contains("declares <Version>19.0.0</Version>", output);
    }

    [Fact]
    public void A_prerelease_tag_is_placed_on_its_line_by_its_major_and_reaches_the_verification_step()
    {
        _repo.Tag("19.0.0-rc1", _v19Tip);

        var (exit, output) = Run("19.0.0-rc1");

        // Steps 1-3 passed: the version matched, and the line was found and contains the commit.
        Assert.Contains("matches.", output);
        Assert.Contains($"{_v19Tip} is contained in origin/dev/v19.", output);
        Assert.DoesNotContain("Release tag - shape", output);
        Assert.DoesNotContain("Release tag - version", output);
        Assert.DoesNotContain("Release tag - line", output);

        // Step 4 then failed on the closed loopback API, as it must here.
        Assert.Equal(1, exit);
        Assert.Contains("Release tag - verified", output);
        Assert.Contains("could not be read from the GitHub API", output);
    }

    [Fact]
    public void A_tag_on_a_commit_its_line_does_not_contain_is_refused()
    {
        _repo.Tag("19.0.0-rc2", _nineteenOnlyInMain);

        var (exit, output) = Run("19.0.0-rc2");

        Assert.Equal(1, exit);
        Assert.Contains("Release tag - line", output);
        Assert.Contains("must be on dev/v19", output);
    }

    [Fact]
    public void A_major_no_published_line_has_is_refused()
    {
        _repo.Checkout(_v19Tip);
        var twenty = _repo.Commit(Declaring("20.0.0"));
        _repo.Tag("20.0.0", twenty);

        var (exit, output) = Run("20.0.0");

        Assert.Equal(1, exit);
        Assert.Contains("No published line (main, dev/v18, dev/v19) declares major 20", output);
    }

    [Fact]
    public void Two_lines_declaring_the_same_major_are_ambiguous()
    {
        _repo.Checkout(_v19Tip);
        _repo.SetRemoteLine("dev/v20", _repo.Commit(Declaring("19.0.1")));
        _repo.Tag("19.0.0-rc1", _v19Tip);

        var (exit, output) = Run("19.0.0-rc1", """["main", "dev/v18", "dev/v19", "dev/v20"]""");

        Assert.Equal(1, exit);
        Assert.Contains("More than one published line declares major 19 (dev/v19, dev/v20)", output);
    }

    // ---- fixture ----

    private static Dictionary<string, string> Declaring(string version) => new()
    {
        ["Directory.Build.props"] = $"""
            <Project>
              <PropertyGroup>
                <Version>{version}</Version>
              </PropertyGroup>
            </Project>
            """,
    };

    private (int Exit, string Output) Run(string tag, string? list = null)
    {
        var listFile = Path.Combine(_listDirectory, "published-lines.json");
        File.WriteAllText(listFile, list ?? """["main", "dev/v18", "dev/v19"]""");

        return PwshScript.Run(
            Script,
            _repo.Root,
            "-Tag", tag,
            "-ReleaseRoot", _repo.Root,
            "-Repository", "fixture/fixture",
            "-LinesFile", listFile,
            "-ApiBaseUri", ClosedLoopbackPort,
            "-WaitMinutes", "0",
            "-PollSeconds", "0");
    }
}
