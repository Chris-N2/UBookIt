using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UBookIt.Core.Bookings;
using UBookIt.Core.Notifications;
using UBookIt.Web.Emails;

namespace UBookIt.Tests.Rendering;

/// <summary>
/// The Razor renderer: what it finds, what it does when it cannot, and — the assertion this
/// whole design was shaped around — that it renders with no ambient web request.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every test here builds the service provider itself and never installs an
/// <see cref="IHttpContextAccessor"/>.</b> That is not incidental tidiness: messages are composed
/// from work that has no request, so a renderer that quietly depended on one would pass every
/// test written inside a controller and fail in the retention sweep, which is the least
/// observable place in the product to fail. If this file ever needs an accessor to make a test
/// pass, the renderer has regressed.
/// </para>
/// <para>
/// Templates come from a precompiled fixture assembly at the real convention path, so what is
/// measured is discovery rather than a view handed over by the test.
/// </para>
/// </remarks>
public class EmailTemplateRenderingTests
{
    /// <summary>A provider with the view engine and the fixture's compiled views, and nothing else.</summary>
    private static (RazorBookingTemplateRenderer Renderer, IServiceProvider Services) Rig()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ILoggerFactory>(new LoggerFactory());
        services.AddLogging();

        var listener = new DiagnosticListener("UBookIt.Tests.Rendering.Emails");
        services.AddSingleton<DiagnosticSource>(listener);
        services.AddSingleton(listener);
        services.AddSingleton<IWebHostEnvironment>(new EmailRenderingEnvironment());

        services
            .AddMvcCore()
            .AddRazorViewEngine()
            .ConfigureApplicationPartManager(parts =>
                parts.ApplicationParts.Add(
                    new CompiledRazorAssemblyPart(typeof(UBookIt.Tests.ThemeFixture.ThemeFixture).Assembly)));

        var provider = services.BuildServiceProvider();

        var renderer = new RazorBookingTemplateRenderer(
            provider.GetRequiredService<IRazorViewEngine>(),
            provider.GetRequiredService<ITempDataProvider>(),
            provider,
            NullLogger<RazorBookingTemplateRenderer>.Instance);

