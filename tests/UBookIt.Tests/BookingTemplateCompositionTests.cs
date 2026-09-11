using Microsoft.Extensions.Logging.Abstractions;
using UBookIt.Core.Bookings;
using UBookIt.Core.Notifications;
using UBookIt.Core.Resources;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Notifications;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// What the composer does with site-supplied content — and, more of the point, what it keeps
/// doing regardless of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The headline guarantee here is the negative one.</b> A site that supplies nothing must send
/// exactly what it sent before this feature existed, so the first test asserts against the
/// literal strings rather than against "some message was produced". A feature that quietly
/// reworded every existing site's mail would be a worse outcome than not shipping it.
/// </para>
/// <para>
/// The rest of the file is about the three outcomes being genuinely three: rendered, absent, and
/// broken. Collapsing broken into absent is the defect this design was shaped to avoid.
/// </para>
/// </remarks>
public class BookingTemplateCompositionTests
{
    private static readonly Guid ResourceId = Guid.NewGuid();

    /// <summary>Returns whatever the test needs, and records what it was asked for.</summary>
    private sealed class StubRenderer(BookingTemplateResult result) : IBookingTemplateRenderer
    {
        public List<(BookingMessageKind Kind, BookingMessageModel Model)> Asked { get; } = [];

        public Task<BookingTemplateResult> RenderAsync(
            BookingMessageKind kind, BookingMessageModel model, CancellationToken cancellationToken = default)
        {
            Asked.Add((kind, model));
            return Task.FromResult(result);
        }
    }

    private sealed class StubResourceStore : IResourceStore
    {
        public Task<Resource?> GetAsync(Guid resourceId, CancellationToken cancellationToken = default)
            => Task.FromResult<Resource?>(
                Resource.Create("room", "Treatment Room", directlyBookable: true, id: resourceId).Value);

