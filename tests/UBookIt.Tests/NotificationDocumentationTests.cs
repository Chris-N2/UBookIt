using UBookIt.Persistence.Notifications;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// What the notification documentation promises, tied to what the package raises.
/// </summary>
/// <remarks>
/// <para>
/// A hook nobody is told about is not an extension point, and this documentation is the only
/// way a site learns these exist. The type names are asserted against the real types so the
/// page cannot send someone to subscribe to something that has been renamed.
/// </para>
/// <para>
/// Several sentences are load-bearing rather than informative, and each is pinned: what the
/// package sends and under what configuration, that a mail server alone enables nothing, that
/// internal messages withhold contact details, and that a handler which throws is a notification
/// nobody receives. Each is the kind of thing discovered the expensive way — by a customer
/// arriving for a booking that was cancelled, by a customer receiving a message the site did not
/// know it sent, or during an incident.
/// </para>
/// <para>
/// <b>The claim these guard changed shape in 0.5.0 and the guard had to change with it.</b> It
/// used to pin "uBookIt sends nothing itself", which was true of every install. Now the honest
/// claim is conditional, so pinning the old sentence would have held the documentation to
/// something the package no longer does — and pinning nothing would have let the page go quiet
/// about a behaviour that writes to a site's customers.
/// </para>
/// </remarks>
public class NotificationDocumentationTests
{
    private static string Docs() => RepoFiles.Read("docs/notifications.md");

    [Fact]
    public void The_documented_notification_types_are_the_ones_the_package_raises()
    {
        var docs = Docs();

        Assert.Contains(nameof(BookingPlacedNotification), docs, StringComparison.Ordinal);
        Assert.Contains(nameof(BookingCancelledNotification), docs, StringComparison.Ordinal);

        // And the namespace, because a subscriber needs the using directive as much as the
        // name.
        Assert.Contains(typeof(BookingPlacedNotification).Namespace!, docs, StringComparison.Ordinal);
    }

    [Fact]
    public void What_the_package_sends_is_stated_rather_than_implied()
    {
        // BOTH directions of the assumption have to be closed. An operator who believes the
        // package emails the customer finds out when somebody arrives for a booking that no
        // longer exists; a site owner who does NOT believe it finds out when a customer replies
        // to a message they did not know was going out.
        var docs = Docs();

        DocumentationAssert.Says(docs, "Out of the box: nothing");
        DocumentationAssert.Says(docs, "the customer will turn up");

        // The settings that turn each direction on, by the names a site actually types.
        Assert.Contains("SendBookerEmails", docs, StringComparison.Ordinal);
        Assert.Contains("InternalRecipients", docs, StringComparison.Ordinal);
    }

    [Fact]
    public void That_a_mail_server_alone_enables_nothing_is_stated()
    {
        // The assumption this closes is the one most likely to be made, because on most sites
        // SMTP is already configured for password resets and invites long before uBookIt is
        // installed. Upgrading must not read as consent to write to that site's customers.
        DocumentationAssert.Says(
            Docs(), "Configuring your site's mail server does not change that");
    }

    [Fact]
    public void What_internal_messages_withhold_is_stated()
    {
        // Otherwise a site adds an address to InternalRecipients expecting to be able to reply
        // to the customer from it, finds it cannot, and works around the package. Saying what
        // the message withholds AND why is what makes the design followable rather than annoying.
        var docs = Docs();

        // Pinned as a single-line fragment: the sentence wraps in the source, and an
        // assertion carrying the line break would be pinning the layout as much as the claim.
        DocumentationAssert.Says(docs, "deliberately carry no booker name");
        Assert.Contains("Sensitive data", docs, StringComparison.Ordinal);
    }

