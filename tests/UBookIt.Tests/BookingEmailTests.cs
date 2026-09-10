using Microsoft.Extensions.Logging.Abstractions;
using UBookIt.Core;
using UBookIt.Core.Bookings;
using UBookIt.Core.Resources;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Notifications;
using UBookIt.Tests.Support;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;

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
    /// D7. Placement produces <c>Confirmed</c> today, so the other arms are unreachable — which is
    /// exactly why they must exist. Approval is a named future feature, and a subject hard-coded to
    /// "confirmed" would become false in a customer's inbox from a change that never touched the
    /// composer.
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

    [Fact]
    public async Task Messages_are_plain_text_with_no_sender_of_our_own()
    {
        var message = Assert.Single((await Send(Notifications(sendBooker: true))).Sent);

        Assert.False(message.IsBodyHtml);
        Assert.Null(message.From);
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
    /// package takes care to govern. Asserted over EVERY entry rather than over the one line the
    /// handler is known to write, so a line added later is covered by this without being noticed.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task No_log_line_carries_a_booker(bool sendFails)
    {
        var logger = new CapturingLogger();
        var sender = new RecordingEmailSender { ThrowOnSend = sendFails };
        var handler = new BookingEmailHandler(
            new SiteBookingSettings
            {
                TimeZoneId = TestData.LondonZoneId,
                Notifications = Notifications(true, ["desk@example.com"]),
            },
            sender,
            Composer(),
            new StubHostingEnvironment(),
            logger);

        var booking = Booking();

        try
        {
            await handler.HandleAsync(new BookingPlacedNotification(booking), CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // The handler does not catch: the observer above it does. What is under test here is
            // what was written to the log on the way past, not who catches it.
        }

        Assert.NotEmpty(logger.Entries);

        foreach (var entry in logger.Entries)
        {
            Assert.DoesNotContain("Ada", entry, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Lovelace", entry, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ada@example.com", entry, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("07700", entry, StringComparison.Ordinal);
        }

        // Anti-vacuity: the assertions above pass trivially against a log that says nothing about
        // the booking at all, so prove the entries are actually about THIS booking.
        Assert.Contains(logger.Entries, e => e.Contains(booking.Id.ToString(), StringComparison.Ordinal));
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
            new StubHostingEnvironment(),
            NullLogger<BookingEmailHandler>.Instance);

        await handler.HandleAsync(
            new BookingPlacedNotification(Booking(direct: true)), CancellationToken.None);

        Assert.Equal(2, sender.Sent.Count);
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
            settings, sender, Composer(), new StubHostingEnvironment(), NullLogger<BookingEmailHandler>.Instance);

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
            settings, sender, Composer(), new StubHostingEnvironment(), NullLogger<BookingEmailHandler>.Instance);

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
            new StubHostingEnvironment(),
            NullLogger<BookingEmailHandler>.Instance);

        booking ??= Booking();

        if (bookingEvent == BookingEvent.Cancelled)
        {
            await handler.HandleAsync(new BookingCancelledNotification(booking), CancellationToken.None);
        }
        else
        {
            await handler.HandleAsync(new BookingPlacedNotification(booking), CancellationToken.None);
        }

        return sender;
    }

    private static BookingMessageComposer Composer(bool resourceExists = true, bool resourceThrows = false)
        => new(
            new StubResourceStore(resourceExists, resourceThrows),
            NullLogger<BookingMessageComposer>.Instance);

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
            => throws
                ? throw new InvalidOperationException("The database is unreachable.")
                : Task.FromResult(
                    exists
                        ? Resource.Create("room", "Treatment Room", directlyBookable: true, id: resourceId).Value
                        : null);

        public Task<ResourcePage> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Resource>> ListByTypeAsync(
            string type, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class StubHostingEnvironment(string applicationUrl = "https://site.example/")
        : IHostingEnvironment
    {
        public Uri ApplicationMainUrl { get; } = new(applicationUrl);

        public string SiteName => "Test";

        public string ApplicationId => "test";

        public string ApplicationPhysicalPath => ".";

        public string ApplicationVirtualPath => "/";

        public bool IsHosted => true;

        public bool IsDebugMode => false;

        public string LocalTempPath => ".";

        public string MapPathContentRoot(string path) => path;

        public string MapPathWebRoot(string path) => path;

        public string ToAbsolute(string virtualPath) => virtualPath;

        public void EnsureApplicationMainUrl(Uri? currentApplicationUrl)
        {
        }
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public bool CanSend { get; init; } = true;

        public bool ThrowOnProbe { get; init; }

        public bool ThrowOnSend { get; init; }

        public bool Probed { get; private set; }

        public List<EmailMessage> Sent { get; } = [];

        public List<string> Types { get; } = [];

        public List<bool> Notified { get; } = [];

        /// <summary>Every call, including the ones that threw.</summary>
        public int Attempts { get; private set; }

        public bool CanSendRequiredEmail()
        {
            Probed = true;

            return ThrowOnProbe
                ? throw new NotImplementedException("To send an Email ensure IEmailSender is implemented")
                : CanSend;
        }

        public Task SendAsync(EmailMessage message, string emailType)
            => SendAsync(message, emailType, false, null);

        public Task SendAsync(EmailMessage message, string emailType, bool enableNotification)
            => SendAsync(message, emailType, enableNotification, null);

        public Task SendAsync(
            EmailMessage message, string emailType, bool enableNotification = false, TimeSpan? expires = null)
        {
            Attempts++;

            if (ThrowOnSend)
            {
                throw new InvalidOperationException("The mail server refused the message.");
            }

            Sent.Add(message);
            Types.Add(emailType);
            Notified.Add(enableNotification);

            return Task.CompletedTask;
        }
    }
}

file sealed class CapturingLogger : Microsoft.Extensions.Logging.ILogger<BookingEmailHandler>
{
    public List<string> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

    public void Log<TState>(
        Microsoft.Extensions.Logging.LogLevel logLevel,
        Microsoft.Extensions.Logging.EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
        => Entries.Add(formatter(state, exception));
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
