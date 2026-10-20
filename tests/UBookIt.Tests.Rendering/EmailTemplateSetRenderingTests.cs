using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using UBookIt.Core.Notifications;
using UBookIt.Web.Emails;

namespace UBookIt.Tests.Rendering;

/// <summary>
/// A service's template set, looked in before the view for every service (service-email-templates:
/// the <c>email-templates</c> delta's "A service's template set is consulted before the content for
/// every service"; design 4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Real views, discovered.</b> The fixture assembly holds set <c>fixture-set</c> with three
/// views: <c>BookerPlaced</c> and <c>InternalPlaced</c>, which render, and <c>BookerMoved</c>, which
/// throws — beside a working <c>BookerMoved</c> for every service, which is what makes the
/// stop-on-failure rule testable. It has no <c>BookerConfirmed</c>, so that message falls through
/// to the view for every service, and nobody supplies <c>InternalCancelled</c>.
/// </para>
/// <para>
/// The fixture is a compiled Razor assembly and the rig's content root is empty, so every set view
/// found here was found inside an RCL — the "Views in an RCL assembly" risk in design.md.
/// </para>
/// </remarks>
public class EmailTemplateSetRenderingTests
{
    private const string Set = "fixture-set";

    private static BookerMessageModel Booker(BookingMessageKind kind, string? set = Set)
        => EmailTemplateRenderingTests.Booker(kind) with { TemplateSet = set };

    /// <summary>Records every path the renderer asks the view engine for, and delegates.</summary>
    private sealed class RecordingViewEngine(IRazorViewEngine inner) : IRazorViewEngine
    {
        public List<string> Paths { get; } = [];

        public ViewEngineResult FindView(ActionContext context, string viewName, bool isMainPage)
        {
            Paths.Add(viewName);
            return inner.FindView(context, viewName, isMainPage);
        }

        public ViewEngineResult GetView(string? executingFilePath, string viewPath, bool isMainPage)
        {
            Paths.Add(viewPath);
            return inner.GetView(executingFilePath, viewPath, isMainPage);
        }

        public RazorPageResult FindPage(ActionContext context, string pageName)
        {
            Paths.Add(pageName);
            return inner.FindPage(context, pageName);
        }

        public RazorPageResult GetPage(string executingFilePath, string pagePath)
        {
            Paths.Add(pagePath);
            return inner.GetPage(executingFilePath, pagePath);
        }

        public string? GetAbsolutePath(string? executingFilePath, string? pagePath)
            => inner.GetAbsolutePath(executingFilePath, pagePath);
    }

