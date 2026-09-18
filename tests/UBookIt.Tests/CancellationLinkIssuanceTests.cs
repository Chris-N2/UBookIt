using Microsoft.Extensions.Logging.Abstractions;
using UBookIt.Core;
using UBookIt.Core.Bookings;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Notifications;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// When a cancellation link is issued and when it is not (`self-service-cancellation`, "A
/// cancellation secret is issued with the booker's message, and only then"; `booking-emails`,
/// "What a message tells the booker").
/// </summary>
/// <remarks>
/// <b>Driven through the handler</b>, which is the production entry point: the decision to issue
/// depends on the setting, the event and the clock together, and testing the minting method alone
/// would prove it works when called rather than that it is called when it should be.
/// </remarks>
public class CancellationLinkIssuanceTests
{
    private static readonly UBookIt.Core.Resources.Resource Room = TestData.Room();

    private sealed class RecordingSecrets : ICancellationSecretStore
    {
        public List<(Guid BookingId, string Hash, DateTimeOffset ExpiresUtc)> Issued { get; } = [];

        public bool Throws { get; init; }

        public Task IssueAsync(
            Guid bookingId, string hash, DateTimeOffset expiresUtc, CancellationToken cancellationToken = default)
        {
            if (Throws)
            {
                throw new InvalidOperationException("The secrets table is unavailable.");
            }

            Issued.Add((bookingId, hash, expiresUtc));
            return Task.CompletedTask;
        }

