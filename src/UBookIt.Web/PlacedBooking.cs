using UBookIt.Core.Bookings;

namespace UBookIt.Web;

/// <summary>
/// Reading the booker of a booking that has just been placed in the current request.
/// </summary>
/// <remarks>
/// <para>
/// A booker carries contact details or is erased, and the compiler makes every reader
/// establish which. The three places this package echoes a placement back — the two
/// confirmation pages and the delivery API's placement response — are all handed a booking
/// created moments earlier in the same request, and placement can only ever produce a
/// booker carrying details. The erased state is unreachable there.
/// </para>
/// <para>
/// <b>Stated once, as a check that throws, rather than suppressed with <c>!</c> at each
/// site.</b> A suppression asserts the same thing silently and, on the day it stops being
/// true, produces a null reference several layers away from the mistake — on a page, in
/// front of a customer. This fails where the assumption lives and says what the assumption
/// was.
/// </para>
/// <para>
/// <b>Deliberately internal to <c>UBookIt.Web</c>.</b> "Give me the contact details and
/// throw if there are none" is exactly the general-purpose escape hatch that would undo the
/// reason <see cref="Booker.Contact"/> is nullable, so it is not published, not put in Core,
/// and not offered to any caller whose booking did not come from a placement it just
/// performed.
/// </para>
/// </remarks>
internal static class PlacedBooking
{
    /// <summary>
    /// The contact details of a booking placed in this request.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The booker has been erased, which means the booking did not come from the placement
    /// this caller believes it did.
    /// </exception>
    public static BookerContact Contact(Booking booking)
        => booking.Booker.Contact
           ?? throw new InvalidOperationException(
               $"Booking {booking.Reference.Value} has an erased booker, so it cannot be "
               + "echoed as a placement. This path renders a booking placed in the current "
               + "request, which always carries contact details.");
}
