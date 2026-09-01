using UBookIt.Core.Bookings;

namespace UBookIt.Tests.Support;

/// <summary>
/// Booking references for tests that need one but are not about them.
/// <para>
/// Most of the suite constructs bookings to exercise something else entirely — status
/// transitions, conflicts, pool matching — and for those the reference is scenery. This gives
/// them a valid one without each test inventing an eight-character string and without any of
/// them depending on a particular value.
/// </para>
/// </summary>
public static class References
{
    /// <summary>A distinct, valid reference. Successive calls differ.</summary>
    public static BookingReference Any() => new RandomBookingReferenceFactory().Next();

    /// <summary>
    /// A specific reference, for the tests that ARE about references and need to assert on a
    /// known value. Throws if the text is not canonical, so a typo in a test fixture fails
    /// loudly rather than quietly testing a different string than the author intended.
    /// </summary>
    public static BookingReference Of(string canonical) => BookingReference.FromCanonical(canonical);
}

/// <summary>
/// A factory that hands out prepared references, then falls back to random ones.
/// <para>
/// The reason the generation seam exists. Uniqueness is enforced by the store, so the
/// behaviour worth testing is what placement does when a reference is already taken — and at
/// 28^8 values, waiting for a real collision is not a test strategy. This lets a test say
/// "return this taken reference twice, then a fresh one" and observe the retry.
/// </para>
/// </summary>
public sealed class ScriptedBookingReferenceFactory(params BookingReference[] scripted) : IBookingReferenceFactory
{
    private readonly Queue<BookingReference> _scripted = new(scripted);
    private readonly RandomBookingReferenceFactory _fallback = new();

    /// <summary>How many times a reference has been asked for.</summary>
    public int Calls { get; private set; }

    public BookingReference Next()
    {
        Calls++;

        return _scripted.Count > 0 ? _scripted.Dequeue() : _fallback.Next();
    }
}

/// <summary>Always returns the same reference — a broken generator, for the exhaustion case.</summary>
public sealed class FixedBookingReferenceFactory(BookingReference reference) : IBookingReferenceFactory
{
    public int Calls { get; private set; }

    public BookingReference Next()
    {
        Calls++;

        return reference;
    }
}
