using Microsoft.Extensions.Logging.Abstractions;
using UBookIt.Core;
using UBookIt.Core.Bookings;
using UBookIt.Core.Resources;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Notifications;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The site direction's gate and recipient list once responsibility exists: the union of
/// the configured list and the resolved parties, per the modified booking-emails
/// requirement "The booker and the site are told independently".
/// </summary>
/// <remarks>
/// The resolver here is the handed-answers fake — which assignments exist and who they
/// resolve to are ITS tests' concern (<see cref="ResponsibleRecipientResolverTests"/> and
/// the integration suite); these tests are about what the HANDLER does with the answers,
/// and each fixture varies the tier the scenario is about so no two pass for the same
/// reason.
/// </remarks>
public class ResponsibilityGateTests
{
    [Fact]
    public async Task Responsibility_alone_enables_the_site_direction()
    {
        // No configured list, booker off — before this change, nothing could be sent.
        var sender = await SendAsync(
            Notifications(),
            new FakeResponsibleRecipients(hasAssignments: true, addresses: ["owner@example.com"]));

        var message = Assert.Single(sender.Sent);

        Assert.Equal("owner@example.com", Assert.Single(message.To));
    }

    [Fact]
    public async Task The_tiers_are_a_union_not_a_precedence()
    {
        var sender = await SendAsync(
            Notifications(recipients: ["desk@example.com"]),
            new FakeResponsibleRecipients(hasAssignments: true, addresses: ["owner@example.com"]));

        var message = Assert.Single(sender.Sent);

        // Both tiers reached — configuring responsibility did not silence the site-wide
        // list, and the list did not pre-empt resolution.
        Assert.Equal(2, message.To.Count());
        Assert.Contains("desk@example.com", message.To);
        Assert.Contains("owner@example.com", message.To);
    }

    [Fact]
    public async Task One_person_in_both_tiers_is_written_to_once()
    {
        var sender = await SendAsync(
            Notifications(recipients: ["desk@example.com"]),
            new FakeResponsibleRecipients(hasAssignments: true, addresses: ["Desk@Example.COM"]));

        // Deduplicated case-insensitively across the tiers: one message, one recipient.
        Assert.Single(Assert.Single(sender.Sent).To);
    }

    /// <summary>
    /// "Asked before the host is" now includes the assignment check: a site with no
    /// list, no booker setting and no assignments has asked for nothing, and must not
    /// be affected by its mail configuration at all — the probe that throws here is
    /// Umbraco's own default sender's behaviour.
    /// </summary>
    [Fact]
    public async Task A_site_that_asked_for_nothing_still_does_not_ask_the_host()
    {
        var sender = new RecordingEmailSender { ThrowOnProbe = true };

        await SendAsync(
            Notifications(),
            new FakeResponsibleRecipients(hasAssignments: false, addresses: []),
            sender);

        Assert.False(sender.Probed);
        Assert.Empty(sender.Sent);
    }

    /// <summary>
    /// User resolution costs Umbraco lookups, so it runs only after the host check: on a
    /// host that cannot send, nothing is resolved. The gate's existence check is the
    /// package's own indexed table and is allowed first — that is "asking whether the
    /// site asked", not resolving who.
    /// </summary>
    [Fact]
    public async Task A_host_that_cannot_send_costs_no_user_resolution()
    {
        var resolver = new FakeResponsibleRecipients(hasAssignments: true, addresses: ["owner@example.com"]);
        var sender = new RecordingEmailSender { CanSend = false };

        await SendAsync(Notifications(), resolver, sender);

        Assert.True(sender.Probed);
        Assert.Equal(0, resolver.ResolveCalls);
        Assert.Empty(sender.Sent);
    }

    /// <summary>
    /// Reachable when every assignment the gate saw resolves to nobody — a deleted or
    /// disabled party. The gate answers "has the site asked", resolution answers "who",
    /// and between them lives this case: asked, but nobody to tell, and no list behind
    /// it. Nothing must be sent and nothing must fault.
    /// </summary>
    [Fact]
    public async Task Assignments_that_resolve_to_nobody_send_nothing()
    {
        var sender = await SendAsync(
            Notifications(),
            new FakeResponsibleRecipients(hasAssignments: true, addresses: []));

        Assert.Empty(sender.Sent);
    }

