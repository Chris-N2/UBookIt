using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Tests.Support;
using UBookIt.Web.Packaging;

namespace UBookIt.Tests;

/// <summary>
/// What the package installs into a site, and the constraints on it.
/// <para>
/// These guard two different kinds of failure, and it is worth knowing which is
/// which. A manifest the plan cannot resolve fails <b>loudly</b> — the resource
/// lookup throws while the plan is built during boot, so the site does not start and
/// every request 500s. That is guarded here anyway, because a red test names the
/// cause immediately and a failed boot costs a deploy to diagnose.
/// </para>
/// <para>
/// The constraints on what the manifest may <i>contain</i> are the silent ones. A
/// template that grows markup, or a manifest that starts writing files into a site,
/// raises nothing at all: the damage appears on a consumer's site, at some later
/// release, and looks like their own mistake. Those cannot be found by running the
/// package, so they are asserted here.
/// </para>
/// </summary>
public class PackagingTests
{
    private const string ManifestPath = "src/UBookIt.Web/Packaging/package.xml";

    private static XDocument Manifest() => XDocument.Parse(RepoFiles.Read(ManifestPath));

    [Fact]
    public void The_manifest_is_embedded_under_the_name_the_plan_resolves()
    {
        // The migration resolves its manifest as an embedded resource named for its
        // own namespace. Move the class, move the file, rename the namespace, or drop
        // the <EmbeddedResource> entry, and the lookup throws during boot: the site
        // will not start and every request 500s.
        //
        // Measured, having first claimed the opposite: the failure is loud, named and
        // unmissable. This test earns its place by naming the cause in a second
        // rather than after a deploy — not by catching something that would otherwise
        // pass unnoticed.
        var planType = typeof(ImportBookingPageSchema);
        var expected = $"{planType.Namespace}.package.xml";

        var resources = planType.Assembly.GetManifestResourceNames();

        Assert.Contains(expected, resources);

        // And it is really the manifest, not an empty file that happens to be there.
        using var stream = planType.Assembly.GetManifestResourceStream(expected);
        Assert.NotNull(stream);

        var document = XDocument.Load(stream!);
        Assert.Equal("umbPackage", document.Root?.Name.LocalName);
    }

    [Fact]
    public void The_shipped_template_carries_no_markup_of_its_own()
    {
        // Design D2, and a constraint rather than a preference: the import replaces a
        // template's contents wholesale on any manifest change, so anything in here
        // is destroyed on a consumer's site by a release that need not even mention
        // templates. A delegate makes that harmless.
        //
        // The rule for whoever wants to add "just a wrapper div" later: anything
        // worth keeping in the shipped template is worth not shipping there. Put it
        // in a view the site can override instead.
        var design = Manifest()
            .Descendants("Template")
            .Select(t => t.Element("Design")?.Value)
            .FirstOrDefault(d => d is not null);

        Assert.NotNull(design);

        var lines = design!
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

        // Each line must BE what it is, not merely contain it.
        //
        // The earlier version checked the line count, that line 1 started with
        // "@inherits", that line 2 contained "Component.InvokeAsync", and that there
        // was no "<" anywhere. All four passed on:
        //
        //     @inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
        //     Book with us today &mdash; powered by uBookIt @await Component.InvokeAsync("BookingFlow")
        //
        // — text, an entity and branding, shipped in a template that a later release
        // silently replaces. Anchored matching is what closes that.
        Assert.Equal(2, lines.Count);

        Assert.Matches(@"^@inherits\s+[\w.]+$", lines[0]);
        Assert.Matches(@"^@await\s+Component\.InvokeAsync\(""\w+""\)$", lines[1]);

        // No layout imposed on the consumer: unset means the site's own _ViewStart
        // applies and the page wears the site's chrome.
        Assert.DoesNotContain("Layout", design, StringComparison.Ordinal);
    }

