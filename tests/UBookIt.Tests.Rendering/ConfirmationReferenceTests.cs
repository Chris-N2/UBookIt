using UBookIt.Tests.Rendering.Support;
using UBookIt.Web.Rendering;

namespace UBookIt.Tests.Rendering;

/// <summary>
/// What the confirmation prints under "Reference".
/// <para>
/// This exists because reverting the fix passed all 747 of the other rendering tests. The
/// whole change began with a shipped view rendering <c>@Model.BookingId</c> beneath a
/// <c>&lt;dt&gt;Reference&lt;/dt&gt;</c> — a label that was accurate and a value nobody could
/// use — and until this file, nothing in the suite would have noticed it coming back.
/// </para>
/// <para>
/// It asserts the negative as well as the positive. "The reference appears" is satisfied by a
/// page that prints both, which is not the fix: a visitor shown two identifiers has to guess
/// which one to quote.
/// </para>
/// </summary>
public class ConfirmationReferenceTests
{
    private readonly ViewRenderer _renderer = new();

    private const string Reference = "7QX4-M2NP";
    private const string BookingId = "00000000-0000-0000-0000-0000000000b1";
    private const string ServiceReference = "5KGT-BW9D";
    private const string ServiceBookingId = "00000000-0000-0000-0000-0000000000b2";

    [Fact]
    public async Task The_direct_confirmation_shows_the_reference_and_not_the_identifier()
    {
        var html = await _renderer.RenderAsync(
            "~/Views/Shared/Components/Booking/Confirmation.cshtml", ViewFixtures.ConfirmationModel());

        Assert.Contains(Reference, html, StringComparison.Ordinal);
        Assert.DoesNotContain(BookingId, html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_service_confirmation_shows_the_reference_and_not_the_identifier()
    {
        // Found by sweeping the shipped views rather than assumed: the service flow had its
        // own confirmation with the same defect, and fixing only the direct one would have
        // left half the product broken in exactly the way the change was written to fix.
        var html = await _renderer.RenderAsync(
            ViewInventory.ServiceConfirmation, ViewFixtures.ServiceConfirmationModel());

        Assert.Contains(ServiceReference, html, StringComparison.Ordinal);
        Assert.DoesNotContain(ServiceBookingId, html, StringComparison.OrdinalIgnoreCase);
    }
}