    [Theory]
    [InlineData(BookingEvent.Confirmed)]
    [InlineData(BookingEvent.Declined)]
    public async Task Responsibility_does_not_extend_the_internal_events(BookingEvent bookingEvent)
    {
        // Confirm and decline are told to the booker only, and responsibility changes
        // WHO the site direction reaches, never WHEN it applies. Booker off, so a message
        // here could only be the internal one that must not exist.
        var sender = await SendAsync(
            Notifications(),
            new FakeResponsibleRecipients(hasAssignments: true, addresses: ["owner@example.com"]),
            bookingEvent: bookingEvent);

        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task An_erased_booker_still_does_not_gate_the_resolved_recipients()
    {
        // Both directions on; the booker is erased. The resolved parties are still told,
        // and nothing is addressed to the booker — there is no address to reach.
        var sender = await SendAsync(
            Notifications(sendBooker: true),
            new FakeResponsibleRecipients(hasAssignments: true, addresses: ["owner@example.com"]),
            booking: ErasedBooking());

        var message = Assert.Single(sender.Sent);

        Assert.Equal("owner@example.com", Assert.Single(message.To));
    }

    // ---- fixtures ----

    private static BookingNotificationSettings Notifications(
        bool sendBooker = false, string[]? recipients = null)
        => new() { SendBookerEmails = sendBooker, InternalRecipients = recipients ?? [] };

    private static async Task<RecordingEmailSender> SendAsync(
        BookingNotificationSettings notifications,
        FakeResponsibleRecipients resolver,
        RecordingEmailSender? sender = null,
        Booking? booking = null,
        BookingEvent bookingEvent = BookingEvent.Placed)
    {
        sender ??= new RecordingEmailSender();

        var handler = new BookingEmailHandler(
            new SiteBookingSettings { TimeZoneId = TestData.LondonZoneId, Notifications = notifications },
            sender,
            new BookingMessageComposer(
                new AbsentResourceStore(), NullLogger<BookingMessageComposer>.Instance),
            resolver,
            new StubHostingEnvironment(),
            NullLogger<BookingEmailHandler>.Instance);

        booking ??= Booking();

        object notification = bookingEvent switch
        {
            BookingEvent.Placed => new BookingPlacedNotification(booking),
            BookingEvent.Confirmed => new BookingConfirmedNotification(booking),
            BookingEvent.Declined => new BookingDeclinedNotification(booking),
            _ => new BookingCancelledNotification(booking),
        };

        await (notification switch
        {
            BookingPlacedNotification n => handler.HandleAsync(n, CancellationToken.None),
            BookingConfirmedNotification n => handler.HandleAsync(n, CancellationToken.None),
            BookingDeclinedNotification n => handler.HandleAsync(n, CancellationToken.None),
            BookingCancelledNotification n => handler.HandleAsync(n, CancellationToken.None),
            _ => throw new InvalidOperationException(),
        });

        return sender;
    }

    private static Booking Booking()
        => Core.Bookings.Booking.Rehydrate(
            Guid.NewGuid(),
            References.Any(),
            BookingInterval.Create(
                TestData.Utc(TestData.BaseDate, "09:00"),
                TestData.Utc(TestData.BaseDate, "10:00"),
                TestData.LondonZoneId).Value,
            Booker.Create(null, "Ada Lovelace", "ada@example.com", null).Value,
            [new ResourceClaim(Guid.NewGuid())],
            BookingStatus.Confirmed,
            TestData.Now).Value;

    private static Booking ErasedBooking()
    {
        var booking = Booking();
        booking.EraseBooker(TestData.Now);
        return booking;
    }

    /// <summary>
    /// No resource resolves. Message CONTENT is not under test here — the composer
    /// produces its unresolvable-subject message, which is enough to address and send.
    /// </summary>
    private sealed class AbsentResourceStore : IResourceStore
    {
        public Task<Resource?> GetAsync(Guid resourceId, CancellationToken cancellationToken = default)
            => Task.FromResult<Resource?>(null);

        public Task<ResourcePage> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Resource>> ListByTypeAsync(string type, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
