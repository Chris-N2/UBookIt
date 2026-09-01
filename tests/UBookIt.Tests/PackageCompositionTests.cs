using System.Text.Json;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// What comes out of <c>dotnet pack</c>, asserted against the packages themselves.
/// <para>
/// Every one of these guards a failure that a successful build already reported as
/// fine. The package that existed before this change built cleanly, declared
/// dependencies on <c>UBookIt.Core</c> and <c>UBookIt.Persistence</c> packages that
/// nobody published, and omitted <c>UBookIt.Web</c> — the assembly that renders the
/// booking page — entirely. Nothing anywhere said so.
/// </para>
/// <para>
/// The shared theme is <b>silent absence</b>. A missing assembly, a missing bundle or a
/// missing dependency does not raise anything: the site starts, the backoffice works,
/// and the visitor-facing half of the product is not there. So these tests are about
/// presence, and they read the artefact rather than the project files that produced it.
/// </para>
/// <para>
/// They are not an installation test. <see cref="PackedSolution"/> says why that
/// distinction matters and the packaging documentation records that the installation
/// was walked by hand rather than automated.
/// </para>
/// </summary>
[Collection(PackedSolutionCollection.Name)]
public class PackageCompositionTests(PackedSolutionFixture fixture)
{
    /// <summary>The package a site installs. Everything else has to be reachable from it.</summary>
    private const string Aggregate = "UBookIt";

    private PackedSolution Packed => fixture.Packed;

