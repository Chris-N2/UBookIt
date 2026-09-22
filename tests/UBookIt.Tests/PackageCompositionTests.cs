using System.Globalization;
using System.Text.RegularExpressions;
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
    [InlineData("icon")]
    public void No_package_leaves_its_metadata_unsaid(string element)
    {
        foreach (var package in Packed.Packages)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(package.Value(element)),
                $"{package.Id} has no <{element}> in its nuspec.");
        }
    }

    /// <summary>
    /// A nuspec element naming an embedded file is worth nothing unless the file is in the
    /// package with it.
    /// </summary>
    /// <remarks>
    /// <c>readme</c> and <c>icon</c> both name a path INSIDE the package, and both are put
    /// there by a separate <c>None ... Pack="true"</c> item. Those are two declarations that
    /// have to agree, and nothing in the build makes them: MSBuild will happily emit a nuspec
    /// declaring <c>icon.png</c> while the ItemGroup meant to carry it is conditioned out, and
    /// report success. nuget.org then renders a blank.
    /// <para>
    /// Both are checked, not just the one that prompted this, because on this project the
    /// instance is never the class — a guard written for the icon alone would have left the
    /// readme's identical failure mode unwatched.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("readme")]
    [InlineData("icon")]
    public void Every_package_contains_the_metadata_file_it_declares(string element)
    {
        foreach (var package in Packed.Packages)
        {
            var declared = package.Value(element);

            Assert.False(
                string.IsNullOrWhiteSpace(declared),
                $"{package.Id} declares no <{element}>.");

            var path = declared.Replace('\\', '/');

            Assert.True(
                package.Entries.Contains(path),
                $"{package.Id}'s nuspec declares <{element}>{declared}</{element}>, but the "
                + $"package does not contain '{path}'. It contains: "
                + string.Join(", ", package.Entries));
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

    /// <summary>
    /// Every Umbraco dependency a published package declares names an upper bound.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A NuGet dependency version is a <b>minimum</b>, so <c>Umbraco.Cms.Web.Website 18.2.0</c>
    /// says "18.2.0 or higher" and nothing else. For as long as uBookIt published one line that
    /// was merely imprecise — it is why the Umbraco Marketplace lists uBookIt as running on v17
    /// <i>and</i> v18, which is false, and why nothing a resolver reads has ever contradicted
    /// that. With two lines published against two Umbraco majors it stops being imprecise: the
    /// constraint exists only in prose, and no package manager reads prose.
    /// </para>
    /// <para>
    /// <b>Read from the packed nuspec, not from <c>Directory.Packages.props</c>, and that is the
    /// whole point.</b> The props file is what we intended; the nuspec is what a consumer's
    /// resolver actually gets, and the two can disagree — a range that a project overrides, or a
    /// dependency that reaches the package through a path central management does not govern,
    /// would leave the props file looking correct and the package unbounded. A guard derived
    /// from the same file that produced the defect would agree with it.
    /// </para>
    /// <para>
    /// uBookIt's own packages are deliberately out of scope: they are versioned in lockstep by
    /// this repository and already covered by
    /// <see cref="Every_ubookit_dependency_names_a_package_this_repository_produces"/>.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_umbraco_dependency_names_an_upper_bound()
    {
        var unbounded = new List<string>();
        var checkedDependencies = 0;

        foreach (var package in Packed.Packages)
        {
            foreach (var (id, version) in package.Dependencies)
            {
                if (!id.StartsWith("Umbraco.Cms", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                checkedDependencies++;

                // A NuGet range with an upper bound ends in ')' or ']'. A bare version, or a
                // range open at the top like "[18.2.0, )", does not bound anything above.
                var bounded =
                    (version.EndsWith(')') || version.EndsWith(']'))
                    && !version.TrimEnd().EndsWith(", )", StringComparison.Ordinal)
                    && !version.TrimEnd().EndsWith(",)", StringComparison.Ordinal);

                if (!bounded)
                {
                    unbounded.Add($"{package.Id} -> {id} '{version}'");
                }
            }
        }

        // ANTI-VACUITY FIRST. A package set with no Umbraco dependency in it satisfies "none is
        // unbounded" perfectly, and would do so if the id prefix were ever wrong.
        Assert.True(
            checkedDependencies > 0,
            "No packed package declares an Umbraco.Cms* dependency, so this guard ran over "
            + "nothing. Either the packages stopped depending on Umbraco or the id prefix this "
            + "guard matches on is wrong.");

        Assert.True(
            unbounded.Count == 0,
            $"{unbounded.Count} Umbraco dependency declaration(s) carry no upper bound, so the "
            + "published package claims to support every future Umbraco major:"
            + $"{Environment.NewLine}  " + string.Join(Environment.NewLine + "  ", unbounded));
    }

    /// <summary>
    /// The bound admits the Umbraco major this line targets, and excludes the next.
    /// </summary>
    /// <remarks>
    /// <b>Covers the one scenario <see cref="Every_umbraco_dependency_names_an_upper_bound"/>
    /// does not.</b> That guard asks only whether <i>a</i> ceiling exists, so
    /// <c>[17.6.2,17.7.0)</c> satisfies it while refusing every Umbraco 17 minor this line claims
    /// to support — a bound that is present, wrong, and invisible. QA found the gap when the
    /// requirement moved onto this branch; the requirement states both halves, so both are
    /// guarded.
    /// <para>
    /// The expected majors are derived from the declared version rather than written down, so
    /// this cannot be left behind by a version bump — the same reasoning the readme's image pin
    /// already uses. It is deliberately silent about the lower bound's minor and patch: which
    /// Umbraco 17 the line needs is a separate decision, and pinning it here would make every
    /// dependency bump a test edit.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_bound_admits_this_major_and_excludes_the_next()
    {
        var declared = VersionTruthTests.DeclaredVersion();
        var major = int.Parse(declared.Split('.')[0], CultureInfo.InvariantCulture);
        var expectedCeiling = $"{major + 1}.0.0";

        var wrong = new List<string>();
        var checkedDependencies = 0;

        foreach (var package in Packed.Packages)
        {
            foreach (var (id, version) in package.Dependencies)
            {
                if (!id.StartsWith("Umbraco.Cms", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                checkedDependencies++;

                var match = Regex.Match(version, @"^\[\s*(?<low>[^,\s]+)\s*,\s*(?<high>[^)\]\s]+)\s*\)$");

                if (!match.Success)
                {
                    wrong.Add($"{package.Id} -> {id} '{version}' is not an interval with an exclusive upper bound.");
                    continue;
                }

                var low = match.Groups["low"].Value;
                var high = match.Groups["high"].Value;

                if (!low.StartsWith($"{major}.", StringComparison.Ordinal))
                {
                    wrong.Add($"{package.Id} -> {id} '{version}' starts at '{low}', which is not an Umbraco {major} version.");
                }

                if (high != expectedCeiling)
                {
                    wrong.Add($"{package.Id} -> {id} '{version}' stops at '{high}', not '{expectedCeiling}'. "
                        + $"A ceiling inside Umbraco {major} refuses releases this line supports.");
                }
            }
        }

        Assert.True(
            checkedDependencies > 0,
            "No packed package declares an Umbraco.Cms* dependency, so this guard ran over nothing.");

        Assert.True(
            wrong.Count == 0,
            $"{wrong.Count} Umbraco dependency range(s) do not admit Umbraco {major} and exclude "
            + $"{major + 1}:{Environment.NewLine}  " + string.Join(Environment.NewLine + "  ", wrong));
    }
}
