using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UBookIt.Core.Bookings;
using UBookIt.Core.Notifications;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Composing;
using UBookIt.Persistence.Notifications;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Which messages carry a service's instructions for the booker (service-booker-instructions: the
/// <c>booking-emails</c> and <c>email-templates</c> deltas).
/// </summary>
/// <remarks>
/// <para>
/// <b>Every scenario is asserted on BOTH halves</b> — the package's own wording and the model a
/// supplied view is given — because design 4 makes one read feed both, and a test of one half
/// cannot see the other drift.
/// </para>
/// <para>
/// The marker is distinctive so that "absent" means absent from the whole body, not merely from
/// the line the test expected it on.
/// </para>
/// </remarks>
public class ServiceBookerInstructionsMessageTests
{
    private const string Marker = "JOIN-7f3a https://example.test/meet";

    private const string Instructions = $"Join on Teams:\r\n{Marker}\r\n\r\nPlease test your microphone.";

    private static readonly Uri CancelLink = new("https://site.example/cancel?secret=abc");

    /// <summary>Records what it was asked for, and supplies nothing, so the package's wording stands.</summary>
    private sealed class RecordingRenderer : IBookingTemplateRenderer
    {
        public List<BookingMessageModel> Models { get; } = [];

        public Task<BookingTemplateResult> RenderAsync(
            BookingMessageKind kind, BookingMessageModel model, CancellationToken cancellationToken = default)
        {
            Models.Add(model);
            return Task.FromResult(BookingTemplateResult.NotSupplied);
        }
    }

