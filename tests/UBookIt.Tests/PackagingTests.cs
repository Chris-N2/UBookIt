using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Tests.Support;
using UBookIt.Web.Packaging;
using Umbraco.Cms.Web.Common.Views;

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

        // And it is the file Umbraco will actually read.
        //
        // Every other guard in this class reads package.xml off disk. Umbraco does not
        // reach for it first: at each entry point — the hash,
        // TryGetEmbeddedPackageDataManifest, and the import — it calls
        // GetEmbeddedPackageZipStream and only falls back to the XML when no
        // package.zip resource exists.
        //
        // So an embedded package.zip silently becomes the real manifest and every
        // check here starts fencing a file that no longer installs anything.
        // Demonstrated: a zip declaring <Stylesheets> and a branded template left all
        // seven of these tests green while the Booking Page type was not installed at
        // all and a rogue template wrote a file into the site.
        //
        // This is not a hypothetical route. A maintainer wanting to ship media or
        // stylesheets would follow Umbraco's documented path, which IS package.zip.
        // Fence the mechanism rather than the instance, exactly as the allowlist does.
        Assert.DoesNotContain(
            resources,
            name => name.EndsWith(".package.zip", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_shipped_template_carries_no_markup_of_its_own()
    {
        // Design D2. The import replaces a template's contents wholesale, and it
        // re-imports the WHOLE manifest — so a release carrying a migration step
        // rewrites this template even though that release never mentioned templates.
        // The plan is run-once, so an ordinary release does not, but the exposure is
        // real whenever we choose to ship a step. A delegate makes it cost nothing.
        //
        // The rule for whoever wants to add "just a wrapper div" later: anything
        // worth keeping in the shipped template is worth not shipping there. Put it
        // in the site's OWN template, which the package never touches — not in a view
        // override, which does not work (design D3).
        // Exactly one template, and this is load-bearing rather than tidiness: taking
        // the FIRST template let a SECOND one carry `<div class="branding"><h1>…`
        // through this check untouched.
        //
        // Scoped to children of <Templates>, NOT to "any <Template> that has a
        // <Design>". The doctype's <AllowedTemplates> also contains <Template>
        // elements, so the earlier filter had to exclude them somehow and chose the
        // wrong discriminator: a second REAL template declaring only Name, Alias and
        // Key then installed, and Umbraco scaffolded a second `.cshtml` into the
        // site — a file on a path the site owns, which the package undertakes not to
        // write.
        var templates = Manifest().Root!.Element("Templates")?.Elements("Template").ToList()
            ?? [];

        Assert.Single(templates);

        var designElement = templates[0].Element("Design");
        Assert.NotNull(designElement);

        var design = designElement!.Value;

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

        // The base type is named, not merely shaped. `@inherits System.Object` passed
        // a shape-only check while making every published booking page fail to
        // compile — the same "a string with no tie to a type" family as the component
        // name below.
        Assert.Equal(
            $"@inherits {typeof(UmbracoViewPage).FullName}",
            lines[0]);

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
    public void The_manifest_declares_only_the_sections_the_package_needs()
    {
        // An ALLOWLIST, deliberately, and this replaced a denylist that named four
        // file-writing sections. The denylist read as a fence and was not one:
        // `<DataTypes>`, `<DictionaryItems>` and `<Languages>` sailed through it —
        // they write no files, so they missed the files check, and they are not
        // content, so they missed the content check. All three install schema and
        // configuration into a consumer's site; a `<Language>` entry adds a language.
        //
        // A denylist can only ever fence the sections someone thought of. This fences
        // everything, so a manifest section added later has to be argued for here
        // rather than discovered on someone else's site.
        var permitted = new[] { "info", "Templates", "DocumentTypes" };

        var declared = Manifest().Root!.Elements()
            .Select(element => element.Name.LocalName)
            .Where(name => !permitted.Contains(name, StringComparer.Ordinal))
            .ToList();

        Assert.True(
            declared.Count == 0,
            $"package.xml declares {string.Join(", ", declared.Select(name => $"<{name}>"))}, "
            + "which this package has not justified installing into a consumer's site. "
            + $"Permitted: {string.Join(", ", permitted.Select(name => $"<{name}>"))}. "
            + "Anything else writes files, schema or configuration a site did not ask "
            + "for — add it here only with a reason.");

        // Non-vacuity: an allowlist over an empty document permits everything.
        Assert.NotEmpty(Manifest().Root!.Elements());
    }

    [Fact]
    public void Every_front_end_asset_arrives_by_a_decided_route()
    {
        // The allowlist above says what the manifest may NOT carry. This says where a
        // front-end asset must instead live, which is the other half and is a positive
        // rule rather than a fence: an asset can only reach a site as a static web
        // asset (wwwroot, served from the package's own path) or not at all.
        //
        // Why it is worth asserting. The mechanism decides who can never be fixed
        // again. A manifest import writes a real file into the site, and the migration
        // plan is run-once by design, so that file is then never touched. Some of this
        // package's CSS carries accessibility weight — focus visibility, the derivation
        // of muted and border colours from the host's text colour, target size — so a
        // site-owned stylesheet is one where a later accessibility fix reaches no
        // existing install, ever, and nothing reports that it did not.
        //
        // A stylesheet dropped anywhere else under UBookIt.Web ships inside the
        // assembly and is served by nothing, which fails silently in the opposite
        // direction: present, referenced, and 404.
        // Enumerated directly rather than through RepoFiles.Paths, whose own vacuity
        // guard asserts a non-empty result. That guard is right for "find the things
        // and check them" and wrong for "prove there are none" — here an empty result
        // is the passing case, and borrowing the helper made the test fail on success.
        var web = Path.Combine(RepoFiles.Root, "src", "UBookIt.Web");

        var strays = new[] { "*.css", "*.js" }
            .SelectMany(pattern => Directory.EnumerateFiles(web, pattern, SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}wwwroot{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(RepoFiles.Root, path))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            strays.Count == 0,
            $"Front-end asset(s) outside wwwroot: {string.Join(", ", strays)}. A static "
            + "web asset under wwwroot is served at _content/UBookIt.Web/... and is "
            + "replaced by an upgrade; anywhere else it is compiled into the assembly "
            + "and served by nothing.");

        // Scope, stated because the test name is broader than the scan: only
        // `UBookIt.Web` is examined, because it is the only project that ships
        // front-end assets. `UBookIt.Backoffice` has its own `wwwroot` for the
        // backoffice client, which is a different delivery story and not governed here.
        //
        // Non-vacuity: a scan finding no assets at all would permit everything. This is
        // a PIN, not a rule — it will need editing the day the deferred JS layer ships,
        // and that is intended: a second asset should be a decision, not a discovery.
        var assets = RepoFiles
            .Paths("src/UBookIt.Web/wwwroot", "*")
            .Select(path => Path.GetFileName(path))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["ubookit.css"], assets);
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
