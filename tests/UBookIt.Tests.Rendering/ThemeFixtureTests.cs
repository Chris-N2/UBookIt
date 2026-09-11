using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;
using UBookIt.Tests.Rendering.Support;
using UBookIt.Web.Theming;

namespace UBookIt.Tests.Rendering;

/// <summary>
/// The setup check, before any result from it is believed.
/// <para>
/// "The package's view rendered" is the <i>same observation</i> whether precedence
/// went the wrong way or the theme assembly was never discovered at all. The spike
/// learned that the expensive way: it read a first render as "the architecture is
/// dead" when it was a two-line registration-order bug. So these tests ask the setup
/// questions separately, and a later precedence failure cannot be misread as one of
/// them.
/// </para>
/// </summary>
public class ThemeFixtureTests
{
    private static readonly System.Reflection.Assembly Fixture =
        ThemedRendering.ThemeAssemblyUnderTest;

    [Fact]
    public void The_fixtures_views_are_precompiled_into_its_own_assembly()
    {
        var descriptors = DescriptorsOf(Fixture);

        Assert.NotEmpty(descriptors);

        foreach (var descriptor in descriptors)
        {
            Assert.Equal(Fixture, descriptor.Type!.Assembly);
        }
    }

    [Fact]
    public void The_fixture_supplies_exactly_the_views_each_theme_is_meant_to_supply()
    {
        // At the expected identifiers, spelled the way the view-location format
        // produces them — so a fixture that drifted from the contract would fail here
        // rather than as an unexplained resolution failure later.
        var expected = new List<string>();

        expected.AddRange(
            UBookItThemeContract.RequiredViews.Select(v => v.PathIn(ThemeFixture.ThemeFixture.Complete)));

        expected.Add($"{UBookItThemeContract.RootFor(ThemeFixture.ThemeFixture.Partial)}/Components/Booking/Default.cshtml");
        expected.Add($"{UBookItThemeContract.RootFor(ThemeFixture.ThemeFixture.Partial)}/Components/BookingFlow/Catalogue.cshtml");
        expected.Add($"{UBookItThemeContract.RootFor(ThemeFixture.ThemeFixture.WrongModel)}/Components/Booking/Default.cshtml");
        expected.Add($"{UBookItThemeContract.RootFor(ThemeFixture.ThemeFixture.BaseModel)}/Components/Booking/Default.cshtml");

        // The fixture assembly carries EMAIL templates as well as theme views — see its csproj,
        // which explains why one assembly holds both. They are enumerated here rather than
        // filtered out, so this stays an EXACT inventory: a stray file of either kind fails, and
        // so does a deleted one. Filtering by path prefix would have let the email fixtures grow
        // or vanish unwatched, which is precisely what this test exists to prevent.
        expected.AddRange(
            new[] { "BookerPlaced", "BookerConfirmed", "BookerCancelled", "InternalPlaced" }
                .Select(name => $"/Views/Partials/UBookIt/Emails/{name}.cshtml"));

        var actual = DescriptorsOf(Fixture).Select(d => d.RelativePath);

        Assert.Equal(
            expected.Order(StringComparer.Ordinal),
            actual.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void No_loose_cshtml_reaches_the_build_output()
    {
        // A theme is an assembly, not a folder of loose files — and if loose files
        // were reaching the output, a resolution result might be about them rather
        // than about a precompiled second assembly winning. The rig's content root is
        // a NullFileProvider, so nothing on disk could be read anyway; this asserts
        // there is nothing there to read in the first place.
        var loose = Directory
            .EnumerateFiles(AppContext.BaseDirectory, "*.cshtml", SearchOption.AllDirectories)
            .ToList();

        Assert.True(
            loose.Count == 0,
            "Loose .cshtml files reached the test output: " + string.Join(", ", loose));
    }

    [Fact]
    public void Runtime_compilation_is_absent_from_this_suite()
    {
        // D8, made structural. The spike measured precedence on a development site,
        // which carries Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation via
        // Umbraco.Cms.DevelopmentMode.Backoffice. ⑫'s override finding had exactly
        // that gap, and `packaging` carries a requirement that documentation SHALL NOT
        // assert more than has been measured because of it.
        //
        // This project deliberately does not reference UBookIt.TestSite, so runtime
        // compilation is not in its closure — which is what lets the resolution guard
        // be a measurement of the configuration a production site runs. Asserted,
        // because a project reference added later would silently take it away.
        const string RuntimeCompilation = "Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation";

        var onDisk = Directory
            .EnumerateFiles(AppContext.BaseDirectory, RuntimeCompilation + ".dll", SearchOption.AllDirectories)
            .ToList();

        Assert.True(
            onDisk.Count == 0,
            "Runtime compilation reached this suite's output: " + string.Join(", ", onDisk)
            + ". The resolution guard's claim to measure a production configuration "
            + "depends on its absence.");

        Assert.DoesNotContain(
            AppDomain.CurrentDomain.GetAssemblies(),
            assembly => assembly.GetName().Name == RuntimeCompilation);
    }

    [Fact]
    public void The_fixture_lives_under_tests_and_not_in_the_shipped_view_set()
    {
        // The landmine named in the design. ViewInventory scans
        // src/UBookIt.Web/Views on disk, so a theme fixture placed there would
        // silently join the shipped view set and every rendering rule would start
        // asserting over a test fixture — while reporting green.
        Assert.DoesNotContain(
            ViewInventory.Shipped,
            view => view.Contains("/Themes/", StringComparison.OrdinalIgnoreCase));

        var root = UBookIt.Tests.Support.RepoFiles.Root;

        Assert.True(
            Directory.Exists(Path.Combine(root, "tests", "UBookIt.Tests.ThemeFixture", "Views")),
            "The theme fixture's views are not where this suite believes they are.");

        Assert.False(
            Directory.Exists(
                Path.Combine(root, "src", "UBookIt.Web", "Views", "Shared", "UBookIt", "Themes")),
            "A theme directory exists inside the package's own shipped views. "
            + "ViewInventory scans that directory, so everything in it becomes part of "
            + "the shipped view set that every rendering rule asserts over.");
    }

    private static IReadOnlyList<CompiledViewDescriptor> DescriptorsOf(System.Reflection.Assembly assembly)
    {
        var part = new CompiledRazorAssemblyPart(assembly);

        return
        [
            .. ((IRazorCompiledItemProvider)part).CompiledItems
                .Select(item => new CompiledViewDescriptor(item)),
        ];
    }
}
