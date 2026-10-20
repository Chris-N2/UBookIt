using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UBookIt.Core.Bookings;
using UBookIt.Core.Notifications;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Notifications;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// What the composer gives content about the booking's service, and what a failed read of it
/// costs (service-email-templates: the <c>booking-emails</c> delta, and the <c>email-templates</c>
/// delta's "Both models identify the service and its template set"; design 3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Both audiences, everywhere it matters.</b> The set is read for the site's own message as
/// well as the booker's, and a composer that forgot one would hand that audience the view for every
/// service — so each claim about the model is a theory over the two.
/// </para>
/// <para>
/// The renderer here SUPPLIES content, so "our own wording was used" is visible: a renderer that
/// supplied nothing would make the fallback and a skipped renderer look the same.
/// </para>
/// </remarks>
public class ServiceTemplateSetMessageTests
{
    private const string SiteContent = "SITE-CONTENT-4c1e";

    private static readonly Uri BackofficeLink = new("https://site.example/umbraco");

    private static readonly Uri CancelLink = new("https://site.example/cancel?secret=abc");

    /// <summary>Supplies content for every message, and records what it was asked for.</summary>
    private sealed class SupplyingRenderer : IBookingTemplateRenderer
    {
        public List<BookingMessageModel> Models { get; } = [];

        public Task<BookingTemplateResult> RenderAsync(
            BookingMessageKind kind, BookingMessageModel model, CancellationToken cancellationToken = default)
        {
            Models.Add(model);
            return Task.FromResult(new BookingTemplateResult(BookingTemplateOutcome.Rendered, Body: SiteContent));
        }
    }

    /// <summary>Counts every read, and answers each with the service it holds (or none).</summary>
    private sealed class CountingServiceStore(Service? service) : IServiceStore
    {
        public int Reads { get; private set; }

        public Service? Service { get; set; } = service;

        public Task<Service?> GetAsync(Guid serviceId, CancellationToken cancellationToken = default)
        {
            Reads++;
            return Task.FromResult(Service?.Id == serviceId ? Service : null);
        }

