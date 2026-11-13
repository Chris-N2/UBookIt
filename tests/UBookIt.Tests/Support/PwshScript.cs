using System.Diagnostics;

namespace UBookIt.Tests.Support;

/// <summary>Runs a repository script for real through <c>pwsh</c>, as CI does.</summary>
public static class PwshScript
{
    /// <summary>
    /// Runs <paramref name="script"/> with <paramref name="arguments"/>, in
    /// <paramref name="workingDirectory"/>, and returns its exit code and its combined output.
    /// </summary>
    public static (int Exit, string Output) Run(
        string script, string workingDirectory, params string[] arguments)
    {
        var info = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File", script }.Concat(arguments))
        {
            info.ArgumentList.Add(argument);
        }

        // The scripts read GitHub's environment when they find it. A test never hands them the
        // caller's token or output file, and loopback is never sent through a proxy, so a script
        // pointed at a closed local port cannot reach anything else (QA round 1, NIT-8).
        foreach (var variable in new[] { "GITHUB_TOKEN", "GH_TOKEN", "GITHUB_OUTPUT", "GITHUB_ENV" })
        {
            info.Environment.Remove(variable);
        }
        info.Environment["NO_PROXY"] = "127.0.0.1,localhost";
        info.Environment["no_proxy"] = "127.0.0.1,localhost";

        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start pwsh.");

        // Both pipes drained concurrently, so neither can fill and block the script.
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(milliseconds: 120_000))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException($"{Path.GetFileName(script)} did not finish within two minutes.");
        }

        Task.WaitAll([stdout, stderr], TimeSpan.FromSeconds(10));
        return (process.ExitCode, stdout.Result + stderr.Result);
    }
}