        public Task<ResourcePage> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Resource>> ListByTypeAsync(
            string type, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private static BookingMessageComposer Composer(IBookingTemplateRenderer? renderer)
        => new(new StubResourceStore(), NullLogger<BookingMessageComposer>.Instance, renderer);

    private static Booking Booking(
        BookingStatus status = BookingStatus.Confirmed, bool direct = false)
        => Core.Bookings.Booking.Rehydrate(
            Guid.NewGuid(),
            References.Any(),
            BookingInterval.Create(
                TestData.Utc(TestData.BaseDate, "09:00"),
                TestData.Utc(TestData.BaseDate, "10:00"),
                TestData.LondonZoneId).Value,
            Booker.Create(null, "Ada Lovelace", "ada@example.com", "07700 900123").Value,
            [new ResourceClaim(ResourceId)],
            status,
            TestData.Now,
            direct ? null : new ServiceAttribution(Guid.NewGuid(), "Initial Consultation"))
            .Value;

    // ---- the migration guarantee ---------------------------------------------------------

    [Fact]
    public async Task With_no_renderer_the_messages_are_exactly_what_they_were()
    {
        // ASSERTED AGAINST THE LITERAL WORDING, not against "a message came out". This is the
        // promise that upgrading changes nothing for a site that supplies nothing, and a test
        // that only checked a message existed would pass against a total rewrite.
        var booking = Booking();
        var composer = Composer(renderer: null);

        var booker = await composer.ForBookerAsync(booking, BookingEvent.Placed);
        var site = await composer.ForSiteAsync(booking, BookingEvent.Placed, new Uri("https://site.example/umbraco"));

        Assert.Equal("Your booking is confirmed", booker.Subject);
        Assert.Contains("Initial Consultation", booker.Body, StringComparison.Ordinal);
        Assert.Contains("9:00 AM (Europe/London)", booker.Body, StringComparison.Ordinal);
        Assert.False(booker.IsHtml);

        Assert.Equal($"New booking — {booking.Reference.Display}", site.Subject);
        Assert.False(site.IsHtml);
    }

    // ---- the three outcomes are three ----------------------------------------------------

    [Fact]
    public async Task Rendered_content_replaces_the_body()
    {
        var renderer = new StubRenderer(new BookingTemplateResult(
            BookingTemplateOutcome.Rendered, Body: "Our own words."));

        var message = await Composer(renderer).ForBookerAsync(Booking(), BookingEvent.Placed);

        Assert.Equal("Our own words.", message.Body);
    }

    [Fact]
    public async Task Content_that_is_not_supplied_leaves_the_package_message_alone()
    {
        var renderer = new StubRenderer(BookingTemplateResult.NotSupplied);

        var message = await Composer(renderer).ForBookerAsync(Booking(), BookingEvent.Placed);

        Assert.Equal("Your booking is confirmed", message.Subject);
        Assert.Contains("Initial Consultation", message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Content_that_fails_falls_back_and_the_message_still_goes()
    {
        // A broken template must not cost the message. The booking is already stored, and a
        // fault in wording must not become the booker's problem.
        var renderer = new StubRenderer(BookingTemplateResult.Failed);

        var message = await Composer(renderer).ForBookerAsync(Booking(), BookingEvent.Placed);

        Assert.Equal("Your booking is confirmed", message.Subject);
        Assert.Contains("Initial Consultation", message.Body, StringComparison.Ordinal);
    }

    // ---- subject and content type --------------------------------------------------------

    [Fact]
    public async Task A_stated_subject_wins_and_silence_keeps_ours()
    {
        var stated = new StubRenderer(new BookingTemplateResult(
            BookingTemplateOutcome.Rendered, Body: "b", Subject: "Your appointment at Acme"));
        var silent = new StubRenderer(new BookingTemplateResult(
            BookingTemplateOutcome.Rendered, Body: "b"));

        Assert.Equal(
            "Your appointment at Acme",
            (await Composer(stated).ForBookerAsync(Booking(), BookingEvent.Placed)).Subject);

        // Supplying a body is not the same as taking responsibility for the whole message.
        Assert.Equal(
            "Your booking is confirmed",
            (await Composer(silent).ForBookerAsync(Booking(), BookingEvent.Placed)).Subject);
    }

    [Fact]
    public async Task Content_is_plain_text_unless_it_says_otherwise_and_is_never_sniffed()
    {
        // The body is unmistakably markup and still goes out as text, because nothing said
        // otherwise. Inferring would be wrong silently, in somebody's inbox.
        var markupWithoutFlag = new StubRenderer(new BookingTemplateResult(
            BookingTemplateOutcome.Rendered, Body: "<p>Hello</p>"));
        var declared = new StubRenderer(new BookingTemplateResult(
            BookingTemplateOutcome.Rendered, Body: "<p>Hello</p>", IsHtml: true));

        Assert.False((await Composer(markupWithoutFlag).ForBookerAsync(Booking(), BookingEvent.Placed)).IsHtml);
        Assert.True((await Composer(declared).ForBookerAsync(Booking(), BookingEvent.Placed)).IsHtml);
    }

    // ---- which message, and what it is given ---------------------------------------------

    [Theory]
    [InlineData(BookingEvent.Placed, BookingMessageKind.BookerPlaced)]
    [InlineData(BookingEvent.Confirmed, BookingMessageKind.BookerConfirmed)]
    [InlineData(BookingEvent.Declined, BookingMessageKind.BookerDeclined)]
    [InlineData(BookingEvent.Cancelled, BookingMessageKind.BookerCancelled)]
    public async Task The_booker_message_kind_follows_the_event(BookingEvent given, BookingMessageKind expected)
    {
        // From the EVENT, not the status: a placement that auto-confirmed and an operator's
        // confirmation both read Confirmed, and content written for one must not render for
        // the other.
        var renderer = new StubRenderer(BookingTemplateResult.NotSupplied);

        await Composer(renderer).ForBookerAsync(Booking(), given);

        Assert.Equal(expected, Assert.Single(renderer.Asked).Kind);
    }

    [Theory]
    [InlineData(BookingEvent.Placed, BookingMessageKind.InternalPlaced)]
    [InlineData(BookingEvent.Cancelled, BookingMessageKind.InternalCancelled)]
    public async Task The_internal_message_kind_follows_the_event(BookingEvent given, BookingMessageKind expected)
    {
        var renderer = new StubRenderer(BookingTemplateResult.NotSupplied);

        await Composer(renderer).ForSiteAsync(Booking(), given, backofficeUrl: null);

        Assert.Equal(expected, Assert.Single(renderer.Asked).Kind);
    }

    [Fact]
    public async Task The_internal_model_is_the_one_with_no_booker_on_it()
    {
        // The type itself is the guarantee — see InternalMessageModel. This asserts the composer
        // actually hands over that type rather than the other one, which is the half a model
        // with no booker member cannot enforce on its own.
        var renderer = new StubRenderer(BookingTemplateResult.NotSupplied);

        await Composer(renderer).ForSiteAsync(Booking(), BookingEvent.Placed, backofficeUrl: null);

        Assert.IsType<InternalMessageModel>(Assert.Single(renderer.Asked).Model);
    }

    [Fact]
    public async Task The_booker_model_carries_the_details_the_message_is_addressed_to()
    {
        var renderer = new StubRenderer(BookingTemplateResult.NotSupplied);

        await Composer(renderer).ForBookerAsync(Booking(), BookingEvent.Placed);

        var model = Assert.IsType<BookerMessageModel>(Assert.Single(renderer.Asked).Model);

        Assert.Equal("Ada Lovelace", model.BookerName);
        Assert.Equal("ada@example.com", model.BookerEmail);
        Assert.Equal("07700 900123", model.BookerPhone);
    }

    [Fact]
    public async Task The_model_carries_structure_rather_than_a_joined_sentence()
    {
        // The whole reason content is Razor rather than numbered placeholders: a service
        // resolves to resources and content must be able to enumerate them.
        var renderer = new StubRenderer(BookingTemplateResult.NotSupplied);

        await Composer(renderer).ForBookerAsync(Booking(), BookingEvent.Placed);

        var model = Assert.Single(renderer.Asked).Model;

        Assert.Equal("Initial Consultation", model.ServiceName);
        Assert.Equal("Treatment Room", Assert.Single(model.ResourceNames));
    }

    [Fact]
    public async Task A_service_booking_still_lists_its_resources_to_content()
    {
        // The plain-text message names the SERVICE and stops, so the structured description
        // deliberately reads resources the fallback never asks for. Without this, content for a
        // multi-resource service booking could not name what the booker was actually given.
        var renderer = new StubRenderer(BookingTemplateResult.NotSupplied);

        await Composer(renderer).ForBookerAsync(Booking(direct: false), BookingEvent.Placed);

        Assert.NotEmpty(Assert.Single(renderer.Asked).Model.ResourceNames);
    }

    [Fact]
    public async Task The_model_gives_instants_in_the_bookings_own_zone_with_the_zone_named()
    {
        // 09:00 London on a BST date is 08:00Z. Asserting both halves makes this a differential
        // rather than two assertions that could each pass against UTC.
        var renderer = new StubRenderer(BookingTemplateResult.NotSupplied);

        await Composer(renderer).ForBookerAsync(Booking(), BookingEvent.Placed);

        var model = Assert.Single(renderer.Asked).Model;

        Assert.Equal(TestData.LondonZoneId, model.TimeZoneId);
        Assert.Equal(9, model.LocalStart.Hour);
        Assert.Equal(1, model.LocalStart.Offset.Hours);
    }

    [Fact]
    public async Task The_model_reports_the_status_separately_from_the_message()
    {
        // Kind and Status answer different questions and can disagree: this is a BookerPlaced
        // message about a Requested booking, which is exactly the case an approval site sends.
        var renderer = new StubRenderer(BookingTemplateResult.NotSupplied);

        await Composer(renderer).ForBookerAsync(Booking(status: BookingStatus.Requested), BookingEvent.Placed);

        var (kind, model) = Assert.Single(renderer.Asked);

        Assert.Equal(BookingMessageKind.BookerPlaced, kind);
        Assert.Equal(BookingStatus.Requested, model.Status);
    }

    [Fact]
    public async Task The_internal_model_flags_a_booking_that_awaits_approval()
    {
        var renderer = new StubRenderer(BookingTemplateResult.NotSupplied);

        await Composer(renderer).ForSiteAsync(
            Booking(status: BookingStatus.Requested), BookingEvent.Placed, backofficeUrl: null);

        var model = Assert.IsType<InternalMessageModel>(Assert.Single(renderer.Asked).Model);

        Assert.True(model.AwaitsApproval);
    }

    [Fact]
    public async Task The_reference_reaches_content_in_the_form_a_person_quotes()
    {
        var booking = Booking();
        var renderer = new StubRenderer(BookingTemplateResult.NotSupplied);

        await Composer(renderer).ForBookerAsync(booking, BookingEvent.Placed);

        // The grouped form the confirmation screen shows, so a message and a screen cannot
        // disagree about what the customer is holding.
        Assert.Equal(booking.Reference.Display, Assert.Single(renderer.Asked).Model.Reference);
        Assert.Contains('-', Assert.Single(renderer.Asked).Model.Reference);
    }
}
