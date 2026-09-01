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
        // ONE pack, over a solution containing only the packable projects, written to a
        // temporary directory so nothing lands in the repository.
        //
        // Both halves of that are load-bearing, and both were measured rather than guessed:
        //
        //   - Packing the real solution also BUILDS UBookIt.TestSite, a full Umbraco web
        //     application, and the deletion above invalidates UBookIt.Backoffice which
        //     TestSite references — so that cost is paid every run. 212 seconds.
        //   - Packing the five projects one at a time instead costs about 40 seconds each in
        //     restore and evaluation of Umbraco's package graph. 305 seconds.
        //   - One invocation over these five: 17 seconds, and 6 with the client already
        //     built.
        //
        // The drift guarantee survives because the project list is derived from the real
        // solution rather than written here: a new packable project appears in UBookIt.slnx,
        // gets packed, and then has to be reachable from the aggregate.
        var packList = Path.Combine(output, "UBookItPackable.slnx");

        File.WriteAllText(packList, BuildPackListSolution());

        Run("dotnet", $"pack \"{packList}\" -c Release -o \"{output}\"");

        File.Delete(packList);

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

    /// <summary>
    /// Every project the solution lists that has not opted out of packing.
    /// <para>
    /// Read from the solution rather than listed here, so that a project added later is
    /// included by existing — which is the whole point of the aggregate-drift guard. A
    /// project opts out with a literal <c>&lt;IsPackable&gt;false&lt;/IsPackable&gt;</c>;
    /// that is a text match rather than an MSBuild evaluation, which would cost a process
    /// launch per project. If an opt-out is ever expressed some other way, this over-includes
    /// and the pack of a non-packable project fails loudly — the safe direction.
    /// </para>
    /// </summary>
    private static IEnumerable<string> PackableProjects()
    {
        var solution = XDocument.Parse(RepoFiles.Read("UBookIt.slnx"));

        var projects = solution
            .Descendants().Where(e => e.Name.LocalName == "Project")
            .Select(e => e.Attribute("Path")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.Combine(RepoFiles.Root, path!.Replace('/', Path.DirectorySeparatorChar)))
            .Where(path => !File.ReadAllText(path).Contains("<IsPackable>false</IsPackable>", StringComparison.Ordinal))
            .ToList();

        // A solution that suddenly lists no packable projects would satisfy every assertion
        // made about the packages by producing none of them.
        if (projects.Count == 0)
        {
            throw new InvalidOperationException("UBookIt.slnx lists no packable projects.");
        }

        return projects;
    }

    /// <summary>
    /// A solution file listing only the packable projects, by absolute path so it can live
    /// in a temporary directory rather than in the repository.
    /// </summary>
    private static string BuildPackListSolution()
    {
        var projects = PackableProjects()
            .Select(path => $"  <Project Path=\"{path.Replace('\\', '/')}\" />");

        return $"<Solution>{Environment.NewLine}{string.Join(Environment.NewLine, projects)}{Environment.NewLine}</Solution>{Environment.NewLine}";
    }

    /// <summary>
    /// Rebuilds the backoffice from nothing and packs it, so that what the package contains
    /// is a statement about the <b>build</b> rather than about the developer's disk.
    /// <para>
    /// <b>This has side effects on the working tree, and they are the point.</b> It deletes
    /// <c>src/UBookIt.Backoffice/wwwroot/App_Plugins</c>, <c>bin/Release</c> and
    /// <c>obj/Release</c> — all build output, none of it under version control, all of it
    /// regenerated by the pack that follows. Consequences worth knowing: running the suite
    /// leaves the Release build cold, and a Release build or pack running concurrently in
    /// another window will race this. Debug is untouched, deliberately, because the test
    /// assembly is running out of it.
    /// </para>
    /// <para>
    /// This is expensive — about 110 seconds, which is a clean Razor SDK build against
    /// Umbraco's static web assets and not something this code can shorten. It is kept
    /// separate from the shared fixture, and paid by exactly one test, because only one
    /// guarantee needs it: the other guards concern dependencies, versions and metadata, and
    /// an ordinary six-second pack answers those perfectly well.
    /// </para>
    /// <para>
    /// Cheaper approaches were tried and measured, and none of them works. Deleting only the
    /// client output leaves discovery's cached asset list in obj/, and that cache answers
    /// from the previous build: with it warm, removing the csproj's Content injection
    /// entirely still produced a complete package. Disabling
    /// <c>StaticWebAssetsCacheDefineStaticWebAssetsEnabled</c> does not help either — measured
    /// at 19 client entries either way. The intermediate has to go.
    /// </para>
    /// </summary>
    internal static PackedPackage PackBackofficeFromNothing(string outputDirectory)
    {
        DeleteGeneratedClientOutput();

        var project = Path.Combine(RepoFiles.Root, "src", "UBookIt.Backoffice", "UBookIt.Backoffice.csproj");

        Run("dotnet", $"pack \"{project}\" -c Release -o \"{outputDirectory}\"");

        var produced = Directory
            .GetFiles(outputDirectory, "UBookIt.Backoffice.*.nupkg")
            .Single(path => !path.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase));

        return PackedPackage.Open(produced);
    }

    private static void DeleteGeneratedClientOutput()
    {
        var backoffice = Path.Combine(RepoFiles.Root, "src", "UBookIt.Backoffice");

        // The Release intermediate goes too, and that is not belt-and-braces — without it
        // this guard does not work.
        //
        // Static web asset discovery caches its resolved asset list in obj/. With that cache
        // warm, the package gets its client from the previous build no matter what the
        // current one does: deleting the Content injection from the csproj entirely still
        // produced a complete package. Measured both ways from a clean intermediate — no
        // injection gives zero client entries, injection gives nineteen — so the cache, not
        // the build, was answering.
        //
        // Release only. The test assembly is running from bin/Debug with
        // UBookIt.Backoffice.dll loaded; the pack is Release, so nothing is pulled out from
        // under the running process.
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

        // Without this the fixture takes fifteen minutes and forty seconds, every time.
        //
        // That number is not arbitrary: it is MSBuild's node-reuse timeout. The worker
        // processes MSBuild keeps alive for the next build inherit the redirected stdout
        // handle, so the pipe does not close when `dotnet pack` exits — the read completes
        // only when the last worker finally expires. The build itself takes twenty seconds.
        //
        // It reads as a slow build, which is why it was worth writing down.
        info.Environment["MSBUILDDISABLENODEREUSE"] = "1";

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException($"Could not start '{fileName} {arguments}'.");

        // Both pipes are drained concurrently, and that is not style.
        //
        // Reading one to the end before starting the other deadlocks as soon as the child
        // fills the pipe it is NOT being read from: the child blocks writing, we block
        // reading, and neither ever moves. It survived early runs only because the output
        // was small; the first run that forced a real rebuild hung for the full timeout and
        // looked like a slow build rather than a hang, which cost a while to see.
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(milliseconds: 10 * 60 * 1000))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException($"'{fileName} {arguments}' did not finish within ten minutes.");
        }

        // Bounded, because the output is only ever used to explain a failure. Anything
        // still holding the pipe open after the process has exited must not be able to hang
        // the suite — belt and braces alongside disabling node reuse above.
        Task.WaitAll([stdoutTask, stderrTask], TimeSpan.FromSeconds(30));

        var stdout = stdoutTask.IsCompletedSuccessfully ? stdoutTask.Result : "(stdout not captured)";
        var stderr = stderrTask.IsCompletedSuccessfully ? stderrTask.Result : "(stderr not captured)";

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
