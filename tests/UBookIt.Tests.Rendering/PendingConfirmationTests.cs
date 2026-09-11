using UBookIt.Tests.Rendering.Support;

namespace UBookIt.Tests.Rendering;

/// <summary>
/// The confirmation page's wording follows the booking's status (default-frontend spec,
/// approval-decline): a booking placed under <c>AutoConfirm</c> off renders as received and
/// awaiting confirmation, and nothing on that page claims it is confirmed.
/// </summary>
/// <remarks>
/// <para>
/// The absence half carries the weight here, and its needle is the CLAIM — "is confirmed",
/// "Booking confirmed" — not the sentence the pending branch happens to emit. The pending
/// wording is written to stay out of the needle's way ("not confirmed yet", "confirm or
/// decline"), so a hit is a genuine contradiction rather than a phrasing accident. Markup
/// cannot wrap a phrase across an element boundary invisibly the way documentation wraps
/// across lines, but attribute text and element text are both in the haystack, so a claim
/// surviving in <c>aria-label</c> alone still fails.
/// </para>
/// <para>
/// The generic rules elsewhere in this suite prove the branch is live (Rule 3) and the member
/// matters (Rule 2); this file pins WHAT each arm says, which those rules cannot.
/// </para>
/// </remarks>
public class PendingConfirmationTests
{
    private readonly ViewRenderer _renderer = new();

    /// <summary>
    /// Whitespace runs collapsed to single spaces before any phrase is looked for. Razor
    /// emits the view source's own line breaks and indentation, so a multi-word phrase can
    /// arrive wrapped — and a raw substring match against wrapped text is the exact defect
    /// the booking-emails QA rounds spent three rounds on. Both halves of every assertion
    /// here go through this, the absence half included: a claim re-added wrapped must fail.
    /// </summary>
    private static string Flat(string html)
        => System.Text.RegularExpressions.Regex.Replace(html, @"\s+", " ");

    public static TheoryData<string, string> ConfirmationViews() => new()
    {
        // Both entry points for the direct flow render one view; both are listed so a future
        // divergence fails here rather than shipping half-corrected.
        { "~/Views/Shared/Components/Booking/Confirmation.cshtml", "direct" },
        { "~/Views/Shared/Components/BookingFlow/Confirmation.cshtml", "flow" },
    };

    private static object Model(string kind, bool pending)
        => kind == "service"
            ? ViewFixtures.For(ViewInventory.ServiceConfirmation)
                .Single(c => c.State == (pending ? "pending" : "one resource")).Model
            : ViewFixtures.For("~/Views/Shared/Components/Booking/Confirmation.cshtml")
                .Single(c => c.State == (pending ? "pending" : "with phone")).Model;

    [Theory]
    [MemberData(nameof(ConfirmationViews))]
    public async Task A_pending_direct_booking_reads_as_received_not_confirmed(string view, string entry)
    {
        Assert.NotNull(entry);
        var html = Flat(await _renderer.RenderAsync(view, Model("direct", pending: true)));

        Assert.Contains("Booking request received", html, StringComparison.Ordinal);
        Assert.Contains("not confirmed yet", html, StringComparison.Ordinal);

        // The claims, not the sentences: any statement that this booking IS confirmed is
        // false on this page, wherever it appears — heading, lead, or accessible name.
        Assert.DoesNotContain("Booking confirmed", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("is confirmed", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_pending_service_booking_reads_as_received_not_confirmed()
    {
        var html = Flat(await _renderer.RenderAsync(
            ViewInventory.ServiceConfirmation, Model("service", pending: true)));

        Assert.Contains("Booking request received", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Booking confirmed", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("is confirmed", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(ConfirmationViews))]
    public async Task A_confirmed_booking_still_reads_as_confirmed(string view, string entry)
    {
        Assert.NotNull(entry);
        var html = Flat(await _renderer.RenderAsync(view, Model("direct", pending: false)));

        Assert.Contains("Booking confirmed", html, StringComparison.Ordinal);
        Assert.DoesNotContain("request received", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_accessible_name_matches_the_heading_in_both_states()
    {
        // One string feeds both in the view, so this pins that neither state leaves the
        // region announced as something the page does not say.
        var pending = await _renderer.RenderAsync(
            "~/Views/Shared/Components/Booking/Confirmation.cshtml", Model("direct", pending: true));
        var confirmed = await _renderer.RenderAsync(
            "~/Views/Shared/Components/Booking/Confirmation.cshtml", Model("direct", pending: false));

        Assert.Contains("aria-label=\"Booking request received\"", pending, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Booking confirmed\"", confirmed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_pending_page_still_carries_everything_the_visitor_needs()
    {
        // The reference matters MORE to a person whose booking is pending — it is what they
        // will quote when they chase it. Same details, both states.
        var html = await _renderer.RenderAsync(
            "~/Views/Shared/Components/Booking/Confirmation.cshtml", Model("direct", pending: true));

        Assert.Contains("7QX4-M2NP", html, StringComparison.Ordinal);
        Assert.Contains("Meeting Room A", html, StringComparison.Ordinal);
        Assert.Contains("Thursday 20 August 2026, 09:00", html, StringComparison.Ordinal);
        Assert.Contains("ada@example.com", html, StringComparison.Ordinal);
        Assert.Contains("Ada Lovelace", html, StringComparison.Ordinal);
    }
}