    /// <summary>
    /// What the package does and does not guarantee about personal data in logs.
    /// </summary>
    /// <remarks>
    /// <b>Pinned because its predecessor was pinned by nothing, and that is how it survived.</b>
    /// The erasure section used to assert flatly that "nothing logs them" — an absolute claim
    /// about the whole package, true when written and falsified the moment the package could hand
    /// a booker's address to a mail client. This change's own outward sweep read this file, caught
    /// the "sends nothing" claim at the top, and walked past its sibling four sections down.
    /// <para>
    /// Both halves are asserted together on purpose: the guarantee uBookIt CAN keep (it writes no
    /// contact details itself) and the one it cannot (a mail server's error may quote the address).
    /// Keeping only the first would restore exactly the over-claim this replaced.
    /// </para>
    /// </remarks>
    [Fact]
    public void What_can_and_cannot_be_kept_out_of_logs_is_stated()
    {
        var docs = Docs();

        DocumentationAssert.Says(
            docs, "uBookIt never writes a booker's name, address or telephone number to a log itself");
        DocumentationAssert.Says(docs, "a mail server's error can quote the address back at you");
        DocumentationAssert.Says(
            docs, "treat your application log as somewhere contact details can appear");

        // AND THAT THE ABSOLUTE IS ABSENT, which is the half the first version of this guard
        // could not see. It asserted three fragments were PRESENT, so it stayed green while a
        // sibling sentence four sections above went on promising the opposite — the over-claim
        // survived by ADDITION, and only deletion of the correction would have failed.
        //
        // A guard for a claim that must not be made has to look for the claim, not only for its
        // replacement. PrivacyNoticeTests.A_site_that_sends_nothing_promises_nothing pairs its
        // positive assertions with a DoesNotContain for exactly this reason.
        // Through DocumentationAssert, NOT Assert.DoesNotContain. A raw substring check here
        // survived re-adding the forbidden sentence WRAPPED — which is the only form it could take
        // if reintroduced at its original location, since this repository wraps prose at about 100
        // columns. The positive assertions above were already wrap-safe; having one matcher for
        // each direction in the same test is what let the negative half rot unnoticed.
        foreach (var overclaim in new[]
        {
            "nothing logs them",
            "The log records the id and nothing else about the booker",
            "No booker name, address or telephone number is ever logged",
        })
        {
            DocumentationAssert.DoesNotSay(docs, overclaim);
        }
    }

    /// <summary>
    /// The same absolute, in the XML documentation that ships with the assembly. A site author
    /// reading IntelliSense meets this before they meet the markdown.
    /// </summary>
    [Fact]
    public void The_handlers_own_documentation_does_not_over_claim()
    {
        var source = RepoFiles.Read("src/UBookIt.Persistence/Notifications/BookingEmailHandler.cs");

        // Same matcher as the markdown guards. XML documentation wraps across `///` prefixes,
        // which a raw substring check cannot see through — QA re-added the absolute immediately
        // below its own correction, wrapped, and this test passed 10/10.
        //
        // And note the shape of the regression that gets through: a real one ADDS rather than
        // REPLACES. Replacing the corrected wording fails on the positive assertion below, which
        // is why an earlier attempt looked like a pass while the absence check never fired at all.
        DocumentationAssert.DoesNotSay(
            source, "No booker name, address or telephone number is ever logged");
        DocumentationAssert.Says(source, "never writes a booker name");
        DocumentationAssert.Says(source, "cannot promise");
    }

    [Fact]
    public void The_delivery_limit_is_stated()
    {
        // "You will be told, unless your handler throws" is the kind of half-promise that
        // gets relied on and then discovered during an incident.
        var docs = Docs();

        DocumentationAssert.Says(docs, "A handler that throws is a notification nobody receives");
        DocumentationAssert.Says(docs, "There is no retry and no queue");
    }

    [Fact]
    public void The_guarantee_that_a_handler_cannot_break_a_booking_is_stated()
    {
        DocumentationAssert.Says(Docs(), "Your handler cannot break a booking");
    }

    [Fact]
    public void The_documentation_shows_how_to_subscribe()
    {
        // A name and a description are not enough to act on; the composer registration is the
        // part a site author actually has to get right.
        var docs = Docs();

        Assert.Contains("AddNotificationAsyncHandler", docs, StringComparison.Ordinal);
        Assert.Contains("INotificationAsyncHandler", docs, StringComparison.Ordinal);
    }

    [Fact]
    public void The_backoffice_documentation_says_what_cancelling_tells_the_booker()
    {
        // Said where the consequence lands, not only on the notifications page. An operator
        // reading about cancelling is the person who needs to know.
        var docs = RepoFiles.Read("docs/backoffice.md");

        DocumentationAssert.Says(docs, "Cancelling tells nobody unless you have configured it to");
        Assert.Contains("notifications.md", docs, StringComparison.Ordinal);
    }
}
