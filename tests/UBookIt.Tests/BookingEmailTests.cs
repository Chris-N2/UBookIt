using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using UBookIt.Core;
using UBookIt.Core.Bookings;
using UBookIt.Core.Notifications;
using UBookIt.Core.Resources;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Notifications;
using UBookIt.Tests.Support;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace UBookIt.Tests;

/// <summary>
/// What the package sends, to whom, and — mostly — what it refuses to send.
/// </summary>
public class BookingEmailTests
{
    private static readonly Guid ResourceId = Guid.NewGuid();

    // ---- composing --------------------------------------------------------------------------

    [Fact]
    public async Task A_service_booking_is_described_by_its_snapshot_name()
    {
        var message = await Composer().ForBookerAsync(Booking(), BookingEvent.Placed);

        Assert.Contains("Initial Consultation", message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_direct_booking_is_described_by_the_resource_it_claims()
    {
        var message = await Composer().ForBookerAsync(Booking(direct: true), BookingEvent.Placed);

        Assert.Contains("Treatment Room", message.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// The reference and the time are the parts a person cannot reconstruct for themselves.
    /// Withholding them because a name could not be read would be the wrong trade.
    /// </summary>
    [Fact]
    public async Task An_unresolvable_subject_still_produces_a_message()
    {
        var booking = Booking(direct: true);
        var message = await Composer(resourceExists: false).ForBookerAsync(booking, BookingEvent.Placed);

        Assert.Contains(booking.Reference.Display, message.Body, StringComparison.Ordinal);
        Assert.Contains("15 September 2026", message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("What:", message.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// The zone travels on the booking, so a site that changes its own does not retrospectively
    /// restate when existing bookings are. The fixture's interval is London wall-clock 09:00 on a
    /// BST date, which is 08:00Z — so the same instant reads 9:00 in London and 8:00 in UTC, and
    /// the pair below is a differential rather than two separate assertions.
    /// </summary>
    [Fact]
    public async Task The_time_is_expressed_in_the_bookings_own_zone()
    {
        var message = await Composer().ForBookerAsync(Booking(), BookingEvent.Placed);

        Assert.Contains("9:00 AM (Europe/London)", message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("8:00 AM", message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_booking_in_a_different_zone_is_not_restated_in_the_sites()
    {
        var message = await Composer().ForBookerAsync(Booking(zoneId: "UTC"), BookingEvent.Placed);

        Assert.Contains("8:00 AM (UTC)", message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("9:00 AM", message.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// D7. Written when placement could only produce <c>Confirmed</c>, so that the day approval
    /// shipped could not turn a hard-coded subject false in a customer's inbox. That day was the
    /// approval-decline change: every arm is now reachable — placement yields <c>Requested</c>
    /// under <c>AutoConfirm</c> off, and decline produces <c>Declined</c> — and nothing here had
    /// to move, which was the point.
    /// </summary>
    [Theory]
    [InlineData(BookingStatus.Confirmed, "Your booking is confirmed")]
    [InlineData(BookingStatus.Requested, "We have received your booking")]
    [InlineData(BookingStatus.Declined, "Your booking could not be accepted")]
    public async Task The_wording_follows_the_bookings_state(BookingStatus status, string expected)
    {
        var message = await Composer().ForBookerAsync(Booking(status: status), BookingEvent.Placed);

        Assert.Equal(expected, message.Subject);
    }

    [Fact]
    public async Task A_cancellation_says_so_whatever_the_status_says()
    {
        var message = await Composer()
            .ForBookerAsync(Booking(status: BookingStatus.Cancelled), BookingEvent.Cancelled);

        Assert.Equal("Your booking has been cancelled", message.Subject);
    }

    /// <summary>
    /// The guarantee that makes the internal message safe to send at all: personal data stays
    /// behind the backoffice's own control, and the message links to it instead.
    /// </summary>
    [Fact]
    public async Task The_internal_message_carries_no_booker()
    {
        var booking = Booking();
        var message = await Composer()
            .ForSiteAsync(booking, BookingEvent.Placed, new Uri("https://site.example/umbraco"));

        Assert.DoesNotContain("Ada Lovelace", message.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ada@example.com", message.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("07700", message.Body, StringComparison.Ordinal);
        Assert.Contains(booking.Reference.Display, message.Body, StringComparison.Ordinal);
        Assert.Contains("https://site.example/umbraco", message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_internal_message_sends_without_a_link()
    {
        var message = await Composer().ForSiteAsync(Booking(), BookingEvent.Placed, backofficeUrl: null);

        Assert.Contains("New booking", message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("backoffice", message.Body, StringComparison.OrdinalIgnoreCase);
    }

    // ---- sending ----------------------------------------------------------------------------

    [Fact]
    public async Task Nothing_configured_sends_nothing()
        => Assert.Empty((await Send(Notifications())).Sent);

    /// <summary>
    /// A site that has asked for nothing must not be reached by its mail configuration at all —
    /// including by a host whose probe throws, which Umbraco's own default sender does. If this
    /// fails, an unconfigured site is being made to care about SMTP.
    /// </summary>
    [Fact]
    public async Task Nothing_configured_does_not_even_ask_the_host()
    {
        var sender = new RecordingEmailSender { ThrowOnProbe = true };

        await Send(Notifications(), sender);

        Assert.False(sender.Probed);
    }

    [Fact]
    public async Task Only_internal_recipients_writes_to_nobody_else()
    {
        var sent = (await Send(Notifications(recipients: ["desk@example.com"]))).Sent;

        Assert.Equal("desk@example.com", Assert.Single(Assert.Single(sent).To));
    }

    [Fact]
    public async Task Only_booker_email_writes_to_nobody_else()
    {
        var sent = (await Send(Notifications(sendBooker: true))).Sent;

        Assert.Equal("ada@example.com", Assert.Single(Assert.Single(sent).To));
    }

    [Fact]
    public async Task Both_directions_send_both_messages()
    {
        var sent = (await Send(Notifications(true, ["desk@example.com"]))).Sent;

        Assert.Equal(2, sent.Count);
        // The site first, deliberately: a bouncing booker address must not cost the business its
        // own notification, and the booker's address is far likelier to bounce.
        Assert.Equal("desk@example.com", Assert.Single(sent[0].To));
        Assert.Equal("ada@example.com", Assert.Single(sent[1].To));
    }

    [Fact]
    public async Task A_host_that_cannot_send_sends_nothing()
        => Assert.Empty((await Send(
            Notifications(true, ["desk@example.com"]),
            new RecordingEmailSender { CanSend = false })).Sent);

    /// <summary>
    /// Reading a throwing probe as "cannot send" would convert a misconfiguration into permanent
    /// silence. It escapes instead, to the observer that keeps it away from the booker and logs it.
    /// </summary>
    [Fact]
    public async Task A_throwing_probe_is_not_read_as_a_refusal()
        => await Assert.ThrowsAsync<NotImplementedException>(() => Send(
            Notifications(true),
            new RecordingEmailSender { ThrowOnProbe = true }));

    /// <summary>
    /// Reachable rather than theoretical: the retention sweep erases a booker some period after
    /// their booking ends, and it can still be cancelled afterwards.
    /// </summary>
    [Fact]
    public async Task An_erased_booker_is_not_written_to_but_the_site_still_is()
    {
        var erased = Booking().Tap(b => b.EraseBooker(TestData.Now));
        var sent = (await Send(
            Notifications(true, ["desk@example.com"]), booking: erased, bookingEvent: BookingEvent.Cancelled)).Sent;

        var message = Assert.Single(sent);
        Assert.Equal("desk@example.com", Assert.Single(message.To));
    }

    // ---- confirmation and decline (approval-decline) ------------------------------------------

    /// <summary>
    /// Confirm and decline are told to the booker only: the site's own people — or a colleague —
    /// performed the action, and the bookings screen is where its state lives. Both directions are
    /// enabled here precisely so that an internal message, if one were wrongly sent, had every
    /// opportunity to appear.
    /// </summary>
    [Theory]
    [InlineData(BookingEvent.Confirmed, BookingStatus.Confirmed, "Your booking is confirmed")]
    [InlineData(BookingEvent.Declined, BookingStatus.Declined, "Your booking could not be accepted")]
    public async Task Confirm_and_decline_write_to_the_booker_only(
        BookingEvent bookingEvent, BookingStatus status, string subject)
    {
        var sent = (await Send(
            Notifications(true, ["desk@example.com"]),
            booking: Booking(status: status),
            bookingEvent: bookingEvent)).Sent;

        var message = Assert.Single(sent);
        Assert.Equal("ada@example.com", Assert.Single(message.To));
        Assert.Equal(subject, message.Subject);
    }

    [Theory]
    [InlineData(BookingEvent.Confirmed, BookingStatus.Confirmed)]
    [InlineData(BookingEvent.Declined, BookingStatus.Declined)]
    public async Task Confirm_and_decline_on_a_site_without_booker_emails_send_nothing_at_all(
        BookingEvent bookingEvent, BookingStatus status)
    {
        // Recipients ARE configured — the direction that must stay silent is the one that is on.
        var sent = (await Send(
            Notifications(sendBooker: false, recipients: ["desk@example.com"]),
            booking: Booking(status: status),
            bookingEvent: bookingEvent)).Sent;

        Assert.Empty(sent);
    }

    [Theory]
    [InlineData(BookingEvent.Confirmed, BookingStatus.Confirmed)]
    [InlineData(BookingEvent.Declined, BookingStatus.Declined)]
    public async Task Confirming_or_declining_an_erased_booker_sends_nothing_to_anyone(
        BookingEvent bookingEvent, BookingStatus status)
    {
        // Reachable: retention erases a booker after the booking's END, and a Requested booking
        // can outlive its end untouched, then be resolved. No address, and no internal message
        // is due for these events — so nothing at all.
        var erased = Booking(status: status).Tap(b => b.EraseBooker(TestData.Now));

        var sent = (await Send(
            Notifications(true, ["desk@example.com"]), booking: erased, bookingEvent: bookingEvent)).Sent;

        Assert.Empty(sent);
    }

    [Fact]
    public async Task An_auto_confirmed_placement_is_one_booker_message_not_two()
    {
        // Auto-confirmation is not an event; it is what placement produced, and the placement
        // message already says so. A second "confirmed" mail would train customers to skim.
        var sent = (await Send(Notifications(sendBooker: true))).Sent;

        var message = Assert.Single(sent);
        Assert.Equal("Your booking is confirmed", message.Subject);
    }

    [Fact]
    public async Task A_received_booking_promises_the_next_message()
    {
        var message = await Composer().ForBookerAsync(Booking(status: BookingStatus.Requested), BookingEvent.Placed);

        Assert.Contains("confirms or declines", message.Body, StringComparison.Ordinal);
        // And nothing in the message claims the booking IS confirmed — the subject says
        // received, and the body must not contradict it. The needle is the claim ("is
        // confirmed"), which the pending wording is written to avoid entirely — "not
        // confirmed yet" and "confirms or declines" both stay out of its way, so any hit
        // here is a genuine contradiction rather than a phrasing accident.
        Assert.DoesNotContain("is confirmed", message.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_internal_message_flags_a_booking_awaiting_approval()
    {
        var booking = Booking(status: BookingStatus.Requested);
        var message = await Composer()
            .ForSiteAsync(booking, BookingEvent.Placed, new Uri("https://site.example/umbraco"));

        Assert.Contains("awaiting approval", message.Subject, StringComparison.Ordinal);
        Assert.Contains("awaits approval", message.Body, StringComparison.Ordinal);
        // The link rides with the flag: the message says act, and where.
        Assert.Contains("https://site.example/umbraco", message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_confirmed_placement_does_not_claim_an_approval_is_awaited()
    {
        var message = await Composer()
            .ForSiteAsync(Booking(), BookingEvent.Placed, new Uri("https://site.example/umbraco"));

        // The broad needle, deliberately: any wording about approval on a confirmed placement
        // is wrong, not just the exact sentence the requested branch emits today.
        Assert.DoesNotContain("approval", message.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("approval", message.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_awaiting_approval_message_still_carries_no_booker()
    {
        var booking = Booking(status: BookingStatus.Requested);
        var message = await Composer()
            .ForSiteAsync(booking, BookingEvent.Placed, new Uri("https://site.example/umbraco"));

        // GUIDs out of the haystack before names are looked for in it — "Ada" is three hex
        // digits, and the reference display is alphanumeric too. Same fix, same reason, as the
        // log-line guard below.
        var haystack = GuidRedaction.WithoutGuids(message.Subject + "\n" + message.Body);

        Assert.DoesNotContain("Ada", haystack.Replace(booking.Reference.Display, "{ref}"), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Lovelace", haystack, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ada@example.com", haystack, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("07700", haystack, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Messages_are_plain_text_with_no_sender_of_our_own()
    {
        var message = Assert.Single((await Send(Notifications(sendBooker: true))).Sent);

        Assert.False(message.IsBodyHtml);
        Assert.Null(message.From);
    }

    /// <summary>
    /// A message declared as HTML actually leaves as HTML.
    /// </summary>
    /// <remarks>
    /// <b>The last hop, and it had no test.</b> Everything upstream — the base page's typed
    /// property, the renderer reading it back, the composer carrying it on
    /// <c>BookingMessage</c> — was covered, and then <c>isBodyHtml:</c> was handed to
    /// <c>EmailMessage</c>. QA replaced that argument with a literal <c>false</c> and all 2379
    /// tests passed: the entire HTML feature could be switched off at its final line, invisibly.
    /// The pre-existing assertion above passes under that mutation too, which is why this is a
    /// second test rather than another line in it — <b>a guard that holds one value can only
    /// see one direction.</b>
    /// </remarks>
    [Fact]
    public async Task A_message_declared_as_html_is_sent_as_html()
    {
        var sender = new RecordingEmailSender();

        var handler = new BookingEmailHandler(
            new SiteBookingSettings
            {
                TimeZoneId = TestData.LondonZoneId,
                Notifications = Notifications(sendBooker: true),
            },
            sender,
            Composer(renderer: new HtmlDeclaringRenderer()),
            NoResponsibility.Instance,
            new StubHostingEnvironment(),
            NullLogger<BookingEmailHandler>.Instance);

        await handler.HandleAsync(new BookingPlacedNotification(Booking()), CancellationToken.None);

        var message = Assert.Single(sender.Sent);

        Assert.True(message.IsBodyHtml);
        Assert.Equal("<p>Declared HTML.</p>", message.Body);
    }

    /// <summary>Renders one HTML body and says so.</summary>
    private sealed class HtmlDeclaringRenderer : IBookingTemplateRenderer
    {
        public Task<BookingTemplateResult> RenderAsync(
            BookingMessageKind kind, BookingMessageModel model, CancellationToken cancellationToken = default)
            => Task.FromResult(new BookingTemplateResult(
                BookingTemplateOutcome.Rendered, Body: "<p>Declared HTML.</p>", IsHtml: true));
    }

    [Fact]
    public async Task The_package_identifies_its_own_mail_and_offers_the_seam()
    {
        var sender = new RecordingEmailSender();

        await Send(Notifications(sendBooker: true), sender);

        Assert.Equal(BookingEmailHandler.EmailType, Assert.Single(sender.Types));
        Assert.True(Assert.Single(sender.Notified));
    }

    /// <summary>
    /// A failure to send is not a reason to write into a log the very details the rest of the
    /// package takes care to govern.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Built from a container, and that shape is the finding rather than a flourish.</b> The
    /// first version captured the HANDLER's logger and gave the composer a null one, while its own
    /// comment claimed it covered "every entry... so a line added later is covered by this without
    /// being noticed". A line WAS added later, in the collaborator the handler calls, and QA put a
    /// booker's name and address into it with the whole suite still green.
    /// </para>
    /// <para>
    /// Naming a second logger would have closed that instance and left the same hole one
    /// collaborator further out: the guard's extension would still be a hand-written list while its
    /// intension is "every line". So the send path is resolved from a container whose only logging
    /// provider captures everything, and every logger every participant is handed comes from it —
    /// including a participant added later, so long as it is resolved rather than newed.
    /// </para>
    /// <para>
    /// <b>Where the boundary actually falls.</b> "Resolved rather than newed" is a real
    /// precondition and nothing enforces it. The concrete instance today is
    /// <see cref="BackofficeBookingLink"/>: it is a static class called from the handler, so it can
    /// never take a logger from the container, and a diagnostic added there — the obvious one being
    /// "no application URL configured" — would be invisible here. It logs nothing today. A better
    /// guard cannot fix that; a reader knowing where the edge is can.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task No_log_line_carries_a_booker(bool sendFails, bool resourceReadFails)
    {
        var (handler, logs, _) = SendPath(
            Notifications(true, ["desk@example.com"]),
            sendFails: sendFails,
            resourceReadFails: resourceReadFails);

        // A DIRECT booking, so the resource read really happens and its failure path is live.
        var booking = Booking(direct: true);

        try
        {
            await handler.HandleAsync(new BookingPlacedNotification(booking), CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // The handler does not catch; the observer above it does. What is under test here is
            // what was written to the log on the way past, not who catches it.
        }

        Assert.NotEmpty(logs.Entries);

        foreach (var entry in logs.Entries)
        {
            // THE BOOKING ID IS REMOVED FROM THE HAYSTACK BEFORE MATCHING, and that is a
            // correctness fix rather than a convenience.
            //
            // "Ada" is three hex digits, so it occurs in a random 32-hex-digit GUID roughly 0.7%
            // of the time — and the anti-vacuity assertion below REQUIRES the booking id to be in
            // these entries. So the guard reported a PII leak in a booking id, at random, across
            // four theory rows. "07700" has the same shape.
            //
            // The weaker fix is to match "Ada Lovelace" instead; it was rejected because a log
            // line carrying only a first name is a real leak this must still catch. Redacting the
            // one token that is legitimately present keeps the needles granular AND deterministic.
            //
            // This is not hypothetical: EraseBookerEndpointTests has the same collision and has
            // been failing intermittently in this repository for some time, misdiagnosed once as a
            // build race. See the deferred obligations note.
            // EVERY GUID, not just this booking's. Redacting the one identifier present today
            // fixes today and leaves the class: the composer's warning is ABOUT a resource read,
            // so a line carrying `claim.ResourceId` is the obvious next addition — and it would
            // reintroduce the collision while looking exactly like a PII leak. Same lesson as the
            // defect this redaction exists to fix, one level up.
            var haystack = GuidRedaction.WithoutGuids(entry.Message + " " + entry.Exception);

            Assert.DoesNotContain("Ada", haystack, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Lovelace", haystack, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ada@example.com", haystack, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("07700", haystack, StringComparison.Ordinal);
        }

        // Anti-vacuity: the loop passes trivially against a log saying nothing about this booking,
        // and against a capture that wired up nothing at all.
        Assert.Contains(
            logs.Entries, e => e.Message.Contains(booking.Id.ToString(), StringComparison.Ordinal));

        // And specifically that the COMPOSER's line is observed — the one the first version of this
        // test could not see. Only reachable when the read fails.
        if (resourceReadFails)
        {
            Assert.Contains(
                logs.Entries,
                e => e.Category.Contains(nameof(BookingMessageComposer), StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// The population rather than the sample. The test above can only inspect lines that were
    /// actually written; this asserts every participant takes its logger from the captured factory
    /// in the first place, so one that logs only on a path no fixture reaches is still covered
    /// rather than silently exempt.
    /// </summary>
    [Fact]
    public void Every_logger_on_the_send_path_is_captured()
    {
        var (handler, logs, _) = SendPath(Notifications(true, ["desk@example.com"]));

        Assert.NotNull(handler);
        Assert.Contains(
            logs.Categories, c => c.Contains(nameof(BookingEmailHandler), StringComparison.Ordinal));
        Assert.Contains(
            logs.Categories, c => c.Contains(nameof(BookingMessageComposer), StringComparison.Ordinal));
    }

    /// <summary>
    /// A store that THROWS has established "what was booked cannot be established" just as surely
    /// as one that returned nothing, so the requirement's answer is the same: send the reference
    /// and the time without the name.
    /// </summary>
    /// <remarks>
    /// Found by QA against design.md, which claimed this already worked. It did not — only the
    /// not-found case was handled, so a transient database fault cost BOTH messages rather than
    /// one line.
    /// </remarks>
    [Fact]
    public async Task A_throwing_resource_read_still_produces_a_message()
    {
        var booking = Booking(direct: true);
        var message = await Composer(resourceThrows: true).ForBookerAsync(booking, BookingEvent.Placed);

        Assert.Contains(booking.Reference.Display, message.Body, StringComparison.Ordinal);
        Assert.Contains("15 September 2026", message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("What:", message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_throwing_resource_read_does_not_stop_either_message()
    {
        var sender = new RecordingEmailSender();
        var handler = new BookingEmailHandler(
            new SiteBookingSettings
            {
                TimeZoneId = TestData.LondonZoneId,
                Notifications = Notifications(true, ["desk@example.com"]),
            },
            sender,
            Composer(resourceThrows: true),
            NoResponsibility.Instance,
            new StubHostingEnvironment(),
            NullLogger<BookingEmailHandler>.Instance);

        await handler.HandleAsync(
            new BookingPlacedNotification(Booking(direct: true)), CancellationToken.None);

        Assert.Equal(2, sender.Sent.Count);
    }

    /// <summary>
    /// Cancellation is NOT "what was booked cannot be established". A real token reaches this call
    /// from Umbraco's notification publisher, so an unfiltered catch would log a warning per claim
    /// on shutdown and then carry on composing and sending a message nobody asked for any more —
    /// turning "stop" into "do it anyway, noisily".
    /// </summary>
    /// <remarks>
    /// Added because removing the filter was a mutant the suite could not see: no test cancelled
    /// anything, so the filter was decoration. The same fault as the one QA had just found one
    /// layer up, in the fix for it.
    /// </remarks>
    [Fact]
    public async Task Cancellation_is_not_mistaken_for_an_unreadable_resource()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Composer().ForBookerAsync(Booking(direct: true), BookingEvent.Placed, cancelled.Token));
    }

    /// <summary>
    /// "Not retried and not queued" is a stated guarantee, and it was previously asserted by
    /// nothing — the suite could not tell one attempt from five.
    /// </summary>
    [Fact]
    public async Task A_failing_send_is_attempted_once()
    {
        var sender = new RecordingEmailSender { ThrowOnSend = true };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Send(Notifications(sendBooker: true), sender));

        Assert.Equal(1, sender.Attempts);
    }

    [Fact]
    public async Task A_successful_send_is_attempted_once_per_recipient_list()
    {
        var sender = await Send(Notifications(true, ["desk@example.com"]));

        Assert.Equal(2, sender.Attempts);
    }

    /// <summary>
    /// PINS THE KNOWN LOSS rather than leaving it to a code comment. Neither send is wrapped, so a
    /// failing internal send costs the booker their confirmation. The site is written to first
    /// deliberately — the booker's address is far likelier to bounce, and losing the business's own
    /// notification is the worse outcome — but this direction of the trade is real and is recorded
    /// in design.md. If someone later makes the two independent, this test should fail and be
    /// deleted with intent, not silently keep passing.
    /// </summary>
    [Fact]
    public async Task A_failing_internal_send_costs_the_booker_their_confirmation()
    {
        var sender = new RecordingEmailSender { ThrowOnSend = true };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Send(Notifications(true, ["desk@example.com"]), sender));

        Assert.Empty(sender.Sent);
        Assert.Equal(1, sender.Attempts);
    }

    // ---- the backoffice link -----------------------------------------------------------------

    [Theory]
    [InlineData("https://site.example/", "https://site.example/umbraco/section/ubookit/view/bookings")]
    [InlineData("https://site.example", "https://site.example/umbraco/section/ubookit/view/bookings")]
    // A site in a virtual directory: the segment must survive. Without a trailing slash Uri
    // resolution treats "booking" as a file and replaces it, producing a link to a backoffice
    // that is not there — and it is exactly the deployment nobody reproduces locally.
    [InlineData("https://site.example/booking/", "https://site.example/booking/umbraco/section/ubookit/view/bookings")]
    [InlineData("https://site.example/booking", "https://site.example/booking/umbraco/section/ubookit/view/bookings")]
    public void The_backoffice_link_keeps_the_sites_own_path(string applicationUrl, string expected)
        => Assert.Equal(
            expected,
            BackofficeBookingLink.For(new StubHostingEnvironment(applicationUrl))!.ToString());

    // ---- the seam between the promise and the behaviour --------------------------------------

    /// <summary>
    /// THE ONE THAT WOULD CATCH THE DRIFT. The privacy notice may say a confirmation will be sent
    /// only where one will be, and both halves live in different assemblies — the notice in the
    /// rendering project, the sending in persistence. Each half is well covered on its own, which
    /// is exactly the shape in which a seam goes untested by either.
    /// </summary>
    /// <remarks>
    /// So this drives BOTH: it renders the notice's predicate and it runs the handler, over every
    /// combination of the two settings and the host's answer, and asserts they agree. Restating
    /// the conjunction in either place — or gating the notice on the setting alone, which is the
    /// likely mistake — fails here rather than shipping a site that promises a confirmation it
    /// never sends.
    /// </remarks>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public async Task The_notice_promises_a_message_exactly_when_one_is_sent(
        bool sendBookerEmails, bool hostCanSendMail, bool expected)
    {
        var settings = new SiteBookingSettings
        {
            TimeZoneId = TestData.LondonZoneId,
            Notifications = Notifications(sendBookerEmails),
        };

        // What the booking form would tell the visitor.
        var promised = UBookIt.Web.Rendering.PrivacyNoticeView
            .From(settings, hostCanSendMail)
            .SendsBookerEmail;

        // What actually happens when they book.
        var sender = new RecordingEmailSender { CanSend = hostCanSendMail };
        var handler = new BookingEmailHandler(
            settings, sender, Composer(), NoResponsibility.Instance, new StubHostingEnvironment(), NullLogger<BookingEmailHandler>.Instance);

        await handler.HandleAsync(new BookingPlacedNotification(Booking()), CancellationToken.None);

        var sent = sender.Sent.Any(m => m.To.Contains("ada@example.com"));

        Assert.Equal(expected, promised);
        Assert.Equal(promised, sent);
    }

    /// <summary>
    /// Internal recipients are invisible to the notice: a site telling its own staff is not a
    /// message to the booker, and saying so on the form would describe processing that person
    /// will never see.
    /// </summary>
    [Fact]
    public async Task Telling_the_site_does_not_make_the_notice_promise_anything()
    {
        var settings = new SiteBookingSettings
        {
            TimeZoneId = TestData.LondonZoneId,
            Notifications = Notifications(sendBooker: false, recipients: ["desk@example.com"]),
        };

        var promised = UBookIt.Web.Rendering.PrivacyNoticeView.From(settings, true).SendsBookerEmail;

        var sender = new RecordingEmailSender();
        var handler = new BookingEmailHandler(
            settings, sender, Composer(), NoResponsibility.Instance, new StubHostingEnvironment(), NullLogger<BookingEmailHandler>.Instance);

        await handler.HandleAsync(new BookingPlacedNotification(Booking()), CancellationToken.None);

        Assert.False(promised);
        // Anti-vacuity: something WAS sent, just not to the booker — so the assertion above is
        // about who was written to rather than about a handler that did nothing at all.
        Assert.Single(sender.Sent);
        Assert.DoesNotContain(sender.Sent, m => m.To.Contains("ada@example.com"));
    }

    // ---- fixtures ---------------------------------------------------------------------------

    private static BookingNotificationSettings Notifications(
        bool sendBooker = false, string[]? recipients = null)
        => new() { SendBookerEmails = sendBooker, InternalRecipients = recipients ?? [] };

    private static async Task<RecordingEmailSender> Send(
        BookingNotificationSettings notifications,
        RecordingEmailSender? sender = null,
        Booking? booking = null,
        BookingEvent bookingEvent = BookingEvent.Placed)
    {
        sender ??= new RecordingEmailSender();

        var handler = new BookingEmailHandler(
            new SiteBookingSettings { TimeZoneId = TestData.LondonZoneId, Notifications = notifications },
            sender,
            Composer(),
            NoResponsibility.Instance,
            new StubHostingEnvironment(),
            NullLogger<BookingEmailHandler>.Instance);

        booking ??= Booking();

        switch (bookingEvent)
        {
            case BookingEvent.Cancelled:
                await handler.HandleAsync(new BookingCancelledNotification(booking), CancellationToken.None);
                break;
            case BookingEvent.Confirmed:
                await handler.HandleAsync(new BookingConfirmedNotification(booking), CancellationToken.None);
                break;
            case BookingEvent.Declined:
                await handler.HandleAsync(new BookingDeclinedNotification(booking), CancellationToken.None);
                break;
            default:
                await handler.HandleAsync(new BookingPlacedNotification(booking), CancellationToken.None);
                break;
        }

        return sender;
    }

    /// <summary>
    /// The whole send path, resolved from a container whose only logging provider captures every
    /// line every participant writes.
    /// </summary>
    private static (BookingEmailHandler Handler, CapturingLoggerProvider Logs, RecordingEmailSender Sender) SendPath(
        BookingNotificationSettings notifications,
        bool sendFails = false,
        bool resourceReadFails = false)
    {
        var logs = new CapturingLoggerProvider();
        var sender = new RecordingEmailSender { ThrowOnSend = sendFails };

        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(logs);
        });

        services.AddSingleton(new SiteBookingSettings
        {
            TimeZoneId = TestData.LondonZoneId,
            Notifications = notifications,
        });
        services.AddSingleton<IResourceStore>(new StubResourceStore(true, resourceReadFails));
        services.AddSingleton<IEmailSender>(sender);
        services.AddSingleton<UBookIt.Persistence.Responsibility.IResponsibleRecipientResolver>(NoResponsibility.Instance);
        services.AddSingleton<IHostingEnvironment>(new StubHostingEnvironment());
        services.AddSingleton<BookingMessageComposer>();
        services.AddSingleton<BookingEmailHandler>();

        var provider = services.BuildServiceProvider();

        return (provider.GetRequiredService<BookingEmailHandler>(), logs, sender);
    }

    private static BookingMessageComposer Composer(
        bool resourceExists = true,
        bool resourceThrows = false,
        IBookingTemplateRenderer? renderer = null)
        => new(
            new StubResourceStore(resourceExists, resourceThrows),
            NullLogger<BookingMessageComposer>.Instance,
            renderer);

    private static Booking Booking(
        BookingStatus status = BookingStatus.Confirmed,
        string? zoneId = null,
        bool direct = false)
        => Core.Bookings.Booking.Rehydrate(
            Guid.NewGuid(),
            References.Any(),
            BookingInterval.Create(
                TestData.Utc(TestData.BaseDate, "09:00"),
                TestData.Utc(TestData.BaseDate, "10:00"),
                zoneId ?? TestData.LondonZoneId).Value,
            Booker.Create(null, "Ada Lovelace", "ada@example.com", "07700 900123").Value,
            [new ResourceClaim(ResourceId)],
            status,
            TestData.Now,
            direct ? null : new ServiceAttribution(Guid.NewGuid(), "Initial Consultation"))
            .Value;

    private sealed class StubResourceStore(bool exists, bool throws = false) : IResourceStore
    {
        public Task<Resource?> GetAsync(Guid resourceId, CancellationToken cancellationToken = default)
        {
            // Honoured rather than ignored, because a stub that swallows the token makes every
            // question about cancellation unanswerable — and the composer's catch has a filter
            // whose whole purpose is to let this through.
            cancellationToken.ThrowIfCancellationRequested();

            return throws
                ? throw new InvalidOperationException("The database is unreachable.")
                : Task.FromResult(
                    exists
                        ? Resource.Create("room", "Treatment Room", directlyBookable: true, id: resourceId).Value
                        : null);
        }

        public Task<ResourcePage> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Resource>> ListByTypeAsync(
            string type, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

}

internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly Lock _gate = new();

    private readonly List<(string Category, string Message, string? Exception)> _entries = [];

    private readonly List<string> _categories = [];

    /// <summary>
    /// Every captured line, with the exception it carried rendered separately.
    /// </summary>
    /// <remarks>
    /// <b>The exception is captured because the default formatter drops it.</b>
    /// <c>formatter(state, exception)</c> returns the formatted message template ONLY — while
    /// every real provider (Serilog, console, Umbraco's own) renders the exception alongside it.
    /// A guard reading only the formatter's output therefore has "message templates" for its
    /// extension while claiming "no report of a sending failure contains a booker's details", and
    /// QA moved a booker's address into the exception of a line whose template it had just fixed,
    /// with the whole suite green.
    /// </remarks>
    public IReadOnlyList<(string Category, string Message, string? Exception)> Entries
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries];
            }
        }
    }

    /// <summary>Every category a logger was asked for, whether or not it wrote anything.</summary>
    public IReadOnlyList<string> Categories
    {
        get
        {
            lock (_gate)
            {
                return [.. _categories];
            }
        }
    }

    public ILogger CreateLogger(string categoryName)
    {
        lock (_gate)
        {
            _categories.Add(categoryName);
        }

        return new CapturingLogger(this, categoryName);
    }

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(CapturingLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            // Locked because a capture that silently drops entries is the one thing a PII guard
            // cannot afford. Nothing on the send path logs from parallel continuations today; this
            // costs nothing and removes the day it does.
            lock (provider._gate)
            {
                provider._entries.Add((category, formatter(state, exception), exception?.ToString()));
            }
        }
    }
}

internal static class BookingTapExtensions
{
    /// <summary>Mutate and return, so a fixture stays an expression.</summary>
    public static Booking Tap(this Booking booking, Action<Booking> mutate)
    {
        mutate(booking);

        return booking;
    }
}
