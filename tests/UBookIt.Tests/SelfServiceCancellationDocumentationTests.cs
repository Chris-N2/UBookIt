using UBookIt.Backoffice.Settings;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// What a site is told before turning self-service cancellation on, and what the template contract
/// publishes (`self-service-cancellation`; `booking-emails`; `email-templates`).
/// </summary>
/// <remarks>
/// <b>Written after QA observed that every documentation SHALL this change added was unguarded
/// while all its siblings were guarded.</b> A spec requirement discharged only by prose is
/// discharged by nothing: somebody tidies a page, the suite stays green, and the site owner is no
/// longer told the thing the requirement exists to tell them.
/// <para>
/// Each claim below fails differently if it goes missing, which is why they are asserted
/// individually rather than by counting words on a page.
/// </para>
/// </remarks>
public class SelfServiceCancellationDocumentationTests
{
    private static string Configuration() => RepoFiles.Read("docs/configuration.md");

    private static string Notifications() => RepoFiles.Read("docs/notifications.md");

    [Fact]
    public void The_setting_is_documented_with_its_key_and_its_tier()
    {
        // Without the key nobody can turn it on; without "read-only" an operator looks for it on
        // the settings screen and concludes the package is broken.
        var docs = Configuration();

        Assert.Contains(SettingCatalogue.SelfServiceCancellationEnabledKey, docs, StringComparison.Ordinal);
        DocumentationAssert.Says(docs, "Exposure, and restart-bound");
    }

    [Fact]
    public void The_dependency_on_booker_emails_is_documented()
    {
        // The trap: a site enables it, nothing happens, and nothing explains why. The settings
        // screen says so too, but a developer configuring appsettings never sees that screen.
        DocumentationAssert.Says(
            Configuration(), "It needs `UBookIt:Notifications:SendBookerEmails` to be on");
    }

    [Fact]
    public void The_three_consequences_of_turning_it_on_are_documented()
    {
        var docs = Configuration();

        // Each arrives as an incident rather than a question if it is missing: the first as a
        // support call about a customer who cancelled somebody else's booking, the second as an
        // argument about a no-show, the third as a customer holding a link that stopped working.
        DocumentationAssert.Says(docs, "The link is the credential");
        DocumentationAssert.Says(docs, "A booker cannot cancel a booking that has already started");
        DocumentationAssert.Says(docs, "Turning it off later strands anyone still holding a link");
    }

    [Fact]
    public void What_the_package_cannot_keep_out_of_a_log_is_documented()
    {
        // THE BOUNDARY THE PACKAGE CANNOT CLOSE FROM INSIDE, and the one QA found missing. The
        // secret is stored only as a hash — a migration was spent on that — and then travels in a
        // URL path, which every web server and proxy writes down verbatim and keeps. A site that
        // is not told this cannot make an informed decision about log retention, and the omission
        // arrives as an answer given in good faith to a data subject that turns out to be untrue.
        var docs = Configuration();

        DocumentationAssert.Says(docs, "The link's secret is in the URL, so it reaches your web server's access logs");
        DocumentationAssert.Says(docs, "Anyone holding that line can cancel that booking");
    }

    [Fact]
    public void The_pages_being_unstyleable_and_unthemable_is_documented()
    {
        // Recorded as a removal rather than left silent: a site owner who finds an unstyled page
        // mid-flow needs to know it is a decision, not a fault, and that no stylesheet of theirs
        // was ignored.
        DocumentationAssert.Says(
            Configuration(), "The cancellation pages cannot be styled or themed");
    }

    [Fact]
    public void The_template_contract_publishes_the_cancellation_url()
    {
        // `email-templates` makes the booker model public API. A site supplying its own view needs
        // the member's name, what its absence means, and what happens if they omit it — the last
        // being the narrowing this package applies to everything a supplied view says.
        var docs = Notifications();

        Assert.Contains("CancellationUrl", docs, StringComparison.Ordinal);
        DocumentationAssert.Says(docs, "there is no self-service cancellation for this booking");
        DocumentationAssert.Says(docs, "If you supply your own view and omit it, your bookers have no self-service route");
    }
}