        public Task<CancellationSecretRecord?> FindAsync(string hash, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Guid?> TryRedeemAsync(
            string hash, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static Booking Booking(DateTimeOffset? start = null)
    {
        var from = start ?? TestData.Utc(TestData.BaseDate, "09:00");

        return Core.Bookings.Booking.Rehydrate(
            Guid.NewGuid(),
            new RandomBookingReferenceFactory().Next(),
            BookingInterval.Create(from, from.AddHours(1), TestData.LondonZoneId).Value,
            Booker.Create(null, "Ada Lovelace", "ada@example.com", "07700 900123").Value,
            [new ResourceClaim(Room.Id)],
            BookingStatus.Confirmed,
            TestData.Now).Value;
    }

    private static (BookingEmailHandler Handler, RecordingSecrets Secrets, RecordingEmailSender Sender) Wire(
        bool enabled, DateTimeOffset? now = null, bool secretsThrow = false)
    {
        var sender = new RecordingEmailSender();
        var secrets = new RecordingSecrets { Throws = secretsThrow };

        var handler = new BookingEmailHandler(
            new SiteBookingSettings
            {
                TimeZoneId = TestData.LondonZoneId,
                Notifications = new BookingNotificationSettings { SendBookerEmails = true },
            },
            sender,
            new BookingMessageComposer(
                new InMemoryResourceStore().Add(Room), NullLogger<BookingMessageComposer>.Instance, null),
            NoResponsibility.Instance,
            new StubHostingEnvironment(),
            new SelfServiceCancellationSettings { Enabled = enabled },
            secrets,
            new FixedClock(now ?? TestData.Now),
            NullLogger<BookingEmailHandler>.Instance);

        return (handler, secrets, sender);
    }

    private static string BodyTo(RecordingEmailSender sender, string address)
    {
        // Asserted rather than null-forgiven: a message with no body is a real failure of the
        // composer, and "!" here would report it as a confusing NullReferenceException three
        // frames away instead of as the thing that went wrong.
        var body = sender.Sent.Single(message => message.To.Contains(address)).Body;

        Assert.NotNull(body);
        return body;
    }

    [Fact]
    public async Task With_the_feature_off_nothing_is_issued_and_no_message_mentions_cancelling()
    {
        // The store THROWS if reached, so "off" is verified rather than presumed.
        var (handler, secrets, sender) = Wire(enabled: false);

        await handler.HandleAsync(new BookingPlacedNotification(Booking()), CancellationToken.None);

        Assert.Empty(secrets.Issued);
        Assert.DoesNotContain(CancellationLink.PathBase, BodyTo(sender, "ada@example.com"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Placement_issues_a_secret_and_the_message_carries_the_link()
    {
        var booking = Booking();
        var (handler, secrets, sender) = Wire(enabled: true);

        await handler.HandleAsync(new BookingPlacedNotification(booking), CancellationToken.None);

        var issued = Assert.Single(secrets.Issued);
        Assert.Equal(booking.Id, issued.BookingId);

        // THE EXPIRY IS THE BOOKING'S START, computed at issue and stored — so moving the booking
        // later cannot extend a link already in somebody's inbox.
        Assert.Equal(booking.Interval.StartUtc, issued.ExpiresUtc);

        var body = BodyTo(sender, "ada@example.com");
        Assert.Contains(CancellationLink.PathBase, body, StringComparison.Ordinal);

        // AND IT SAYS WHAT THE LINK IS FOR. A bare address is one a reader discards as decoration,
        // and there is no second copy to fall back on.
        Assert.Contains("cancel", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task What_is_stored_is_the_hash_and_never_the_secret_in_the_message()
    {
        var (handler, secrets, sender) = Wire(enabled: true);

        await handler.HandleAsync(new BookingPlacedNotification(Booking()), CancellationToken.None);

        var body = BodyTo(sender, "ada@example.com");
        var hash = Assert.Single(secrets.Issued).Hash;

        // The message carries something the store does not hold, and the store holds something the
        // message does not carry. That is the whole arrangement in two assertions.
        Assert.DoesNotContain(hash, body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(64, hash.Length);
    }

    [Fact]
    public async Task An_operators_placement_also_issues_one()
    {
        // The booking is the booker's whichever door it came through, and they may cancel it.
        var (handler, secrets, _) = Wire(enabled: true);

        await handler.HandleAsync(new BookingPlacedOnBehalfNotification(Booking()), CancellationToken.None);

        Assert.Single(secrets.Issued);
    }

    [Theory]
    [InlineData("confirmed")]
    [InlineData("cancelled")]
    [InlineData("declined")]
    public async Task A_later_message_does_not_restate_the_link(string later)
    {
        // A single-use credential sent twice sits in two mailbox copies with nothing to tell the
        // reader which is live.
        var booking = Booking();
        var (handler, secrets, sender) = Wire(enabled: true);

        Task raise = later switch
        {
            "confirmed" => handler.HandleAsync(new BookingConfirmedNotification(booking), CancellationToken.None),
            "cancelled" => handler.HandleAsync(new BookingCancelledNotification(booking), CancellationToken.None),
            _ => handler.HandleAsync(new BookingDeclinedNotification(booking), CancellationToken.None),
        };

        await raise;

        Assert.Empty(secrets.Issued);
        Assert.DoesNotContain(CancellationLink.PathBase, BodyTo(sender, "ada@example.com"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_booking_that_has_already_begun_is_issued_nothing()
    {
        // An operator can take a booking at the desk minutes before it starts. The expiry derived
        // from its start would already be past, so the link would be dead before it arrived — a
        // message promising something that cannot work is the failure this package has shipped once.
        var booking = Booking();
        var (handler, secrets, sender) = Wire(enabled: true, now: booking.Interval.StartUtc.AddMinutes(1));

        await handler.HandleAsync(new BookingPlacedOnBehalfNotification(booking), CancellationToken.None);

        Assert.Empty(secrets.Issued);

        // The message is still sent, carrying everything else it is due.
        var body = BodyTo(sender, "ada@example.com");
        Assert.DoesNotContain(CancellationLink.PathBase, body, StringComparison.Ordinal);
        Assert.Contains(booking.Reference.Display, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failure_to_issue_still_sends_the_message()
    {
        // The booking is already placed, and the reference and time are what a person cannot
        // reconstruct. Losing them because a row could not be written would be the wrong trade —
        // the same rule the capability applies to a failure to send.
        var booking = Booking();
        var (handler, _, sender) = Wire(enabled: true, secretsThrow: true);

        await handler.HandleAsync(new BookingPlacedNotification(booking), CancellationToken.None);

        var body = BodyTo(sender, "ada@example.com");
        Assert.Contains(booking.Reference.Display, body, StringComparison.Ordinal);
        Assert.DoesNotContain(CancellationLink.PathBase, body, StringComparison.Ordinal);
    }

    [Fact]
    public void The_link_is_built_from_the_sites_own_address()
    {
        // A message is composed without a request, so the site's configured address is the only
        // thing that can say where the link points — there is no host header to borrow.
        var link = CancellationLink.For(new StubHostingEnvironment("https://bookings.example/"), "abc123");

        Assert.NotNull(link);
        Assert.Equal("https://bookings.example/" + CancellationLink.PathBase + "/abc123", link!.AbsoluteUri);
    }

    [Fact]
    public void A_site_in_a_virtual_directory_keeps_its_prefix()
    {
        // THE TRAILING SLASH, which is not cosmetic: Uri resolution treats the last segment of a
        // base without one as a file and replaces it, so this would otherwise produce
        // https://host/umbraco/... and lose the application path entirely.
        var link = CancellationLink.For(new StubHostingEnvironment("https://host.example/booking"), "abc123");

        Assert.NotNull(link);
        Assert.StartsWith("https://host.example/booking/", link!.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public void No_address_means_no_link_rather_than_a_relative_one()
    {
        // ApplicationMainUrl is declared non-nullable and is not: it stays null until Umbraco
        // resolves the application URL. A relative link is useless from an inbox, so the honest
        // answer is none.
        var link = CancellationLink.For(new StubHostingEnvironment(null), "abc123");

        Assert.Null(link);
    }
}