        public Task<ServicePage> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class ThrowingServiceStore : IServiceStore
    {
        public Task<Service?> GetAsync(Guid serviceId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The database is unavailable.");

        public Task<ServicePage> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private static readonly Resource Room = Resource.Create("room", "Consulting Room", directlyBookable: true).Value;

    private static Service Consultation(string? templateSet, string? instructions = "Join on Teams")
        => Service.Create("Online consultation", null, [new ServiceRole("room", 1)]).Value
            .WithBookerInstructions(instructions).Value
            .WithTemplateSet(templateSet).Value;

    private static Booking Booking(BookingStatus status, Service? service)
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
            service is null ? null : new ServiceAttribution(service.Id, service.Name))
            .Value;

    private static BookingMessageComposer Composer(
        IServiceStore? services, IBookingTemplateRenderer? renderer, ILogger<BookingMessageComposer>? logger = null)
        => services is null
            ? new BookingMessageComposer(
                new InMemoryResourceStore().Add(Room), logger ?? NullLogger<BookingMessageComposer>.Instance, renderer)
            : new BookingMessageComposer(
                new InMemoryResourceStore().Add(Room), logger ?? NullLogger<BookingMessageComposer>.Instance, renderer, services);

    /// <summary>
    /// TOTAL dispatch over the audience, so a collapsed dispatch that sent every case to one method
    /// fails on the subject rather than passing on identical expectations.
    /// </summary>
    private static Task<BookingMessage> ComposeAsync(
        BookingMessageComposer composer, string audience, Booking booking, BookingEvent bookingEvent = BookingEvent.Placed)
        => audience switch
        {
            "booker" => composer.ForBookerAsync(booking, bookingEvent),
            "site" => composer.ForSiteAsync(booking, bookingEvent, BackofficeLink),
            _ => throw new ArgumentOutOfRangeException(nameof(audience), audience, "Unknown audience."),
        };

    private static string ExpectedSubject(string audience, Booking booking)
        => (audience, booking.Status) switch
        {
            ("booker", BookingStatus.Confirmed) => "Your booking is confirmed",
            ("booker", BookingStatus.Requested) => "We have received your booking",
            ("site", BookingStatus.Requested) => $"New booking awaiting approval — {booking.Reference.Display}",
            ("site", _) => $"New booking — {booking.Reference.Display}",
            _ => throw new ArgumentOutOfRangeException(nameof(audience), audience, "Unexpected case."),
        };

    private static async Task<BookingMessageModel> ModelForAsync(
        string audience, Booking booking, IServiceStore services)
    {
        var renderer = new SupplyingRenderer();
        await ComposeAsync(Composer(services, renderer), audience, booking);

        var model = Assert.Single(renderer.Models);
        Assert.IsType(audience == "booker" ? typeof(BookerMessageModel) : typeof(InternalMessageModel), model);
        return model;
    }

    // ── Both models identify the service and its template set ───────────────────────────────

    [Theory]
    [InlineData("booker")]
    [InlineData("site")]
    public async Task A_service_bookings_model_carries_the_services_id_and_set(string audience)
    {
        var service = Consultation("remote");

        var model = await ModelForAsync(audience, Booking(BookingStatus.Confirmed, service), new CountingServiceStore(service));

        Assert.Equal(service.Id, model.ServiceId);
        Assert.Equal("remote", model.TemplateSet);
    }

    [Theory]
    [InlineData("booker")]
    [InlineData("site")]
    public async Task The_set_is_absent_where_the_service_names_none(string audience)
    {
        var service = Consultation(templateSet: null);

        var model = await ModelForAsync(audience, Booking(BookingStatus.Confirmed, service), new CountingServiceStore(service));

        Assert.Equal(service.Id, model.ServiceId);
        Assert.Null(model.TemplateSet);
    }

    [Theory]
    [InlineData("booker")]
    [InlineData("site")]
    public async Task A_direct_booking_carries_neither(string audience)
    {
        var model = await ModelForAsync(audience, Booking(BookingStatus.Confirmed, service: null), new CountingServiceStore(null));

        Assert.Null(model.ServiceId);
        Assert.Null(model.TemplateSet);
    }

    [Theory]
    [InlineData("booker")]
    [InlineData("site")]
    public async Task A_deleted_service_keeps_its_recorded_id_and_loses_its_set(string audience)
    {
        var deleted = Consultation("remote");

        // The store no longer holds it: it was deleted after the booking was placed.
        var model = await ModelForAsync(audience, Booking(BookingStatus.Confirmed, deleted), new CountingServiceStore(null));

        Assert.Equal(deleted.Id, model.ServiceId);
        Assert.Null(model.TemplateSet);
    }

    [Theory]
    [InlineData("booker")]
    [InlineData("site")]
    public async Task A_changed_set_reaches_the_next_message(string audience)
    {
        var service = Consultation("in-person");
        var store = new CountingServiceStore(service);
        var booking = Booking(BookingStatus.Requested, service);

        Assert.Equal("in-person", (await ModelForAsync(audience, booking, store)).TemplateSet);

        store.Service = service.WithTemplateSet("remote").Value;

        Assert.Equal("remote", (await ModelForAsync(audience, booking, store)).TemplateSet);
    }

    [Fact]
    public async Task Two_services_sharing_a_set_both_carry_it()
    {
        var first = Consultation("remote");
        var second = Service.Create("Online follow-up", null, [new ServiceRole("room", 1)]).Value
            .WithTemplateSet("remote").Value;

        Assert.Equal("remote", (await ModelForAsync("booker", Booking(BookingStatus.Confirmed, first), new CountingServiceStore(first))).TemplateSet);
        Assert.Equal("remote", (await ModelForAsync("booker", Booking(BookingStatus.Confirmed, second), new CountingServiceStore(second))).TemplateSet);
    }

    [Fact]
    public void The_site_model_has_both_members_and_still_no_contact_detail()
    {
        var properties = typeof(InternalMessageModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(properties, p => p.Name == nameof(BookingMessageModel.ServiceId) && p.PropertyType == typeof(Guid?));
        Assert.Contains(properties, p => p.Name == nameof(BookingMessageModel.TemplateSet) && p.PropertyType == typeof(string));

        foreach (var word in new[] { "Booker", "Email", "Phone", "Contact" })
        {
            Assert.DoesNotContain(properties, p => p.Name.Contains(word, StringComparison.OrdinalIgnoreCase));
        }
    }

    // ── A failed read means the package's own wording ───────────────────────────────────────

    /// <summary>
    /// The scenario "The service cannot be read on a site that supplies content", and the
    /// <c>email-templates</c> scenario "An unreadable service gives the package's own content",
    /// for both audiences and both statuses. The renderer would supply content for every message,
    /// so its content in the body — or the renderer being asked at all — is the failure.
    /// </summary>
    [Theory]
    [InlineData("booker", BookingStatus.Confirmed)]
    [InlineData("booker", BookingStatus.Requested)]
    [InlineData("site", BookingStatus.Confirmed)]
    [InlineData("site", BookingStatus.Requested)]
    public async Task An_unreadable_service_gives_the_packages_own_wording_without_asking_the_renderer(
        string audience, BookingStatus status)
    {
        var logs = new LevelledLogs();
        var factory = LoggerFactory.Create(builder => builder.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
        var renderer = new SupplyingRenderer();
        var composer = Composer(new ThrowingServiceStore(), renderer, factory.CreateLogger<BookingMessageComposer>());
        var booking = Booking(status, Consultation("remote"));

        // The booker's message is given a cancellation link, because "everything else the package's
        // own message carries" includes it and the site's message has no such member to lose.
        var message = audience == "booker"
            ? await composer.ForBookerAsync(booking, BookingEvent.Placed, null, CancelLink, CancellationToken.None)
            : await ComposeAsync(composer, audience, booking);

        if (audience == "booker")
        {
            Assert.Contains(CancelLink.AbsoluteUri, message.Body, StringComparison.Ordinal);
        }

        // The package's own message, whole: its subject, the reference, what and when.
        Assert.Empty(renderer.Models);
        Assert.Equal(ExpectedSubject(audience, booking), message.Subject);
        Assert.DoesNotContain(SiteContent, message.Body, StringComparison.Ordinal);
        Assert.Contains(booking.Reference.Display, message.Body, StringComparison.Ordinal);
        Assert.Contains("Online consultation", message.Body, StringComparison.Ordinal);
        Assert.Contains("9:00 AM (Europe/London)", message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Instructions for your booking", message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Join on Teams", message.Body, StringComparison.Ordinal);

        // Logged ONCE — the read failure, and no second line claiming a render failed.
        var entry = Assert.Single(logs.Entries, e => e.Level >= LogLevel.Warning);
        Assert.Contains("could not read a service", entry.Message, StringComparison.Ordinal);
        Assert.Contains(booking.Id.ToString(), entry.Message, StringComparison.Ordinal);

        foreach (var detail in new[] { "Ada Lovelace", "ada@example.com", "07700 900123" })
        {
            Assert.DoesNotContain(detail, entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(detail, entry.Exception ?? "", StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_readable_service_lets_the_same_renderer_supply_content()
    {
        // ANTI-VACUITY for the theory above: the same renderer, wired the same way, does supply
        // content when the read succeeds — so its absence above is the failure path at work.
        var service = Consultation("remote");
        var renderer = new SupplyingRenderer();

        var message = await ComposeAsync(Composer(new CountingServiceStore(service), renderer), "booker", Booking(BookingStatus.Confirmed, service));

        Assert.Equal(SiteContent, message.Body);
        Assert.Single(renderer.Models);
    }

    // ── What a message costs in service reads (design 3, the table) ─────────────────────────

    /// <summary>
    /// At most one service read per message, and none where nothing could use it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A TABLE over audience × booking shape × renderer × status, on the terms
    /// <c>BookingTemplateCompositionTests.What_a_message_costs_in_store_reads</c> set: the defect
    /// this guards against is a cost moving between cells — the site's message skipping the read,
    /// the booker's reading twice, a host without a renderer starting to pay for a set it cannot
    /// use — rather than a wrong message.
    /// </para>
    /// <para>
    /// The expectations DIFFER between the audiences (booker/service/no renderer/Confirmed reads,
    /// site/service/no renderer does not), so a dispatch collapsed to one method fails on counts as
    /// well as on the subject.
    /// </para>
    /// </remarks>
    [Theory]
    // audience  direct  renderer  status                       reads
    [InlineData("booker", true, false, BookingStatus.Confirmed, 0)]
    [InlineData("booker", true, false, BookingStatus.Requested, 0)]
    [InlineData("booker", true, true, BookingStatus.Confirmed, 0)]
    [InlineData("booker", true, true, BookingStatus.Requested, 0)]
    [InlineData("booker", false, false, BookingStatus.Confirmed, 1)]
    [InlineData("booker", false, false, BookingStatus.Requested, 0)]
    [InlineData("booker", false, true, BookingStatus.Confirmed, 1)]
    [InlineData("booker", false, true, BookingStatus.Requested, 1)]
    [InlineData("site", true, false, BookingStatus.Confirmed, 0)]
    [InlineData("site", true, false, BookingStatus.Requested, 0)]
    [InlineData("site", true, true, BookingStatus.Confirmed, 0)]
    [InlineData("site", true, true, BookingStatus.Requested, 0)]
    [InlineData("site", false, false, BookingStatus.Confirmed, 0)]
    [InlineData("site", false, false, BookingStatus.Requested, 0)]
    [InlineData("site", false, true, BookingStatus.Confirmed, 1)]
    [InlineData("site", false, true, BookingStatus.Requested, 1)]
    public async Task What_a_message_costs_in_service_reads(
        string audience, bool direct, bool withRenderer, BookingStatus status, int expectedReads)
    {
        var service = Consultation("remote");
        var store = new CountingServiceStore(service);

        // A renderer that supplies NOTHING, so the subject asserted below is the package's own and
        // the dispatch check holds in every cell.
        var renderer = withRenderer ? new RecordingNothingRenderer() : null;
        var booking = Booking(status, direct ? null : service);

        var message = await ComposeAsync(Composer(store, renderer), audience, booking);

        Assert.Equal(ExpectedSubject(audience, booking), message.Subject);
        Assert.Equal(expectedReads, store.Reads);
    }

    private sealed class RecordingNothingRenderer : IBookingTemplateRenderer
    {
        public Task<BookingTemplateResult> RenderAsync(
            BookingMessageKind kind, BookingMessageModel model, CancellationToken cancellationToken = default)
            => Task.FromResult(BookingTemplateResult.NotSupplied);
    }

    // ── A composer built through the 17.0.0 constructor ─────────────────────────────────────

    /// <summary>
    /// Design 3, last bullet: an absent store is not a read that failed. The set is absent, the
    /// id is still there, the renderer IS asked — so lookup proceeds from the view for every
    /// service — and nothing is logged as a failure.
    /// </summary>
    [Theory]
    [InlineData("booker")]
    [InlineData("site")]
    public async Task The_published_constructor_has_no_set_and_still_asks_the_renderer(string audience)
    {
        var logs = new LevelledLogs();
        var factory = LoggerFactory.Create(builder => builder.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
        var renderer = new SupplyingRenderer();
        var service = Consultation("remote");
        var composer = Composer(services: null, renderer, factory.CreateLogger<BookingMessageComposer>());

        var message = await ComposeAsync(composer, audience, Booking(BookingStatus.Confirmed, service));

        var model = Assert.Single(renderer.Models);
        Assert.Equal(SiteContent, message.Body);
        Assert.Equal(service.Id, model.ServiceId);
        Assert.Null(model.TemplateSet);
        Assert.DoesNotContain(logs.Entries, e => e.Level >= LogLevel.Warning);
    }

    /// <summary>
    /// Every line written, with its level and exception — the level is what "logged as a failure"
    /// means, which the suite's shared capture does not record.
    /// </summary>
    private sealed class LevelledLogs : ILoggerProvider
    {
        private readonly Lock _gate = new();
        private readonly List<(LogLevel Level, string Message, string? Exception)> _entries = [];

        public IReadOnlyList<(LogLevel Level, string Message, string? Exception)> Entries
        {
            get
            {
                lock (_gate)
                {
                    return [.. _entries];
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => new Logger(this);

        public void Dispose()
        {
        }

        private sealed class Logger(LevelledLogs owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (owner._gate)
                {
                    owner._entries.Add((logLevel, formatter(state, exception), exception?.ToString()));
                }
            }
        }
    }
}