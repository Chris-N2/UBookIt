using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;

namespace UBookIt.Tests.Support;

/// <summary>
/// Packs the solution once and exposes the packages that came out.
/// <para>
/// This runs a real <c>dotnet pack</c> rather than reading project files, and that is
/// the whole point of it. Every defect these tests exist to prevent — a dependency on
/// a package nobody publishes, an assembly left out of the aggregate, a backoffice
/// bundle that was never built, a manifest still saying version zero — was present in
/// a build that reported complete success. <b>Inspection is what missed them.</b> A
/// guard derived from the same project files that produced the defect would have
/// agreed with it.
/// </para>
/// <para>
/// It is not, and must not be described as, a substitute for installing the package
/// into a site. It proves what is in the box; only an installation proves the box is
/// enough.
/// </para>
/// <para>
/// One pack per test run, shared through a collection fixture. It costs a minute or so
/// on a cold build and very little afterwards, which is the price of asserting against
/// the artefact instead of against an opinion about it.
/// </para>
/// </summary>
public sealed class PackedSolution : IDisposable
{
    private PackedSolution(string outputDirectory, IReadOnlyList<PackedPackage> packages)
    {
        OutputDirectory = outputDirectory;
        Packages = packages;
    }

    public string OutputDirectory { get; }

    /// <summary>Every package the solution produced — which is exactly the set of packable projects.</summary>
    public IReadOnlyList<PackedPackage> Packages { get; }

    public PackedPackage this[string id] =>
        Packages.SingleOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException(
            $"No package '{id}' was produced. Produced: {string.Join(", ", Packages.Select(p => p.Id))}");

    public static PackedSolution Pack()
    {
        var output = Path.Combine(Path.GetTempPath(), "ubookit-pack-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(output);

        // Pack a checkout, not a working copy.
        //
        // The backoffice client output is gitignored, so on a clean clone it does not
        // exist — and for a long time nothing built it, which meant `dotnet pack` produced
        // a backoffice package with no client in it and reported success. That was not
        // noticed for the most ordinary reason: every working copy anyone would have
        // checked had already been made complete by a manual `npm run build`.
        //
        // Deleting it here is what makes "the client is in the package" an assertion about
        // the BUILD rather than about the developer's disk. Nothing is lost: the build
        // regenerates it during the pack below, and if it cannot, that is the defect this
        // is looking for and the failure is the right outcome.
        DeleteGeneratedClientOutput();

        Run("dotnet", $"pack \"{Path.Combine(RepoFiles.Root, "UBookIt.slnx")}\" -c Release -o \"{output}\"");

        var packages = Directory
            .GetFiles(output, "*.nupkg")
            // Symbol packages are a different artefact with the same extension family;
            // asserting metadata about them would be asserting about the wrong thing.
            .Where(path => !path.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase))
            .Select(PackedPackage.Open)
            .OrderBy(p => p.Id, StringComparer.Ordinal)
            .ToList();

        // A pack that produced nothing would satisfy every "for each package" assertion
        // below without exercising one of them.
        if (packages.Count == 0)
        {
            throw new InvalidOperationException($"dotnet pack produced no packages in {output}.");
        }

        return new PackedSolution(output, packages);
    }

    private static void DeleteGeneratedClientOutput()
    {
        var backoffice = Path.Combine(RepoFiles.Root, "src", "UBookIt.Backoffice");

        // A checkout has no client output AND no intermediate output. Deleting only the
        // first produces a state no clone is ever in, and it does not behave like one:
        // static web asset discovery caches its file list in obj/, so a wwwroot emptied
        // behind that cache's back yields a package with no client and no error. Measured,
        // not assumed — the first version of this helper did exactly that and produced a
        // failure that looked like the defect but was an artefact of the helper.
        //
        // Release only. The test assembly runs from bin/Debug and has UBookIt.Backoffice.dll
        // loaded; the pack below is Release, so this reproduces the clean state that matters
        // without deleting a file out from under the running process.
        foreach (var directory in new[]
                 {
                     Path.Combine(backoffice, "wwwroot", "App_Plugins"),
                     Path.Combine(backoffice, "obj", "Release"),
                     Path.Combine(backoffice, "bin", "Release"),
                 })
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void Run(string fileName, string arguments)
    {
        var info = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = RepoFiles.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException($"Could not start '{fileName} {arguments}'.");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();

        if (!process.WaitForExit(milliseconds: 10 * 60 * 1000))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException($"'{fileName} {arguments}' did not finish within ten minutes.");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'{fileName} {arguments}' failed with exit code {process.ExitCode}.{Environment.NewLine}{stdout}{Environment.NewLine}{stderr}");
        }
    }

