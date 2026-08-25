using System.Xml.Linq;
using UBookIt.Tests.Support;
using UBookIt.Web.Packaging;

namespace UBookIt.Tests;

/// <summary>
/// What the package installs into a site, and the constraints on it.
/// <para>
/// Every failure these guard against is <b>silent</b>. A manifest the plan cannot
/// resolve produces no error — the site starts and simply has no schema. A template
/// that grows markup produces no error either; the loss only appears on a consumer's
/// site, at the next release, at startup. Neither is discoverable by running the
/// package, so both are asserted here.
/// </para>
/// </summary>
public class PackagingTests
{
    private const string ManifestPath = "src/UBookIt.Web/Packaging/package.xml";

    private static XDocument Manifest() => XDocument.Parse(RepoFiles.Read(ManifestPath));

    [Fact]
    public void The_manifest_is_embedded_under_the_name_the_plan_resolves()
    {
        // The coupling that fails silently.
        //
        // AutomaticPackageMigrationPlan resolves its manifest as an embedded resource
        // named for the plan type's namespace. Move the class, move the file, rename
        // the namespace, or drop the <EmbeddedResource> entry, and the plan finds
        // nothing — no exception, no log line, no schema. This is the whole reason
        // this test exists.
        var planType = typeof(BookingPagePackageMigrationPlan);
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

        // The base-class directive Umbraco templates require, and the delegation.
        Assert.Equal(2, lines.Count);
        Assert.StartsWith("@inherits ", lines[0], StringComparison.Ordinal);
        Assert.Contains("Component.InvokeAsync", lines[1], StringComparison.Ordinal);

        // No element markup at all — the check that actually bites, since a wrapper
        // would pass a line count on its own.
        Assert.DoesNotContain("<", design, StringComparison.Ordinal);

        // No layout imposed on the consumer: unset means the site's own _ViewStart
        // applies and the page wears the site's chrome.
        Assert.DoesNotContain("Layout", design, StringComparison.Ordinal);
    }

    [Fact]
    public void The_manifest_installs_no_content()
    {
        // "Installs a document type, never a document" is checked rather than
        // intended. The manifest format carries content perfectly well, so its
        // absence is a decision and this is where the decision is enforced.
        Assert.Empty(Manifest().Descendants("Documents"));
        Assert.Empty(Manifest().Descendants("DocumentSet"));
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