        return (renderer, provider);
    }

    /// <summary>
    /// A content root with no files, so nothing can be read from disk. The templates under test
    /// are compiled into the fixture assembly, and this makes that structural rather than
    /// incidental — the same claim the theme rig makes about the shipped views.
    /// </summary>
    private sealed class EmailRenderingEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = string.Empty;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string ApplicationName { get; set; } = "UBookIt.Tests.Rendering";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = string.Empty;

        public string EnvironmentName { get; set; } = "Test";
    }

    private static BookerMessageModel Booker(BookingMessageKind kind) => new()
    {
        Kind = kind,
        Status = BookingStatus.Confirmed,
        Reference = "7QX4-M2NP",
        ServiceName = "Initial Consultation",
        ResourceNames = ["Treatment Room"],
        LocalStart = new DateTimeOffset(2026, 9, 15, 9, 0, 0, TimeSpan.FromHours(1)),
        LocalEnd = new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.FromHours(1)),
        TimeZoneId = "Europe/London",
        BookerName = "Ada Lovelace",
        BookerEmail = "ada@example.com",
    };

    private static InternalMessageModel Internal(bool awaitsApproval = false) => new()
    {
        Kind = BookingMessageKind.InternalPlaced,
        Status = awaitsApproval ? BookingStatus.Requested : BookingStatus.Confirmed,
        Reference = "7QX4-M2NP",
        ServiceName = "Initial Consultation",
        ResourceNames = ["Treatment Room", "Ada"],
        LocalStart = new DateTimeOffset(2026, 9, 15, 9, 0, 0, TimeSpan.FromHours(1)),
        LocalEnd = new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.FromHours(1)),
        TimeZoneId = "Europe/London",
        AwaitsApproval = awaitsApproval,
    };

    // ---- the requirement that would otherwise fail only in a background job ----------------

    [Fact]
    public async Task It_renders_with_no_ambient_request()
    {
        // THE TEST THIS FILE EXISTS FOR. The provider below has no IHttpContextAccessor and no
        // request of any kind; the renderer builds its own context. A renderer that reached for
        // an ambient request would throw here and work perfectly in a controller.
        var (renderer, services) = Rig();

        Assert.Null(services.GetService<IHttpContextAccessor>());

        var result = await renderer.RenderAsync(
            BookingMessageKind.BookerPlaced, Booker(BookingMessageKind.BookerPlaced));

        Assert.Equal(BookingTemplateOutcome.Rendered, result.Outcome);
        Assert.Contains("Ada Lovelace", result.Body!, StringComparison.Ordinal);
        Assert.Contains("7QX4-M2NP", result.Body!, StringComparison.Ordinal);
    }

    // ---- the three outcomes ----------------------------------------------------------------

    [Fact]
    public async Task A_message_with_no_template_is_not_supplied_rather_than_an_error()
    {
        // No InternalCancelled.cshtml exists in the fixture. This is the ordinary state and must
        // be reported as such, not as a failure and certainly not as an exception.
        var (renderer, _) = Rig();

        var result = await renderer.RenderAsync(
            BookingMessageKind.InternalCancelled, Internal());

        Assert.Equal(BookingTemplateOutcome.NotSupplied, result.Outcome);
        Assert.Null(result.Body);
    }

    [Fact]
    public async Task A_template_that_throws_is_failed_and_the_exception_does_not_escape()
    {
        // Broken must be distinguishable from absent — that distinction is the reason the
        // outcome is an enum of three rather than a nullable string. And nothing may escape:
        // the caller has a stored booking and a message that must still go out.
        var (renderer, _) = Rig();

        var result = await renderer.RenderAsync(
            BookingMessageKind.BookerCancelled, Booker(BookingMessageKind.BookerCancelled));

        Assert.Equal(BookingTemplateOutcome.Failed, result.Outcome);
        Assert.Null(result.Body);
    }

    [Fact]
    public async Task Failed_and_not_supplied_are_not_the_same_answer()
    {
        // Asserted as a differential rather than as two separate facts: the defect being
        // prevented is the two collapsing into one, which two independent assertions of the
        // right value would still pass if a later refactor mapped both to the same member.
        var (renderer, _) = Rig();

        var broken = await renderer.RenderAsync(
            BookingMessageKind.BookerCancelled, Booker(BookingMessageKind.BookerCancelled));
        var absent = await renderer.RenderAsync(
            BookingMessageKind.InternalCancelled, Internal());

        Assert.NotEqual(absent.Outcome, broken.Outcome);
    }

    // ---- what a template may state -----------------------------------------------------------

    [Fact]
    public async Task A_template_states_its_subject_and_content_type_through_typed_members()
    {
        var (renderer, _) = Rig();

        var result = await renderer.RenderAsync(
            BookingMessageKind.BookerConfirmed, Booker(BookingMessageKind.BookerConfirmed));

        Assert.Equal("Your appointment at Acme", result.Subject);
        Assert.True(result.IsHtml);
        Assert.Contains("<p>", result.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_template_that_states_nothing_reports_nothing()
    {
        // The composer turns a null subject into "keep ours"; the renderer's job is only to
        // report that the template said nothing, rather than inventing a subject of its own.
        var (renderer, _) = Rig();

        var result = await renderer.RenderAsync(
            BookingMessageKind.BookerPlaced, Booker(BookingMessageKind.BookerPlaced));

        Assert.Null(result.Subject);
        Assert.False(result.IsHtml);
    }

    // ---- the model reaches the template ------------------------------------------------------

    [Fact]
    public async Task A_template_can_enumerate_what_a_service_resolved_to()
    {
        // The capability Razor was chosen for. A joined string could not produce this, and it is
        // why the models publish a collection.
        var (renderer, _) = Rig();

        var result = await renderer.RenderAsync(BookingMessageKind.InternalPlaced, Internal());

        Assert.Equal(BookingTemplateOutcome.Rendered, result.Outcome);
        Assert.Contains("- Treatment Room", result.Body!, StringComparison.Ordinal);
        Assert.Contains("- Ada", result.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_template_sees_the_zone_the_booking_was_placed_against()
    {
        var (renderer, _) = Rig();

        var result = await renderer.RenderAsync(
            BookingMessageKind.BookerConfirmed, Booker(BookingMessageKind.BookerConfirmed));

        // 09:00 as the booking records it, not 08:00 as UTC would render it.
        Assert.Contains("09:00 (Europe/London)", result.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_internal_template_renders_the_approval_flag()
    {
        var (renderer, _) = Rig();

        var awaiting = await renderer.RenderAsync(
            BookingMessageKind.InternalPlaced, Internal(awaitsApproval: true));
        var settled = await renderer.RenderAsync(
            BookingMessageKind.InternalPlaced, Internal(awaitsApproval: false));

        Assert.Contains("awaiting approval", awaiting.Body!, StringComparison.Ordinal);
        Assert.DoesNotContain("awaiting approval", settled.Body!, StringComparison.Ordinal);
    }

    // ---- what the boot check reports ---------------------------------------------------------

    [Fact]
    public void The_boot_check_reports_supplied_and_unsupplied_by_asking_the_view_engine()
    {
        var (renderer, _) = Rig();

        var report = new UBookItEmailTemplateBootCheck(
            renderer, NullLogger<UBookItEmailTemplateBootCheck>.Instance).Run();

        // Every message is accounted for, not only the ones that were found — an author who
        // misspelled a file needs to see it listed as unsupplied.
        Assert.Equal(Enum.GetValues<BookingMessageKind>().Length, report.Count);

        Assert.True(report.Single(r => r.Kind == BookingMessageKind.BookerPlaced).Supplied);
        Assert.False(report.Single(r => r.Kind == BookingMessageKind.InternalCancelled).Supplied);
    }

    [Fact]
    public void The_boot_check_reports_a_broken_template_as_supplied()
    {
        // It asks whether content EXISTS, not whether it works — and that is right: a template
        // that throws is supplied, and reporting it as absent would tell an author their file
        // was not found when the problem is inside it. The failure surfaces when it renders.
        var (renderer, _) = Rig();

        var report = new UBookItEmailTemplateBootCheck(
            renderer, NullLogger<UBookItEmailTemplateBootCheck>.Instance).Run();

        Assert.True(report.Single(r => r.Kind == BookingMessageKind.BookerCancelled).Supplied);
    }
}
