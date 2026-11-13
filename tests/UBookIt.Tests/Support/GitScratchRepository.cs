using System.Diagnostics;

namespace UBookIt.Tests.Support;

/// <summary>
/// A throwaway git repository for running the real CI scripts against: commits made from whole
/// file sets, and remote-tracking refs (<c>refs/remotes/origin/&lt;line&gt;</c>) set directly, so
/// a test can say "this is what each published line's tip holds" without a remote.
/// </summary>
/// <remarks>
/// Every git call passes its own identity and disables line-ending conversion and signing, so a
/// developer's global configuration cannot change what a test sees.
/// </remarks>
public sealed class GitScratchRepository : IDisposable
{
    public GitScratchRepository()
    {
        Root = Path.Combine(Path.GetTempPath(), "ubookit-git-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Git("init", "-q", "-b", "scratch");
    }

    public string Root { get; }

    /// <summary>
    /// Commits exactly <paramref name="files"/> (path → content) as the whole tree, on top of the
    /// current HEAD, and returns the new commit's SHA.
    /// </summary>
    public string Commit(IReadOnlyDictionary<string, string> files)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(Root))
        {
            if (Path.GetFileName(entry) == ".git")
            {
                continue;
            }

            if (Directory.Exists(entry))
            {
                Directory.Delete(entry, recursive: true);
            }
            else
            {
                File.Delete(entry);
            }
        }

        foreach (var (path, content) in files)
        {
            var full = Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }

        Git("add", "-A");
        Git("commit", "-q", "--allow-empty", "-m", "fixture");
        return Git("rev-parse", "HEAD").Trim();
    }

    /// <summary>Moves HEAD (detached) to <paramref name="sha"/>, so the next commit forks from it.</summary>
    public void Checkout(string sha) => Git("checkout", "-q", "--detach", sha);

    /// <summary>Points <c>refs/remotes/origin/&lt;line&gt;</c> at <paramref name="sha"/>.</summary>
    public void SetRemoteLine(string line, string sha) =>
        Git("update-ref", $"refs/remotes/origin/{line}", sha);

    public void Tag(string name, string sha) => Git("tag", name, sha);

    public string Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[]
                 {
                     "-c", "user.name=fixture", "-c", "user.email=fixture@example.invalid",
                     "-c", "core.autocrlf=false", "-c", "commit.gpgsign=false", "-c", "tag.gpgsign=false",
                 }.Concat(arguments))
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start git.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(milliseconds: 60_000))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} did not finish within a minute.");
        }

        Task.WaitAll([stdout, stderr], TimeSpan.FromSeconds(10));
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {stderr.Result}");
        }

        return stdout.Result;
    }

    public void Dispose()
    {
        // git marks object files read-only; clear that so the directory can be deleted.
        foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(Root, recursive: true);
    }
}