    public void Dispose()
    {
        foreach (var package in Packages)
        {
            package.Dispose();
        }

        try
        {
            Directory.Delete(OutputDirectory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a test run over.
        }
    }
}

/// <summary>One produced <c>.nupkg</c>: its nuspec metadata and the paths inside it.</summary>
public sealed class PackedPackage : IDisposable
{
    private readonly ZipArchive _archive;
    private readonly XElement _metadata;

    private PackedPackage(ZipArchive archive, XElement metadata, IReadOnlyList<string> entries)
    {
        _archive = archive;
        _metadata = metadata;
        Entries = entries;
    }

    /// <summary>Every path inside the package, using forward slashes.</summary>
    public IReadOnlyList<string> Entries { get; }

    public string Id => Value("id");

    public string Version => Value("version");

    /// <summary>Reads a nuspec metadata element, or the empty string when it is absent.</summary>
    public string Value(string name) =>
        _metadata.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value.Trim() ?? string.Empty;

    /// <summary>Every dependency id/version pair across all target framework groups.</summary>
    public IReadOnlyList<(string Id, string Version)> Dependencies =>
        _metadata
            .Elements().Where(e => e.Name.LocalName == "dependencies")
            .Descendants().Where(e => e.Name.LocalName == "dependency")
            .Select(e => (Id: e.Attribute("id")?.Value ?? string.Empty, Version: e.Attribute("version")?.Value ?? string.Empty))
            .ToList();

    /// <summary>Reads a file out of the package as text.</summary>
    public string ReadText(string entryPath)
    {
        var entry = _archive.GetEntry(entryPath)
            ?? throw new InvalidOperationException(
                $"'{entryPath}' is not in {Id}. It contains: {string.Join(", ", Entries)}");

        using var reader = new StreamReader(entry.Open());

        return reader.ReadToEnd();
    }

    internal static PackedPackage Open(string path)
    {
        var archive = ZipFile.OpenRead(path);

        var entries = archive.Entries.Select(e => e.FullName.Replace('\\', '/')).ToList();

        var nuspecEntry = archive.Entries.SingleOrDefault(e =>
            e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) && !e.FullName.Contains('/'))
            ?? throw new InvalidOperationException($"No nuspec at the root of {path}.");

        using var stream = nuspecEntry.Open();

        var document = XDocument.Load(stream);

        // The nuspec schema namespace varies with what the package declares — a project
        // with no dependencies gets an older one — so nothing here matches on namespace.
        var metadata = document.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "metadata")
            ?? throw new InvalidOperationException($"No <metadata> in the nuspec of {path}.");

        return new PackedPackage(archive, metadata, entries);
    }

    public void Dispose() => _archive.Dispose();
}

/// <summary>
/// Keeps every test that needs the packed solution in one xUnit collection, so the pack
/// runs once rather than per test class.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PackedSolutionCollection : ICollectionFixture<PackedSolutionFixture>
{
    public const string Name = "packed solution";
}

/// <summary>Lazily packs, so a run that touches none of these tests pays nothing.</summary>
public sealed class PackedSolutionFixture : IDisposable
{
    private readonly Lazy<PackedSolution> _packed = new(PackedSolution.Pack, isThreadSafe: true);

    public PackedSolution Packed => _packed.Value;

    public void Dispose()
    {
        if (_packed.IsValueCreated)
        {
            _packed.Value.Dispose();
        }
    }
}