    [Fact]
    public void Every_ubookit_dependency_names_a_package_this_repository_produces()
    {
        // This is the original defect exactly. UBookIt.Backoffice depended on
        // UBookIt.Core and UBookIt.Persistence, neither of which was ever produced,
        // so `dotnet add package` could not restore. The build was perfectly happy:
        // a ProjectReference resolves at build time and only becomes a package
        // dependency when somebody packs.
        var produced = Packed.Packages.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var package in Packed.Packages)
        {
            var ours = package.Dependencies.Where(d => d.Id.StartsWith("UBookIt", StringComparison.OrdinalIgnoreCase));

            foreach (var dependency in ours)
            {
                Assert.True(
                    produced.Contains(dependency.Id),
                    $"{package.Id} depends on {dependency.Id}, which this repository does not produce. "
                    + $"Restoring it would fail. Produced: {string.Join(", ", produced.Order(StringComparer.Ordinal))}.");
            }
        }
    }

    [Fact]
    public void Every_package_carries_the_same_version()
    {
        // A uBookIt package depending on a different version of a uBookIt package is a
        // resolution nobody chose, and it works on the machine that built it.
        var version = Packed[Aggregate].Version;

        foreach (var package in Packed.Packages)
        {
            Assert.Equal(version, package.Version);
        }

        foreach (var package in Packed.Packages)
        {
            var ours = package.Dependencies.Where(d => d.Id.StartsWith("UBookIt", StringComparison.OrdinalIgnoreCase));

            foreach (var dependency in ours)
            {
                Assert.True(
                    dependency.Version == version,
                    $"{package.Id} depends on {dependency.Id} {dependency.Version}, but everything here is {version}.");
            }
        }
    }

    [Fact]
    public void The_package_a_site_installs_reaches_every_package_this_repository_produces()
    {
        // The drift guard. A fifth assembly added later and left out of the aggregate is
        // the same silent failure that lost UBookIt.Web, wearing a different hat — and
        // whoever adds it will be thinking about their feature, not about this file.
        //
        // The comparison is against what the solution actually produced rather than a
        // list written here, so a new packable project fails this test by existing.
        var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>([Aggregate]);

        while (pending.Count > 0)
        {
            var id = pending.Pop();

            if (!reachable.Add(id))
            {
                continue;
            }

            foreach (var dependency in Packed[id].Dependencies)
            {
                if (dependency.Id.StartsWith("UBookIt", StringComparison.OrdinalIgnoreCase))
                {
                    pending.Push(dependency.Id);
                }
            }
        }

        var produced = Packed.Packages.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unreachable = produced.Except(reachable, StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList();

        Assert.True(
            unreachable.Count == 0,
            $"Installing {Aggregate} would not bring in: {string.Join(", ", unreachable)}. "
            + "Add a ProjectReference to src/UBookIt/UBookIt.csproj, or make the project unpackable if it is not meant to ship.");
    }

    [Fact]
    public void The_front_end_rendering_is_in_the_box()
    {
        // Named separately from the reachability test because this is the specific
        // absence that has no symptom. Nothing references UBookIt.Web, so it was never
        // packed; a site installing uBookIt got the backoffice, the Management API and a
        // booking page that rendered nothing at all, with no error raised anywhere.
        var web = Packed["UBookIt.Web"];

        Assert.Contains("lib/net10.0/UBookIt.Web.dll", web.Entries);
        Assert.Contains("staticwebassets/ubookit.css", web.Entries);
    }

    [Fact]
    public void The_backoffice_client_is_in_the_box()
    {
        // wwwroot/App_Plugins is gitignored and nothing in MSBuild used to build it, so
        // packing a clean clone produced a backoffice package containing no client —
        // and said "Successfully created package". The site would have got the
        // Management API and no Bookings section.
        //
        // Presence, from an ordinary pack. Cheap, and it catches an outright regression —
        // but it cannot tell whether the bundle was built by this build or was simply lying
        // on the disk, which is the distinction that matters here. That is the next test.
        var backoffice = Packed["UBookIt.Backoffice"];

        Assert.Contains("staticwebassets/App_Plugins/UBookItBackoffice/u-book-it-backoffice.js", backoffice.Entries);
        Assert.Contains("staticwebassets/App_Plugins/UBookItBackoffice/umbraco-package.json", backoffice.Entries);
    }

    [Fact]
    public void The_backoffice_client_is_built_by_the_build_and_not_by_the_developer()
    {
        // The only guard here that could have found what was actually wrong, and the reason
        // it takes ~110 seconds instead of six: it deletes the client output AND the Release
        // intermediate, so the bundle in the package must have been produced by this build.
        //
        // Two separate defects passed a presence check. First nothing built the client at
        // all — wwwroot/App_Plugins is gitignored and no target produced it. Then, after
        // that was "fixed" by running vite from MSBuild, static web asset discovery still
        // never saw the result, because wwwroot is globbed at EVALUATION time, before any
        // target runs. Both produced "Successfully created package" and a package with no
        // Bookings section in it.
        //
        // A guard whose subject is a build step has to destroy that step's output before
        // measuring, or it is measuring history. Here that means the intermediate too: with
        // obj/Release warm, deleting the csproj's Content injection outright still yielded a
        // complete package, because discovery's cache answered instead of the build.
        var output = Path.Combine(Path.GetTempPath(), "ubookit-client-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(output);

        try
        {
            using var backoffice = PackedSolution.PackBackofficeFromNothing(output);

            Assert.Contains("staticwebassets/App_Plugins/UBookItBackoffice/u-book-it-backoffice.js", backoffice.Entries);
            Assert.Contains("staticwebassets/App_Plugins/UBookItBackoffice/umbraco-package.json", backoffice.Entries);
        }
        finally
        {
            try
            {
                Directory.Delete(output, recursive: true);
            }
            catch (IOException)
            {
                // A leftover temp directory is not worth failing a test run over.
            }
        }
    }

    [Fact]
    public void The_version_an_editor_sees_is_the_version_they_installed()
    {
        // The backoffice Packages screen reads this manifest. It said 0.0.0, which beside
        // a working package reads as broken or abandoned.
        var backoffice = Packed["UBookIt.Backoffice"];

        var manifest = backoffice.ReadText("staticwebassets/App_Plugins/UBookItBackoffice/umbraco-package.json");

        Assert.Contains($"\"version\": \"{backoffice.Version}\"", manifest);
    }

    [Fact]
    public void The_name_an_editor_sees_is_the_product_they_installed()
    {
        // The Packages screen showed "UBookIt.Backoffice", which is an assembly name. The
        // editor installed uBookIt; they have no reason to know the package is four
        // assemblies, and being shown one of their names invites the question of where the
        // other three went.
        //
        // This is the same rule the nuspec metadata already has to satisfy — a package
        // named after its own assembly reads as one nobody looked at — applied to the one
        // place an editor actually looks. Found by looking at the screen, which is the only
        // way it could have been found.
        var backoffice = Packed["UBookIt.Backoffice"];

        var manifest = backoffice.ReadText("staticwebassets/App_Plugins/UBookItBackoffice/umbraco-package.json");

        using var document = JsonDocument.Parse(manifest);

        var name = document.RootElement.GetProperty("name").GetString();

        Assert.False(string.IsNullOrWhiteSpace(name), "The manifest has no name for the Packages screen to show.");
        Assert.NotEqual(backoffice.Id, name);
    }

    [Theory]
    [InlineData("authors")]
    [InlineData("description")]
    [InlineData("title")]
    [InlineData("projectUrl")]
    [InlineData("license")]
    [InlineData("readme")]
    public void No_package_leaves_its_metadata_unsaid(string element)
    {
        foreach (var package in Packed.Packages)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(package.Value(element)),
                $"{package.Id} has no <{element}> in its nuspec.");
        }
    }

    [Fact]
    public void No_package_describes_itself_with_a_framework_default()
    {
        // A package whose author is its own assembly name and whose description is
        // "Package Description" tells a prospective consumer that nobody has looked at
        // it. Both were true here: UBookIt.Backoffice set Product and Title to
        // "UBookIt.Backoffice", and the other three had no description at all.
        foreach (var package in Packed.Packages)
        {
            Assert.NotEqual(package.Id, package.Value("authors"));
            Assert.NotEqual(package.Id, package.Value("description"));
            Assert.NotEqual(package.Id, package.Value("title"));
            Assert.NotEqual("Package Description", package.Value("description"));

            // A description that is only the name padded out says nothing either.
            Assert.True(
                package.Value("description").Length > 40,
                $"{package.Id}'s description is too short to tell anybody what it is for.");
        }
    }

    [Fact]
    public void No_package_claims_a_customisation_route_the_package_does_not_have()
    {
        // UBookIt.Web's description said "Views are overridable per site or by a theme".
        // The first half is false: these views are compiled into the assembly without source
        // checksums, so a file placed at the same path in a consuming site is never
        // consulted. It is the trap a site author falls into first, packaging's spec has an
        // explicit SHALL NOT about claiming it, and docs/booking-page.md documents it as a
        // route that does NOT work — while the nuspec, which is what a prospective consumer
        // reads on nuget.org, said it does.
        //
        // A word-level check rather than a claim-level one, because "does the prose assert
        // something true" is not decidable. It is deliberately blunt: the supported route is
        // a theme, so a package description has no business using this vocabulary at all,
        // and anyone who needs it will read this message first.
        foreach (var package in Packed.Packages)
        {
            var description = package.Value("description");

            Assert.False(
                description.Contains("overrid", StringComparison.OrdinalIgnoreCase),
                $"{package.Id}'s description talks about overriding. The package's views cannot be "
                + "overridden by a file in the consuming site — they are precompiled without source "
                + "checksums — and openspec/specs/packaging/spec.md forbids claiming that route. "
                + "The supported route is a theme; say that instead.");
        }
    }

    [Fact]
    public void No_package_ships_a_runtime_configuration_file()
    {
        // Microsoft.EntityFrameworkCore.Design turns on runtimeconfig generation so that
        // `dotnet ef` has something to run, which is correct — but a library handing a
        // host a runtime configuration is not. UBookIt.Persistence shipped one.
        foreach (var package in Packed.Packages)
        {
            var offending = package.Entries
                .Where(e => e.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.True(offending.Count == 0, $"{package.Id} ships {string.Join(", ", offending)}.");
        }
    }
}