    private sealed class ThrowingServiceStore : IServiceStore
    {
        public Task<Service?> GetAsync(Guid serviceId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The database is unavailable.");

        public Task<ServicePage> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class CancellingServiceStore : IServiceStore
    {
        public Task<Service?> GetAsync(Guid serviceId, CancellationToken cancellationToken = default)
            => throw new OperationCanceledException();

        public Task<ServicePage> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private static readonly Resource Room = Resource.Create("room", "Consulting Room", directlyBookable: true).Value;

    private static Service Consultation(string? instructions = Instructions, string name = "Online consultation")
        => Service.Create(name, null, [new ServiceRole("room", 1)]).Value
            .WithBookerInstructions(instructions).Value;

    private static Booking Booking(BookingStatus status, Service? service, string? recordedName = null)
        => Core.Bookings.Booking.Rehydrate(
            Guid.NewGuid(),
            References.Any(),
            BookingInterval.Create(
                TestData.Utc(TestData.BaseDate, "09:00"),
                TestData.Utc(TestData.BaseDate, "10:00"),
                TestData.LondonZoneId).Value,
            Booker.Create(null, "Ada Lovelace", "ada@example.com", "07700 900123").Value,
            [new ResourceClaim(Room.Id)],
            status,
            TestData.Now,
            service is null ? null : new ServiceAttribution(service.Id, recordedName ?? service.Name))
            .Value;

    private static readonly BookingInterval Previous = BookingInterval.Create(
        TestData.Utc(TestData.BaseDate, "14:00"),
        TestData.Utc(TestData.BaseDate, "15:00"),
        TestData.LondonZoneId).Value;

    private static (BookingMessageComposer Composer, RecordingRenderer Renderer) Wire(
        IServiceStore services, ILogger<BookingMessageComposer>? logger = null)
    {
        var renderer = new RecordingRenderer();
        return (
            new BookingMessageComposer(
                new InMemoryResourceStore().Add(Room),
                logger ?? NullLogger<BookingMessageComposer>.Instance,
                renderer,
                services),
            renderer);
    }

    private static async Task<(BookingMessage Message, BookerMessageModel Model)> ComposeAsync(
        Booking booking, BookingEvent bookingEvent, params Service[] services)
    {
        var store = new InMemoryServiceStore();
        foreach (var service in services)
        {
            store.Add(service);
        }

        var (composer, renderer) = Wire(store);
        var message = await composer.ForBookerAsync(
            booking,
            bookingEvent,
            bookingEvent == BookingEvent.Moved ? Previous : null,
            CancelLink,
            CancellationToken.None);

        return (message, Assert.IsType<BookerMessageModel>(Assert.Single(renderer.Models)));
    }

    private static void AssertCarries((BookingMessage Message, BookerMessageModel Model) composed)
    {
        Assert.Contains("Instructions for your booking:", composed.Message.Body, StringComparison.Ordinal);
        Assert.Contains(Marker, composed.Message.Body, StringComparison.Ordinal);
        Assert.Equal(Instructions, composed.Model.BookerInstructions);
    }

    private static void AssertDoesNotCarry((BookingMessage Message, BookerMessageModel Model) composed)
    {
        Assert.DoesNotContain("Instructions for your booking", composed.Message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("JOIN-7f3a", composed.Message.Body, StringComparison.Ordinal);
        Assert.Null(composed.Model.BookerInstructions);
    }

    // ── Which messages carry them ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(BookingEvent.Placed)]
    [InlineData(BookingEvent.PlacedOnBehalf)]
    public async Task A_placement_confirmed_on_placement_carries_them(BookingEvent placement)
    {
        var service = Consultation();

        AssertCarries(await ComposeAsync(Booking(BookingStatus.Confirmed, service), placement, service));
    }

    [Theory]
    [InlineData(BookingEvent.Placed)]
    [InlineData(BookingEvent.PlacedOnBehalf)]
    public async Task A_placement_awaiting_approval_does_not(BookingEvent placement)
    {
        var service = Consultation();

        AssertDoesNotCarry(await ComposeAsync(Booking(BookingStatus.Requested, service), placement, service));
    }

    [Fact]
    public async Task The_confirmation_carries_them()
    {
        var service = Consultation();

        AssertCarries(await ComposeAsync(Booking(BookingStatus.Confirmed, service), BookingEvent.Confirmed, service));
    }

    [Fact]
    public async Task A_confirmed_booking_moved_carries_them()
    {
        var service = Consultation();

        AssertCarries(await ComposeAsync(Booking(BookingStatus.Confirmed, service), BookingEvent.Moved, service));
    }

    [Fact]
    public async Task A_requested_booking_moved_does_not()
    {
        var service = Consultation();

        AssertDoesNotCarry(await ComposeAsync(Booking(BookingStatus.Requested, service), BookingEvent.Moved, service));
    }

    [Fact]
    public async Task A_decline_does_not()
    {
        var service = Consultation();

        AssertDoesNotCarry(await ComposeAsync(Booking(BookingStatus.Declined, service), BookingEvent.Declined, service));
    }

    [Fact]
    public async Task A_cancellation_does_not()
    {
        var service = Consultation();

        AssertDoesNotCarry(await ComposeAsync(Booking(BookingStatus.Cancelled, service), BookingEvent.Cancelled, service));
    }

    [Fact]
    public async Task A_booking_placed_directly_against_a_resource_carries_none()
    {
        AssertDoesNotCarry(await ComposeAsync(Booking(BookingStatus.Confirmed, service: null), BookingEvent.Placed));
    }

    [Fact]
    public async Task A_deleted_service_gives_none()
    {
        var deleted = Consultation();

        // The service is not in the store: it was deleted after the booking was placed.
        AssertDoesNotCarry(await ComposeAsync(Booking(BookingStatus.Confirmed, deleted), BookingEvent.Confirmed));
    }

    [Fact]
    public async Task A_service_without_instructions_gives_none()
    {
        var service = Consultation(instructions: null);

        AssertDoesNotCarry(await ComposeAsync(Booking(BookingStatus.Confirmed, service), BookingEvent.Confirmed, service));
    }

    // ── Read now, not recorded at placement ─────────────────────────────────────────────────

    [Fact]
    public async Task The_current_instructions_are_sent_and_the_recorded_name_is_kept()
    {
        // Placed as "Online consultation" with the old link; since renamed, and given a new link.
        var now = Consultation("New link: https://example.test/new", name: "Video consultation");
        var booking = Booking(BookingStatus.Confirmed, now, recordedName: "Online consultation");

        var (message, model) = await ComposeAsync(booking, BookingEvent.Moved, now);

        Assert.Contains("https://example.test/new", message.Body, StringComparison.Ordinal);
        Assert.Equal("New link: https://example.test/new", model.BookerInstructions);

        // The name is the one recorded when it was sold, on the same message.
        Assert.Contains("Online consultation", message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Video consultation", message.Body, StringComparison.Ordinal);
        Assert.Equal("Online consultation", model.ServiceName);
    }

    // ── A failed read costs the instructions and nothing else ────────────────────────────────

    [Fact]
    public async Task A_service_that_cannot_be_read_costs_the_instructions_alone_and_is_logged_without_the_booker()
    {
        var logs = new CapturingLoggerProvider();
        var factory = LoggerFactory.Create(builder => builder.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
        var (composer, renderer) = Wire(new ThrowingServiceStore(), factory.CreateLogger<BookingMessageComposer>());
        var booking = Booking(BookingStatus.Confirmed, Consultation());

        var message = await composer.ForBookerAsync(
            booking, BookingEvent.Confirmed, null, CancelLink, CancellationToken.None);

        // Everything else it is due: the subject, the reference, what and when, and the link.
        Assert.Equal("Your booking is confirmed", message.Subject);
        Assert.Contains(booking.Reference.Display, message.Body, StringComparison.Ordinal);
        Assert.Contains("Online consultation", message.Body, StringComparison.Ordinal);
        Assert.Contains("9:00 AM (Europe/London)", message.Body, StringComparison.Ordinal);
        Assert.Contains(CancelLink.AbsoluteUri, message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Instructions for your booking", message.Body, StringComparison.Ordinal);
        Assert.Null(Assert.IsType<BookerMessageModel>(Assert.Single(renderer.Models)).BookerInstructions);

        var entry = Assert.Single(logs.Entries, e => e.Message.Contains("could not read a service", StringComparison.Ordinal));
        Assert.Contains(booking.Id.ToString(), entry.Message, StringComparison.Ordinal);

        foreach (var detail in new[] { "Ada Lovelace", "ada@example.com", "07700 900123" })
        {
            Assert.DoesNotContain(detail, entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(detail, entry.Exception ?? "", StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Cancellation_is_not_swallowed()
    {
        var (composer, _) = Wire(new CancellingServiceStore());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => composer.ForBookerAsync(
            Booking(BookingStatus.Confirmed, Consultation()), BookingEvent.Confirmed, null, null,
            CancellationToken.None));
    }

    [Fact]
    public async Task The_service_is_not_read_for_a_message_that_could_not_carry_them()
    {
        // Design 2: reading only when needed keeps the extra read off every other message. A
        // store that throws proves it was never asked — a read would have been logged.
        var logs = new CapturingLoggerProvider();
        var factory = LoggerFactory.Create(builder => builder.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
        var (composer, _) = Wire(new ThrowingServiceStore(), factory.CreateLogger<BookingMessageComposer>());

        await composer.ForBookerAsync(
            Booking(BookingStatus.Requested, Consultation()), BookingEvent.Placed, null, null,
            CancellationToken.None);

        Assert.DoesNotContain(logs.Entries, e => e.Message.Contains("could not read a service", StringComparison.Ordinal));
    }

    // ── Where they sit, and how they read ────────────────────────────────────────────────────

    [Fact]
    public async Task They_sit_after_the_booking_and_its_closing_line_and_before_the_cancellation_link()
    {
        var service = Consultation();

        var (message, _) = await ComposeAsync(Booking(BookingStatus.Confirmed, service), BookingEvent.Confirmed, service);
        var body = message.Body;

        var when = body.IndexOf("at 9:00 AM", StringComparison.Ordinal);
        var closing = body.IndexOf("Please quote the reference above", StringComparison.Ordinal);
        var heading = body.IndexOf("Instructions for your booking:", StringComparison.Ordinal);
        var cancel = body.IndexOf("If you need to cancel this booking", StringComparison.Ordinal);

        Assert.True(when >= 0 && closing > when && heading > closing && cancel > heading, body);
    }

    [Fact]
    public async Task They_are_reproduced_as_written_without_doubled_line_breaks()
    {
        var service = Consultation();

        var (message, _) = await ComposeAsync(Booking(BookingStatus.Confirmed, service), BookingEvent.Confirmed, service);

        // The CRLFs the textarea gave are the body's own line endings now: one blank line where the
        // site wrote one, and no \r\r\n or stray \r where a CRLF met AppendLine.
        var nl = Environment.NewLine;
        Assert.Contains(
            $"Instructions for your booking:{nl}Join on Teams:{nl}{Marker}{nl}{nl}Please test your microphone.{nl}",
            message.Body,
            StringComparison.Ordinal);
        Assert.DoesNotContain("\r\r", message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain($"{nl}{nl}{nl}", message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_no_instructions_no_heading_is_left_over_a_blank()
    {
        var service = Consultation(instructions: null);
        var withNone = await ComposeAsync(Booking(BookingStatus.Confirmed, service), BookingEvent.Confirmed, service);

        Assert.DoesNotContain("Instructions", withNone.Message.Body, StringComparison.Ordinal);
    }

    // ── The site's own recipients, and the internal model ────────────────────────────────────

    [Fact]
    public async Task The_message_to_the_sites_own_recipients_does_not_carry_them()
    {
        var service = Consultation();
        var store = new InMemoryServiceStore().Add(service);
        var (composer, renderer) = Wire(store);

        var message = await composer.ForSiteAsync(
            Booking(BookingStatus.Confirmed, service), BookingEvent.Placed, new Uri("https://site.example/umbraco"),
            CancellationToken.None);

        Assert.DoesNotContain("JOIN-7f3a", message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Instructions", message.Body, StringComparison.Ordinal);
        Assert.IsType<InternalMessageModel>(Assert.Single(renderer.Models));
    }

    [Fact]
    public void The_internal_model_has_no_member_for_them()
    {
        // By type, not by value: the compiler is what keeps them out of the internal message, so
        // the guarantee is that there is nothing to set. Every public property, under any name
        // that could hold them.
        var properties = typeof(InternalMessageModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.DoesNotContain(properties, p => p.Name.Contains("Instruction", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            typeof(BookerMessageModel).GetProperties(),
            p => p.Name == nameof(BookerMessageModel.BookerInstructions) && p.PropertyType == typeof(string));
    }

    // ── The seam: the package's own registration ─────────────────────────────────────────────

    [Fact]
    public async Task The_package_registers_a_composer_that_reads_instructions_even_with_no_renderer()
    {
        // Routed through the persistence composer's real registration rather than through `new`,
        // so a registration that let the container choose the published three-parameter
        // constructor — which it would, with no renderer to satisfy the longer one — turns this
        // red. No renderer is registered here, which is the case that matters.
        var registrations = new ServiceCollection();
        new UBookItPersistenceComposer().Compose(new ServicesOnlyUmbracoBuilder(registrations));
        var descriptor = Assert.Single(registrations, d => d.ServiceType == typeof(BookingMessageComposer));

        var service = Consultation();
        var container = new ServiceCollection()
            .AddSingleton<IResourceStore>(new InMemoryResourceStore().Add(Room))
            .AddSingleton<IServiceStore>(new InMemoryServiceStore().Add(service))
            .AddLogging();
        container.Add(descriptor);

        await using var provider = container.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var composer = scope.ServiceProvider.GetRequiredService<BookingMessageComposer>();

        var message = await composer.ForBookerAsync(
            Booking(BookingStatus.Confirmed, service), BookingEvent.Confirmed, null, null,
            CancellationToken.None);

        Assert.Contains(Marker, message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_composer_built_through_the_published_constructor_carries_none()
    {
        // The published constructor has no store to read, so it says nothing rather than failing.
        var service = Consultation();
        var composer = new BookingMessageComposer(
            new InMemoryResourceStore().Add(Room), NullLogger<BookingMessageComposer>.Instance);

        var message = await composer.ForBookerAsync(
            Booking(BookingStatus.Confirmed, service), BookingEvent.Confirmed, null, null,
            CancellationToken.None);

        Assert.DoesNotContain("Instructions", message.Body, StringComparison.Ordinal);
    }
}
