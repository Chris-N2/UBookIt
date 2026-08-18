using System.Runtime.CompilerServices;

namespace UBookIt.Tests.Support;

/// <summary>
/// Reads shipped source files so a test can assert a property the compiled
/// artefact cannot carry.
/// <para>
/// Used sparingly and only where the property genuinely lives in the source: that
/// no code path <em>exists</em> which could pin a resource, and that no GET form
/// carries a contact field. Both are absences, and an absence has no runtime
/// representation to assert against — a test that exercised the shipped form
/// would prove only that this form does not leak, never that none can.
/// </para>
/// <para>
/// Located from this file's own compile-time path rather than from the output
/// directory, so it does not depend on how or where the tests were built.
/// </para>
/// </summary>
public static class RepoFiles
{
    /// <summary>The repository root — three directories above tests/UBookIt.Tests/Support.</summary>
    public static string Root { get; } = ResolveRoot();

    /// <summary>Reads a file by repo-relative path, failing the test if it has moved.</summary>
    public static string Read(string relativePath)
    {
        var full = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(full), $"Expected source file not found: {relativePath}");

        return File.ReadAllText(full);
    }

    /// <summary>Every file under a repo-relative directory matching a pattern, recursively.</summary>
    public static IReadOnlyList<string> Paths(string relativeDirectory, string pattern)
    {
        var full = Path.Combine(Root, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(Directory.Exists(full), $"Expected source directory not found: {relativeDirectory}");

        var files = Directory
            .GetFiles(full, pattern, SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        // A scan over nothing passes every assertion made about it. The guard is
        // what keeps a moved directory from reading as a clean bill of health.
        Assert.NotEmpty(files);

        return files;
    }

    private static string ResolveRoot([CallerFilePath] string thisFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
