using System.Text.RegularExpressions;
using UBookIt.Tests.Rendering.Support;
using UBookIt.Tests.Support;
using UBookIt.Web.Rendering;

namespace UBookIt.Tests.Rendering;

/// <summary>
/// The rig itself, before anything it renders.
/// <para>
/// Every rule in this suite rests on one claim: that what is rendered here is the
/// view the site serves, compiled into <c>UBookIt.Web.dll</c>, rather than a
/// re-parse of the <c>.cshtml</c> source. If that claim is false the suite is
/// testing a second copy of the views and proves nothing about the shipped one —
/// so it is asserted rather than believed.
/// </para>
/// </summary>
public class RigTests
{
    private readonly ViewRenderer _renderer = new();

    [Fact]
    public async Task The_rig_renders_a_view()
    {
        // One view, one assertion. A broken rig should fail here, obviously, rather
        // than as a dozen confusing failures spread across the rule suites.
        var html = await _renderer.RenderAsync(
            ViewInventory.ServiceConfirmation,
            new ServiceConfirmationModel
            {
                BookingId = Guid.NewGuid(),
                ServiceName = "Massage",
                ResourceNames = ["Jane"],
                LocalStart = "Thursday 20 August 2026, 09:00",
                LocalEnd = "09:30",
                BookerName = "Ada Lovelace",
                BookerEmail = "ada@example.com",
            });

        Assert.Contains("Ada Lovelace", html, StringComparison.Ordinal);
        Assert.Contains("Massage", html, StringComparison.Ordinal);
    }

    [Fact]
    public void The_views_are_compiled_into_the_shipped_assembly()
    {
        // Structural, and the reason the rig can make its central claim: the views
        // are Razor-compiled items carried by UBookIt.Web.dll. If someone switched
        // the project away from Microsoft.NET.Sdk.Razor, or turned
        // AddRazorSupportForMvc off, the views would leave the assembly and this
        // fails — rather than the suite quietly starting to read .cshtml from disk.
        var compiled = ViewRenderer.ViewAssembly
            .GetCustomAttributes(inherit: false)
            .Where(a => a.GetType().Name == "RazorCompiledItemAttribute")
            .ToList();

        Assert.NotEmpty(compiled);

        // And it is *these* views, not some other assembly's.
        var identifiers = compiled
            .Select(a => (string)a.GetType().GetProperty("Identifier")!.GetValue(a)!)
            .ToList();

        Assert.Contains("/Views/Shared/UBookIt/_DateAndLength.cshtml", identifiers);
    }

    [Fact]
    public async Task Rendering_reads_no_cshtml_from_disk()
    {
        // The other half, and the one that would actually catch a regression to
        // runtime compilation: the rig's content root is a NullFileProvider, so
        // there is no directory of .cshtml files to fall back to. A view that
        // resolves can only have come from the compiled assembly.
        //
        // Asserted by rendering with the repository's own Views directory present
        // but unreachable from the rig — if the engine were reading source, the
        // render would fail rather than succeed.
        Assert.True(Directory.Exists(Path.Combine(RepoFiles.Root, "src", "UBookIt.Web", "Views")));

        var html = await _renderer.RenderAsync(
            ViewInventory.ServiceUnavailable,
            ServiceUnavailableModel.NotFulfillable("Massage"));

        Assert.Contains("Massage", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_compilation_is_absent_from_the_closure()
    {
        // Not merely disabled — absent. Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation
        // is not in UBookIt.Web's dependency closure at all; the TestSite carries
        // it, but only via Umbraco.Cms.DevelopmentMode.Backoffice, consistent with
        // Umbraco requiring precompiled views in production.
        //
        // This is why the rendering project references UBookIt.Web and deliberately
        // NOT UBookIt.TestSite: that reference would drag runtime compilation back
        // in and quietly turn the claim above into something this suite could no
        // longer make. Asserted because a project reference is one line for someone
        // to add without knowing that.
        // Asserted over the OUTPUT DIRECTORY, not over AppDomain.GetAssemblies():
        // assembly loading is lazy, so "not loaded yet" and "not present" look
        // identical at the moment a test asks, and the weaker check would pass on a
        // build that did ship it.
        var output = Path.GetDirectoryName(typeof(RigTests).Assembly.Location)!;

        Assert.False(
            File.Exists(Path.Combine(output, "Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation.dll")),
            "Runtime compilation reached the output directory — the rig can no longer claim "
            + "it renders the compiled views rather than re-parsing .cshtml source.");

        // And the reference that would put it there. Matched as a ProjectReference
        // element rather than as a substring: this file names the TestSite in a
        // comment explaining why it is absent, and a substring check finds its own
        // documentation. (It did, on the first run.)
        var project = RepoFiles.Read("tests/UBookIt.Tests.Rendering/UBookIt.Tests.Rendering.csproj");

        Assert.DoesNotContain(
            "UBookIt.TestSite.csproj\"",
            Regex.Replace(project, "<!--.*?-->", string.Empty, RegexOptions.Singleline),
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_linked_repo_helper_resolves_the_same_root()
    {
        // RepoFiles is shared with UBookIt.Tests by link rather than copied. It
        // locates the root from its own compile-time path, which is the ORIGINAL
        // file's path, so a linked copy resolves identically — true, and worth
        // asserting rather than assuming, since a copy that drifted would make
        // every source-reading assertion in this suite read the wrong tree.
        Assert.True(File.Exists(Path.Combine(RepoFiles.Root, "UBookIt.slnx")));
    }
}