    [Fact]
    public void The_component_the_template_delegates_to_exists()
    {
        // The manifest names a view component in a string. Nothing connected that
        // string to a real type: renaming it to "BookinFlow" left the entire suite
        // green while every shipped booking page on every consumer site would throw
        // at render.
        //
        // This is spec Requirement 1's headline scenario — "a published page of the
        // shipped type renders the flow" — which otherwise has no regression guard at
        // all, only a one-off live check that leaves nothing behind.
        var invoked = Regex
            .Match(
                Manifest().Descendants("Design").Single().Value,
                @"Component\.InvokeAsync\(""(?<name>\w+)""\)")
            .Groups["name"].Value;

        Assert.NotEmpty(invoked);

        // ASP.NET Core's convention: the component's name is its type name with any
        // "ViewComponent" suffix removed, unless [ViewComponent(Name = …)] overrides
        // it. Derived rather than hardcoded, so renaming the class is caught too.
        var componentNames = typeof(BookingPagePackageMigrationPlan).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsPublic: true }
                && typeof(ViewComponent).IsAssignableFrom(type))
            .Select(type => type.GetCustomAttribute<ViewComponentAttribute>()?.Name
                ?? (type.Name.EndsWith("ViewComponent", StringComparison.Ordinal)
                    ? type.Name[..^"ViewComponent".Length]
                    : type.Name))
            .ToList();

        Assert.NotEmpty(componentNames);

        Assert.True(
            componentNames.Contains(invoked, StringComparer.Ordinal),
            $"The shipped template invokes view component '{invoked}', which does not "
            + $"exist in UBookIt.Web. Available: {string.Join(", ", componentNames)}. "
            + "Every published booking page would throw at render.");
    }

    [Fact]
    public void The_manifest_installs_no_content()
    {
        // "Installs a document type, never a document" is checked rather than
        // intended. The manifest format carries content perfectly well, so its
        // absence is a decision and this is where the decision is enforced.
        Assert.Empty(Manifest().Descendants("Documents"));
        Assert.Empty(Manifest().Descendants("DocumentSet"));
        Assert.Empty(Manifest().Descendants("Media"));
        Assert.Empty(Manifest().Descendants("MediaItems"));
    }

    [Fact]
    public void The_manifest_writes_no_files_into_the_site()
    {
        // `PackageDataInstallation` imports these sections by writing FILES into the
        // consumer's site. The package installs exactly one file — the Booking Page
        // template — and everything else it ships is compiled into the assembly.
        //
        // Checked because the alternative is discovering it on someone else's site.
        // A manifest that gained a <PartialViews> or <Stylesheets> entry would start
        // writing into paths the site owns, on every install, with nothing here
        // objecting. The one section deliberately used, <Templates>, is asserted
        // elsewhere to contain exactly one delegating template.
        foreach (var section in new[] { "PartialViews", "Stylesheets", "Scripts", "Files" })
        {
            Assert.True(
                Manifest().Descendants(section).All(element => !element.HasElements),
                $"package.xml declares <{section}>, which writes files into the "
                + "consumer's site. Only the Booking Page template may be installed.");
        }
    }

    [Fact]
    public void The_shipped_document_type_is_creatable_on_a_fresh_install()
    {
        // Settled at apply: allowed at root, because with this false the type is
        // creatable nowhere until a site author wires allowed-children, and the
        // package reads as broken on first use.
        //
        // Asserted rather than left to the manifest because changing it is
        // upgrade-visible — the import resets AllowAtRoot on existing installs — so
        // it should be a decision someone revisits, not an edit someone makes.
        var info = Manifest().Descendants("DocumentType").Single().Element("Info")!;

        Assert.Equal("True", info.Element("AllowAtRoot")?.Value);

        // It must also have a template to render through, or a published page of this
        // type shows nothing.
        Assert.Equal("uBookItBookingPage", info.Element("DefaultTemplate")?.Value);
    }

    [Fact]
    public void The_shipped_document_type_carries_no_properties()
    {
        // v1 ships none: the flow reads its state from the URL, so the page needs no
        // configuration. Recorded as an assertion because the reasoning is
        // measurement-backed and worth not losing — a property added in a later
        // release DOES reach sites that installed this one, and nothing is ever
        // removed, so deferring configuration costs nothing.
        //
        // When configuration is genuinely wanted, this test is the place that says
        // the decision is being changed deliberately.
        var properties = Manifest().Descendants("GenericProperty").ToList();

        Assert.Empty(properties);
    }
}
