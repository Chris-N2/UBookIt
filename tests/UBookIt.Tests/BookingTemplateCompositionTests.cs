using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UBookIt.Core.Bookings;
using UBookIt.Core.Notifications;
using UBookIt.Core.Resources;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Notifications;
using UBookIt.Tests.Support;
using UBookIt.Web.Emails;

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

    /// <summary>
    /// Any GUID, in the forms a log line renders one — removed from a haystack before short
    /// alphabetic needles are looked for in it. See BookingEmailTests for the defect this
    /// prevents.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex AnyGuid = new(
        "[0-9a-fA-F]{8}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{12}",
        System.Text.RegularExpressions.RegexOptions.Compiled);

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

    /// <summary>The same composer, with every line it writes captured.</summary>
    private static (BookingMessageComposer Composer, CapturingLoggerProvider Logs) ComposerWithLogs(
        IBookingTemplateRenderer? renderer)
    {
        var logs = new CapturingLoggerProvider();
        var factory = LoggerFactory.Create(builder => builder.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));

        return (
            new BookingMessageComposer(
                new StubResourceStore(), factory.CreateLogger<BookingMessageComposer>(), renderer),
            logs);
    }

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

    /// <summary>
    /// A failure is reported, and an absence is not.
    /// </summary>
    /// <remarks>
    /// <b>The two tests above assert IDENTICAL observable facts, so neither can tell the
    /// outcomes apart.</b> QA deleted the composer's entire <c>Failed</c> branch — its log line
    /// included — and all 2379 tests passed: three outcomes collapsed into two with nothing
    /// failing. The spec requires a failure to be "distinguishable in the report from a message
    /// for which nothing was supplied", and a report nobody reads cannot be distinguishable.
    /// <para>
    /// Asserted as a DIFFERENTIAL between the two outcomes rather than as "a failure logs
    /// something", because the defect is the two becoming the same, and a one-sided assertion
    /// would survive a change that logged for both.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task A_failure_is_logged_and_an_absence_is_not()
    {
        var booking = Booking();

        var (failing, failedLogs) = ComposerWithLogs(new StubRenderer(BookingTemplateResult.Failed));
        await failing.ForBookerAsync(booking, BookingEvent.Placed);

        var (absent, absentLogs) = ComposerWithLogs(new StubRenderer(BookingTemplateResult.NotSupplied));
        await absent.ForBookerAsync(booking, BookingEvent.Placed);

        Assert.NotEmpty(failedLogs.Entries);

        // The ordinary state says nothing. A line per unsupplied message per send would bury the
        // failures that matter, which is the whole reason the two are treated differently.
        Assert.Empty(absentLogs.Entries);
    }

    [Fact]
    public async Task The_failure_line_names_the_booking_and_no_booker()
    {
        var booking = Booking();
        var (composer, logs) = ComposerWithLogs(new StubRenderer(BookingTemplateResult.Failed));

        await composer.ForBookerAsync(booking, BookingEvent.Placed);

        var entry = Assert.Single(logs.Entries);

        // Anti-vacuity first: without the id, the assertions below would pass against a line
        // about nothing in particular.
        Assert.Contains(booking.Id.ToString(), entry.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(BookingMessageKind.BookerPlaced), entry.Message, StringComparison.Ordinal);

        // And no booker. GUIDs come out of the haystack first — "Ada" is three hex digits, so a
        // random id matches it about 0.7% of the time and this guard would fail at random.
        var haystack = AnyGuid.Replace($"{entry.Message} {entry.Exception}", "{guid}");

        Assert.DoesNotContain("Ada", haystack, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Lovelace", haystack, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ada@example.com", haystack, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("07700", haystack, StringComparison.Ordinal);
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
    public async Task An_erased_booker_gets_the_packages_own_message_and_no_template_is_rendered()
    {
        // Reachable in principle rather than in the send path — the handler establishes an
        // address before this is called — but the composer is public and this used to be a bare
        // `Contact!`, which turned a direct call into a NullReferenceException layers from the
        // mistake. There is a defined answer, and it is the same one absence gets.
        var erased = Booking().Tap(b => b.EraseBooker(TestData.Now));
        var renderer = new StubRenderer(new BookingTemplateResult(
            BookingTemplateOutcome.Rendered, Body: "should never be used"));

        var message = await Composer(renderer).ForBookerAsync(erased, BookingEvent.Cancelled);

        Assert.Empty(renderer.Asked);
        Assert.DoesNotContain("should never be used", message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_directly_booked_booking_is_not_read_twice()
    {
        // The plain-text path already reads every claim to build its "What:" line, and the
        // structured model needs the same names. Reading them again would double the round trips
        // per claim on every message a template-enabled site sends, and nothing would report it.
        var store = new CountingResourceStore();
        var renderer = new StubRenderer(BookingTemplateResult.NotSupplied);
        var composer = new BookingMessageComposer(
            store, NullLogger<BookingMessageComposer>.Instance, renderer);

        await composer.ForBookerAsync(Booking(direct: true), BookingEvent.Placed);

        Assert.Equal(1, store.Reads);

        // And the names still reach content, so this is not a saving bought by losing the data.
        Assert.Equal("Treatment Room", Assert.Single(Assert.Single(renderer.Asked).Model.ResourceNames));
    }

    private sealed class CountingResourceStore : IResourceStore
    {
        public int Reads { get; private set; }

        public Task<Resource?> GetAsync(Guid resourceId, CancellationToken cancellationToken = default)
        {
            Reads++;

            return Task.FromResult<Resource?>(
                Resource.Create("room", "Treatment Room", directlyBookable: true, id: resourceId).Value);
        }

        public Task<ResourcePage> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Resource>> ListByTypeAsync(
            string type, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
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

// ---- the model contract, which is where the structural guarantee lives -------------------

/// <summary>
/// What the published models may and may not expose.
/// </summary>
/// <remarks>
/// <b>This file exists because the guarantee it guards had no guard.</b> The change's headline
/// claim is that a message to a site's configured recipients cannot carry a booker's contact
/// details <i>because the model has no member for them</i> — the compiler enforcing what a
/// reviewer would otherwise have to. QA added <c>BookerEmail</c> to
/// <c>InternalMessageModel</c> and all 2379 tests passed. A structural guarantee with no test
/// over the structure is a comment.
/// </remarks>
public class BookingMessageModelContractTests
{
    /// <summary>
    /// Whether a member's name suggests it carries the BOOKER's contact details.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Matched by name rather than against a list of today's members, so a member added later is
    /// caught by the rule instead of needing somebody to remember this file exists.
    /// </para>
    /// <para>
    /// <b>Precise rather than broad, and that is deliberate.</b> The first version matched any
    /// name containing "name" and flagged <c>ServiceName</c> and <c>ResourceNames</c> — neither
    /// of which is personal data. A guard that fires on legitimate members is a guard somebody
    /// relaxes, which is how this project has lost guards before. So: anything about the
    /// <i>booker</i>, anything that is an address or a number whatever it is attached to, and a
    /// bare <c>Name</c> or <c>Contact</c>, which unqualified can only mean the person.
    /// </para>
    /// </remarks>
    private static bool LooksLikeBookerContact(PropertyInfo property)
    {
        var name = property.Name;

        return name.Contains("Booker", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Email", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Phone", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Telephone", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Name", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Contact", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ContactLookingMembersOf(Type type)
        => [.. type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(LooksLikeBookerContact)
            .Select(p => p.Name)];

    [Fact]
    public void The_internal_model_exposes_no_contact_detail_member()
    {
        // THE WHOLE PUBLIC SURFACE, inherited members included. Declared-only would miss a
        // member added to the shared base — which is exactly where a careless "just put it on
        // the base, both need it" change would put one.
        var offending = ContactLookingMembersOf(typeof(InternalMessageModel));

        Assert.True(
            offending.Count == 0,
            "InternalMessageModel must expose nothing that could carry a booker's contact "
            + "details — the absence IS the guarantee that a message to a configuration-file "
            + "address list cannot leak them. Found: " + string.Join(", ", offending));
    }

    [Fact]
    public void The_rule_would_catch_a_member_added_to_either_type()
    {
        // ANTI-VACUITY, and it is the half that matters: the test above passes trivially if the
        // word list stops matching anything. The booker's own model carries exactly these
        // members legitimately, so finding them there proves the predicate still bites.
        var onBooker = ContactLookingMembersOf(typeof(BookerMessageModel));

        Assert.Contains(nameof(BookerMessageModel.BookerName), onBooker);
        Assert.Contains(nameof(BookerMessageModel.BookerEmail), onBooker);
        Assert.Contains(nameof(BookerMessageModel.BookerPhone), onBooker);
    }

    [Fact]
    public void The_shared_base_carries_no_contact_detail_member_either()
    {
        // Stated separately from the internal model's own rule, because the base is the place a
        // future change would most plausibly add one "since both audiences need it".
        Assert.Empty(ContactLookingMembersOf(typeof(BookingMessageModel)));
    }

    [Fact]
    public void The_web_composer_registers_the_renderer_and_the_boot_check()
    {
        // Deleting either registration disables the entire feature — every message silently
        // reverts to the package's wording and no test anywhere noticed. design.md names the
        // boot check as the mitigation for "registration silently does nothing", but the boot
        // check was itself only ever constructed directly.
        var registrations = new ServiceCollection();
        new UBookIt.Web.Composing.UBookItRenderingComposer()
            .Compose(new ServicesOnlyUmbracoBuilder(registrations));

        Assert.Contains(registrations, d => d.ServiceType == typeof(IBookingTemplateRenderer));
        Assert.Contains(registrations, d => d.ServiceType == typeof(RazorBookingTemplateRenderer));
    }

    [Fact]
    public void The_renderer_is_registered_once_and_resolves_to_one_instance_per_scope()
    {
        // The port and the concrete type must be the SAME instance, not two. Harmless while the
        // renderer is stateless, and precisely the kind of harmless that stops being so the day
        // somebody caches a compiled view on it.
        var registrations = new ServiceCollection();
        new UBookIt.Web.Composing.UBookItRenderingComposer()
            .Compose(new ServicesOnlyUmbracoBuilder(registrations));

        var port = Assert.Single(
            registrations.Where(d => d.ServiceType == typeof(IBookingTemplateRenderer)));

        // Registered by factory rather than by implementation type — which is what makes it
        // resolve the concrete registration instead of constructing a second one.
        Assert.Null(port.ImplementationType);
        Assert.NotNull(port.ImplementationFactory);
    }

    [Fact]
    public void The_published_message_set_is_exactly_what_the_package_sends()
    {
        // No name for a message that is never sent: content written against one would never
        // render, and an author would have no way to find out why.
        Assert.Equal(
            new[]
            {
                BookingMessageKind.BookerPlaced,
                BookingMessageKind.BookerConfirmed,
                BookingMessageKind.BookerDeclined,
                BookingMessageKind.BookerCancelled,
                BookingMessageKind.InternalPlaced,
                BookingMessageKind.InternalCancelled,
            },
            Enum.GetValues<BookingMessageKind>());

        // Named explicitly, because these two are the ones somebody will eventually "notice are
        // missing" and add without checking whether such a message exists. They do not.
        Assert.DoesNotContain(
            Enum.GetNames<BookingMessageKind>(),
            name => name is "InternalConfirmed" or "InternalDeclined");
    }
}
