namespace UBookIt.Core.Bookings;

/// <summary>
/// Supplies a fresh <see cref="BookingReference"/>.
/// <para>
/// A port rather than a static helper, and the reason is the one constraint
/// <c>UBookIt.Core</c> is built around: it carries <b>no package reference of any kind</b> and
/// its behaviour is a pure function of its inputs. A random source inside the domain would put
/// non-determinism into the one assembly that has none and make "which reference did this
/// produce" untestable.
/// </para>
/// <para>
/// It also makes the failure case reachable. A test can supply a factory that returns a
/// reference already in use, which is the only practical way to exercise the collision path —
/// at 28^8 values, waiting for a real collision is not a test strategy.
/// </para>
/// <para>
/// This mirrors how identity already works here: <see cref="Booking.Create"/> takes its id
/// from its caller rather than generating one.
/// </para>
/// </summary>
public interface IBookingReferenceFactory
{
    /// <summary>Returns a reference. Successive calls SHOULD return different values.</summary>
    /// <remarks>
    /// "Should" rather than "must": uniqueness is enforced by the store, because a factory
    /// cannot know what is already persisted and a check followed by a write is a race. What
    /// this promises is a well-formed reference drawn unpredictably, not a unique one.
    /// </remarks>
    BookingReference Next();
}
