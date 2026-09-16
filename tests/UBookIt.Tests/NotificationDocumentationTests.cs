using UBookIt.Core.Notifications;
using UBookIt.Persistence.Notifications;
using UBookIt.Tests.Support;
using UBookIt.Web.Emails;

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
        Assert.Contains(nameof(BookingConfirmedNotification), docs, StringComparison.Ordinal);
        Assert.Contains(nameof(BookingDeclinedNotification), docs, StringComparison.Ordinal);
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

    // ---- approval-decline ----------------------------------------------------------------

    [Fact]
    public void The_approval_flow_is_documented_where_the_operator_reads()
    {
        var docs = RepoFiles.Read("docs/backoffice.md");

        // The consequence of doing nothing, stated rather than implied: no expiry is a
        // decision this change made, and an operator running approval needs to know the
        // package will not chase them.
        DocumentationAssert.Says(docs, "A requested booking waits for you, indefinitely");

        // And the notification conditional for the two new verbs, on cancel's terms.
        DocumentationAssert.Says(
            docs,
            "Confirming or declining tells the person who booked only if booking emails are configured");
    }

    [Fact]
    public void The_notifications_documentation_states_the_approval_behaviour()
    {
        var docs = Docs();

        DocumentationAssert.Says(docs, "AutoConfirm");
        DocumentationAssert.Says(docs, "awaits approval");

        // Confirm/decline are booker-only, and the docs must say so where the recipient list
        // is configured — an internal recipient wondering why they heard nothing is the
        // predictable reader.
        DocumentationAssert.Says(docs, "Confirming or declining sends this list nothing");

        // A booking placed under auto-confirm raises placement only — the double-message
        // question every subscriber will ask.
        DocumentationAssert.Says(
            docs, "A booking placed under auto-confirm raises `BookingPlacedNotification` and nothing else");
    }

    // ---- email templates -------------------------------------------------------------------

    /// <summary>
    /// An author must be able to supply content from the documentation alone — the alternative to
    /// finding this is worse than going without, because a site that wants different wording and
    /// cannot find it will take over sending instead, and thereby inherit the sending conditions,
    /// the erased-booker rule and the contact-detail rules that this package is tested for and
    /// their handler will not be.
    /// </summary>
    [Fact]
    public void An_author_can_supply_content_from_the_documentation_alone()
    {
        var docs = Docs();

        // The path, and every message name — asserted against the enum, so a name added or
        // renamed in code fails here rather than leaving the documentation quietly incomplete.
        DocumentationAssert.Says(docs, RazorBookingTemplateRenderer.TemplateFolder.TrimStart('~', '/'));

        foreach (var kind in Enum.GetValues<BookingMessageKind>())
        {
            Assert.Contains($"{kind}.cshtml", docs, StringComparison.Ordinal);
        }

        // How to state the two things a template may state, and the base page it needs.
        //
        // Asserted as the ASSIGNMENTS an author writes rather than as the bare words: "Subject"
        // and "IsHtml" both occur in unrelated prose on this page, so checking for them alone
        // was very nearly vacuous — it would have passed against a document that never showed
        // how to set either.
        Assert.Contains(nameof(UBookItEmailPage<BookingMessageModel>), docs, StringComparison.Ordinal);
        Assert.Contains("Subject = ", docs, StringComparison.Ordinal);
        Assert.Contains("IsHtml = true", docs, StringComparison.Ordinal);
        Assert.Contains("@inherits", docs, StringComparison.Ordinal);
    }

    [Fact]
    public void The_single_body_limit_is_stated_with_its_reason()
    {
        var docs = Docs();

        // Wrap-safe through DocumentationAssert, so the fragment is written on one line here
        // regardless of how the sentence wraps in the document.
        DocumentationAssert.Says(docs, "There is no plain-text part alongside it");
        // And WHY, so it does not read as an arbitrary restriction somebody could ask us to lift.
        DocumentationAssert.Says(docs, "limit of Umbraco's mail abstraction rather than a choice");
    }

    [Fact]
    public void What_becomes_the_authors_and_what_does_not_is_stated()
    {
        var docs = Docs();

        DocumentationAssert.Says(docs, "The words become yours, including whether they are accurate");

        // The four that do NOT narrow. An author who assumed any of these had become theirs
        // would be reproducing a rule the package still enforces — or worse, assuming it had
        // stopped applying.
        DocumentationAssert.Says(docs, "Sending still needs both a uBookIt setting");
        DocumentationAssert.Says(docs, "is still never written to");
        DocumentationAssert.Says(docs, "still carries no booker contact details");
    }

    /// <summary>
    /// The documented example must not present itself as the package's own wording.
    /// </summary>
    /// <remarks>
    /// The change deliberately ships no example template, because one that reproduced the
    /// default would be a second copy of three pieces of composer logic, free to drift. The
    /// documentation's example is a site's own wording for the same reason — there is nothing
    /// for it to drift from. This guards that framing, since an example quietly rewritten to
    /// "here is what uBookIt sends" would reintroduce the duplicate in prose.
    /// </remarks>
    [Fact]
    public void The_documented_example_does_not_claim_to_be_what_the_package_sends()
        => DocumentationAssert.Says(Docs(), "This example is not what uBookIt sends");

    /// <summary>
    /// No document tells an author that content they supply is still subject to the package's
    /// wording guarantees.
    /// </summary>
    /// <remarks>
    /// <b>The absence half, which the first version of these guards simply did not have.</b>
    /// Every other check here is a presence check, and this file's own remarks already record
    /// why that is not enough: an over-claim survives by ADDITION, so only a
    /// <c>DoesNotSay</c> can see one. The specific over-claim to fear is a sentence reassuring
    /// an author that uBookIt still ensures their wording is accurate — which is exactly what
    /// the `booking-emails` narrowing says it does not, and exactly the comforting thing
    /// somebody would add to documentation about a feature that hands words over.
    /// <para>
    /// Swept across every shipped document rather than `notifications.md` alone, because the
    /// reassurance is likelier to be written where templates are being sold than where they are
    /// specified.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_document_claims_supplied_content_is_still_subject_to_the_packages_wording_guarantees()
    {
        foreach (var doc in ShippedMarkdown())
        {
            var text = RepoFiles.Read(doc);

            // FIRST AND SECOND PERSON, uBookIt-named.
            DocumentationAssert.DoesNotSay(text, "uBookIt still checks");
            DocumentationAssert.DoesNotSay(text, "uBookIt will still ensure");
            DocumentationAssert.DoesNotSay(text, "your wording is still checked");
            DocumentationAssert.DoesNotSay(
                text, "supplied content still describes the booking's state correctly");
            DocumentationAssert.DoesNotSay(text, "your template will derive");

            // THIRD PERSON, which the first version missed entirely — the same reassurance
            // written about "the package" rather than to "you" passed every needle above.
            DocumentationAssert.DoesNotSay(text, "the package still validates");
            DocumentationAssert.DoesNotSay(text, "the package still checks");
            DocumentationAssert.DoesNotSay(text, "the subject is still derived");

            // AND THE INTERNAL OVER-CLAIM, which round 2's own new requirements made narrowable
            // and which nothing was watching. A document telling an author that a supplied
            // internal message still carries the reference, or still links to the backoffice,
            // now contradicts the spec — those became the site's to include or omit.
            DocumentationAssert.DoesNotSay(text, "still carries the reference");
            DocumentationAssert.DoesNotSay(text, "still links to the backoffice");
            DocumentationAssert.DoesNotSay(text, "will still identify the booking");
        }
    }

    /// <summary>
    /// Every markdown file the repository ships, discovered rather than listed.
    /// </summary>
    /// <remarks>
    /// <b>The enumerated list was the sample, and QA found the population.</b> The first version
    /// of the sweep below named four files under <c>docs/</c>. <c>README.md</c> was in no guard
    /// in this repository at all — and it is the file <c>Directory.Build.props</c> packs into
    /// every NuGet package, so its "placement auto-confirms" and "nothing is sent by the
    /// package" outlived two changes that falsified them and would have shipped to every
    /// consumer. Discovery closes the class: a markdown file added later is swept without
    /// anybody remembering to add it.
    /// <para>
    /// <b>Scoped to what a consumer can actually read</b>, on one stated principle rather than a
    /// list of exclusions. <c>openspec/</c> is out because archived changes are a historical
    /// record and are SUPPOSED to contain sentences that were true when written; live specs are
    /// covered by their own capability guards. <c>CLAUDE.md</c> and <c>.claude/</c> are out for
    /// the same reason and a sharper one: they are agent tooling that ships nowhere, and
    /// CLAUDE.md is this project's record of retired wordings, which it QUOTES on purpose.
    /// Sweeping it would fail this guard for a false reason the first time a lesson was written
    /// down — and a guard that cries wolf gets weakened rather than fixed.
    /// </para>
    /// </remarks>
    private static IEnumerable<string> ShippedMarkdown()
    {
        var root = RepoFiles.Root;

        // The roots a CONSUMER can read: the package readme, the documentation set, and any
        // per-project readme that goes into a .nupkg. Enumerated as roots rather than as a
        // whole-tree walk with exclusions, which is what the first version did — 973 files
        // visited to keep 23, through `ref/` (two full Umbraco checkouts) and `node_modules`,
        // the two places likeliest to hold a path this cannot open or one too long to walk.
        // SearchOption.AllDirectories implies IgnoreInaccessible = false, so either would have
        // failed the test for a reason with nothing to do with documentation.
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };

        IEnumerable<string> Under(string relative, string pattern = "*.md")
        {
            var directory = Path.Combine(root, relative);

            return Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, pattern, options)
                : [];
        }

        return Directory
            .EnumerateFiles(root, "README.md", new EnumerationOptions { IgnoreInaccessible = true })
            .Concat(Under("docs"))
            .Concat(Under("src", "README.md"))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'))
            .Distinct(StringComparer.Ordinal);
    }

    [Fact]
    public void The_sweep_reads_the_files_it_claims_to()
    {
        // Anti-vacuity, and specifically for the file whose absence was the finding: a glob
        // that silently matched nothing would make the sweep below pass over an empty set.
        var swept = ShippedMarkdown().ToList();

        Assert.Contains("README.md", swept);
        Assert.Contains("docs/notifications.md", swept);
        Assert.Contains("docs/backoffice.md", swept);
        Assert.Contains("docs/mvp.md", swept);
        Assert.True(swept.Count >= 5, $"Only {swept.Count} markdown files found; the sweep is not reading the repository.");
    }

    /// <summary>
    /// The claims this change falsified must not survive as claims. An over-claim survives by
    /// ADDITION — only deleting the correction fails a positive assertion — so each needle
    /// here is the false sentence itself, matched wrap-safely.
    /// </summary>
    /// <remarks>
    /// The needles are chosen to miss the corrections: "tells nobody unless you have
    /// configured it to" is the truthful conditional and must stay, so the needle for the
    /// false form is the unconditional phrasing that nothing correct contains.
    /// <c>docs/mvp.md</c> is swept too — its historical account was deliberately worded to
    /// paraphrase rather than quote the retired sentence, precisely so this guard could
    /// cover it without an exemption.
    /// </remarks>
    [Fact]
    public void The_claims_this_change_falsified_are_not_made_anywhere_in_the_docs()
    {
        foreach (var doc in ShippedMarkdown())
        {
            var text = RepoFiles.Read(doc);

            DocumentationAssert.DoesNotSay(text, "No v1 pathway produces those statuses");
            DocumentationAssert.DoesNotSay(text, "no pathway produces them");
            DocumentationAssert.DoesNotSay(text, "notifies nobody by itself");
            DocumentationAssert.DoesNotSay(text, "does not tell the person who booked");
            DocumentationAssert.DoesNotSay(text, "It does not approve or decline");

            // Falsified by 0.5.0, not by this change — found by QA in README, which no guard
            // read. Swept here rather than left for the next change to trip over: the class is
            // "an unconditional claim that the package sends nothing", and 0.5.0's own sweep
            // demonstrably could not enumerate it.
            DocumentationAssert.DoesNotSay(text, "Nothing is sent by the package");
            DocumentationAssert.DoesNotSay(text, "placement auto-confirms");

            // Falsified by 0.3.0's find-by-booker, same class, same reason it survived: README
            // was in no guard.
            DocumentationAssert.DoesNotSay(text, "there is no search by name, email or reference");

            // Falsified by move-booking. This sentence stood in EIGHT places, on the record as
            // a decision; the decision was reversed, and a sentence that survives in one of
            // them tells an operator the screen cannot do what it can. docs/mvp.md's
            // historical account paraphrases rather than quotes it, so this can cover it.
            DocumentationAssert.DoesNotSay(text, "the shape of that operation is a cancellation and a new booking");
            DocumentationAssert.DoesNotSay(text, "There is no reschedule");
            DocumentationAssert.DoesNotSay(text, "Amending a booking's time. There is no such operation");
        }
    }

    /// <summary>
    /// The booking form must not promise a confirmation it may not send — the CRITICAL this
    /// change shipped into QA.
    /// </summary>
    /// <remarks>
    /// The rendered surfaces are guarded in <c>PrivacyNoticeTests</c>, wrap-safely and in both
    /// directions. This is the DOCUMENTATION half of the same claim: a doc telling a site owner
    /// that visitors are promised a confirmation would be the identical over-claim, one file
    /// further out, and nothing else here would see it.
    /// </remarks>
    [Fact]
    public void No_document_promises_the_booker_a_confirmation_the_package_may_not_send()
    {
        foreach (var doc in ShippedMarkdown())
        {
            var text = RepoFiles.Read(doc);

            DocumentationAssert.DoesNotSay(text, "send your booking confirmation");
            DocumentationAssert.DoesNotSay(text, "we will send a confirmation");
        }
    }
}
