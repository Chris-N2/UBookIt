using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The claims the delivery API's documentation must keep making. Wrap-safe
/// (<see cref="DocumentationAssert"/>), and guarding guarantees rather than wording:
/// each of these is a statement a site owner acts on, and losing any of them to a
/// rewrite would ship a promise gap rather than a style change.
/// </summary>
public class DeliveryApiDocumentationTests
{
    private static string Readme() => RepoFiles.Read("README.md");

    private static string DocsPage() => RepoFiles.Read("docs/delivery-api.md");

    /// <summary>
    /// The default is the feature (Chris's prompting concern: an API a site owner never
    /// knew they had). Both documents must state it — the README is where the concern
    /// arrives, the docs page is where the decision is acted on.
    /// </summary>
    [Fact]
    public void Both_documents_state_the_api_is_off_by_default()
    {
        DocumentationAssert.Says(Readme(), "The API is off by default");
        DocumentationAssert.Says(DocsPage(), "It is off by default");
        DocumentationAssert.Says(
            DocsPage(),
            "A fresh install — and an upgrade that changes no configuration — serves none of these endpoints");
    }

    /// <summary>
    /// The boundary the package must never blur: it cannot protect against volume, and
    /// saying so is the protection against the claim drifting in later. "Your API lets
    /// somebody break my site" is answered by this sentence existing, not by code.
    /// </summary>
    [Fact]
    public void The_no_ddos_claim_boundary_is_stated()
    {
        DocumentationAssert.Says(Readme(), "uBookIt makes no DDoS-protection claim");
        DocumentationAssert.Says(DocsPage(), "no DDoS-protection claim of any kind");
        DocumentationAssert.Says(
            DocsPage(),
            "Rate limiting, bot filtering and denial-of-service protection belong to the infrastructure in front of your application");
    }

    /// <summary>
    /// The impossibility, stated rather than discovered: an anonymous API cannot know
    /// who is calling, and the mechanisms that sound like they would help do not. If
    /// this goes, the next reader asks for the Origin check the page exists to refuse.
    /// </summary>
    [Fact]
    public void The_origin_validation_impossibility_is_stated()
    {
        DocumentationAssert.Says(DocsPage(), "the API cannot know who is calling");
        DocumentationAssert.Says(DocsPage(), "written by the caller");
        DocumentationAssert.Says(DocsPage(), "CORS is a browser policy");
    }

    /// <summary>
    /// Found during apply: Umbraco ships its own content Delivery API under an
    /// almost-identical setting name (Umbraco:CMS:DeliveryApi:Enabled — the TestSite
    /// itself sets it). A site owner flipping the wrong one would believe they had
    /// turned uBookIt's API off. The docs must keep the two apart explicitly.
    /// </summary>
    [Fact]
    public void The_umbraco_delivery_api_collision_is_disambiguated()
    {
        DocumentationAssert.Says(DocsPage(), "Not the same thing as Umbraco's Delivery API");
        DocumentationAssert.Says(DocsPage(), "neither switches the other");
    }

    /// <summary>The flip is breaking for existing consumers, and the docs must say so where the fix is.</summary>
    [Fact]
    public void The_breaking_default_flip_is_called_out()
    {
        DocumentationAssert.Says(
            DocsPage(), "it is a breaking change for existing API consumers");
    }

    /// <summary>
    /// Absence over refusal is a guarantee with a reason, and the reason is the part a
    /// maintainer needs before "improving" the 404 into a helpful message.
    /// </summary>
    [Fact]
    public void Absence_over_refusal_is_stated_with_its_reason()
    {
        DocumentationAssert.Says(DocsPage(), "A disabled endpoint does not exist");
        DocumentationAssert.Says(
            DocsPage(),
            "those tell an anonymous stranger that the package is installed and that the endpoint exists to be switched on");
    }

    /// <summary>
    /// The shipped page's independence: a site owner deciding on the switches must be
    /// told the booking page does not care.
    /// </summary>
    [Fact]
    public void The_shipped_page_is_stated_independent()
        => DocumentationAssert.Says(
            DocsPage(),
            "leaving both settings off costs it nothing, and turning them on changes nothing about it");
}