    // ── The order ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_sets_view_is_used_instead_of_the_view_for_every_service()
    {
        var (renderer, _) = EmailTemplateRenderingTests.Rig();

        var result = await renderer.RenderAsync(BookingMessageKind.BookerPlaced, Booker(BookingMessageKind.BookerPlaced));

        Assert.Equal(BookingTemplateOutcome.Rendered, result.Outcome);
        Assert.Contains("SET-VIEW fixture-set BookerPlaced for 7QX4-M2NP (set fixture-set)", result.Body!, StringComparison.Ordinal);

        // The view for every service exists for this message and was not the one rendered.
        Assert.DoesNotContain("Dear Ada Lovelace", result.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_message_the_set_does_not_supply_uses_the_view_for_every_service()
    {
        var (renderer, _) = EmailTemplateRenderingTests.Rig();

        var result = await renderer.RenderAsync(
            BookingMessageKind.BookerConfirmed, Booker(BookingMessageKind.BookerConfirmed));

        Assert.Equal(BookingTemplateOutcome.Rendered, result.Outcome);
        Assert.Equal("Your appointment at Acme", result.Subject);
        Assert.DoesNotContain("SET-VIEW", result.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_message_supplied_nowhere_is_not_supplied()
    {
        var (renderer, _) = EmailTemplateRenderingTests.Rig();

        var result = await renderer.RenderAsync(
            BookingMessageKind.InternalCancelled,
            EmailTemplateRenderingTests.Internal() with { Kind = BookingMessageKind.InternalCancelled, TemplateSet = Set });

        Assert.Equal(BookingTemplateOutcome.NotSupplied, result.Outcome);
        Assert.Null(result.Body);
    }

    [Fact]
    public async Task A_failing_set_view_is_failed_and_does_not_fall_through_to_the_view_for_every_service()
    {
        var logs = new Logs();
        var (renderer, _) = EmailTemplateRenderingTests.Rig(logger: logs.CreateLogger());

        var failed = await renderer.RenderAsync(BookingMessageKind.BookerMoved, Booker(BookingMessageKind.BookerMoved));

        Assert.Equal(BookingTemplateOutcome.Failed, failed.Outcome);
        Assert.Null(failed.Body);

        // Reported against the SET's path, so an author is sent to the file that broke.
        var entry = Assert.Single(logs.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains("~/Views/Partials/UBookIt/Emails/fixture-set/BookerMoved.cshtml", entry.Message, StringComparison.Ordinal);

        // ANTI-VACUITY: the view for every service renders for the same message without a set, so
        // the Failed above is the rule at work and not a fixture with nothing to fall through to.
        var general = await renderer.RenderAsync(
            BookingMessageKind.BookerMoved,
            Booker(BookingMessageKind.BookerMoved, set: null) with
            {
                PreviousLocalStart = new DateTimeOffset(2026, 9, 15, 14, 0, 0, TimeSpan.FromHours(1)),
                PreviousLocalEnd = new DateTimeOffset(2026, 9, 15, 15, 0, 0, TimeSpan.FromHours(1)),
            });
        Assert.Equal(BookingTemplateOutcome.Rendered, general.Outcome);
    }

    [Fact]
    public async Task The_sites_own_message_is_resolved_in_the_set_with_the_model_that_has_no_booker()
    {
        // The set view is typed to InternalMessageModel; handing it any other model would throw
        // and come back Failed, so Rendered is the type check.
        var (renderer, _) = EmailTemplateRenderingTests.Rig();

        var result = await renderer.RenderAsync(
            BookingMessageKind.InternalPlaced, EmailTemplateRenderingTests.Internal() with { TemplateSet = Set });

        Assert.Equal(BookingTemplateOutcome.Rendered, result.Outcome);
        Assert.Contains("SET-VIEW fixture-set InternalPlaced for 7QX4-M2NP", result.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_model_with_no_set_renders_exactly_as_before()
    {
        var (renderer, _) = EmailTemplateRenderingTests.Rig();

        var result = await renderer.RenderAsync(
            BookingMessageKind.BookerPlaced, Booker(BookingMessageKind.BookerPlaced, set: null));

        Assert.Equal(BookingTemplateOutcome.Rendered, result.Outcome);
        Assert.Contains("Dear Ada Lovelace, your reference is 7QX4-M2NP.", result.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_set_with_no_folder_at_all_falls_through_to_the_view_for_every_service()
    {
        var (renderer, _) = EmailTemplateRenderingTests.Rig();

        var result = await renderer.RenderAsync(
            BookingMessageKind.BookerPlaced, Booker(BookingMessageKind.BookerPlaced, set: "no-such-set"));

        Assert.Equal(BookingTemplateOutcome.Rendered, result.Outcome);
        Assert.Contains("Dear Ada Lovelace", result.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_set_is_looked_in_first_and_then_the_folder_for_every_service()
    {
        RecordingViewEngine? engine = null;
        var (renderer, _) = EmailTemplateRenderingTests.Rig(inner => engine = new RecordingViewEngine(inner));

        await renderer.RenderAsync(BookingMessageKind.BookerConfirmed, Booker(BookingMessageKind.BookerConfirmed));

        Assert.Equal(
            [
                "~/Views/Partials/UBookIt/Emails/fixture-set/BookerConfirmed.cshtml",
                "~/Views/Partials/UBookIt/Emails/BookerConfirmed.cshtml",
            ],
            engine!.Paths.Take(2));
    }

    // ── A name that is not a set name is never a path ─────────────────────────────────────────

    [Theory]
    [InlineData("../partials")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("Remote")]
    [InlineData(" fixture-set")]
    [InlineData("")]
    public async Task An_invalid_set_on_the_model_is_failed_with_no_lookup(string set)
    {
        RecordingViewEngine? engine = null;
        var logs = new Logs();
        var (renderer, _) = EmailTemplateRenderingTests.Rig(inner => engine = new RecordingViewEngine(inner), logs.CreateLogger());

        var result = await renderer.RenderAsync(BookingMessageKind.BookerPlaced, Booker(BookingMessageKind.BookerPlaced, set));

        Assert.Equal(BookingTemplateOutcome.Failed, result.Outcome);
        Assert.Empty(engine!.Paths);
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("not a valid set name", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("../partials")]
    [InlineData("Remote")]
    [InlineData("")]
    public void The_set_path_and_the_set_lookup_refuse_an_invalid_name(string set)
    {
        var (renderer, _) = EmailTemplateRenderingTests.Rig();

        Assert.Throws<ArgumentException>(() => RazorBookingTemplateRenderer.SetPathFor(set, BookingMessageKind.BookerPlaced));
        Assert.Throws<ArgumentException>(() => renderer.IsSuppliedInSet(set, BookingMessageKind.BookerPlaced));
    }

    [Fact]
    public void The_set_path_is_the_set_as_a_folder_beneath_the_email_folder()
        => Assert.Equal(
            "~/Views/Partials/UBookIt/Emails/remote/BookerPlaced.cshtml",
            RazorBookingTemplateRenderer.SetPathFor("remote", BookingMessageKind.BookerPlaced));

    // ── Supplied in a set, asked of the view engine ──────────────────────────────────────────

    [Fact]
    public void A_set_in_a_compiled_assembly_is_found_message_by_message()
    {
        var (renderer, _) = EmailTemplateRenderingTests.Rig();

        Assert.True(renderer.IsSuppliedInSet(Set, BookingMessageKind.BookerPlaced));
        Assert.True(renderer.IsSuppliedInSet(Set, BookingMessageKind.InternalPlaced));

        // A broken view is supplied — it exists — exactly as IsSupplied reports one.
        Assert.True(renderer.IsSuppliedInSet(Set, BookingMessageKind.BookerMoved));

        Assert.False(renderer.IsSuppliedInSet(Set, BookingMessageKind.BookerConfirmed));
        Assert.False(renderer.IsSuppliedInSet("no-such-set", BookingMessageKind.BookerPlaced));
    }

    /// <summary>Captures each line with its level.</summary>
    internal sealed class Logs
    {
        private readonly Lock _gate = new();
        private readonly List<(LogLevel Level, string Message)> _entries = [];

        public IReadOnlyList<(LogLevel Level, string Message)> Entries
        {
            get
            {
                lock (_gate)
                {
                    return [.. _entries];
                }
            }
        }

        public ILogger<T> CreateLogger<T>() => new Logger<T>(this);

        public ILogger<RazorBookingTemplateRenderer> CreateLogger() => CreateLogger<RazorBookingTemplateRenderer>();

        private sealed class Logger<T>(Logs owner) : ILogger<T>
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (owner._gate)
                {
                    owner._entries.Add((logLevel, formatter(state, exception)));
                }
            }
        }
    }
}
