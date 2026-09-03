namespace UBookIt.Core.Bookings;

/// <summary>
/// Supplies a fresh <see cref="BookingReference"/>.
/// <para>
/// A port rather than a static helper <b>so that the failure case is reachable</b>. Uniqueness
/// is enforced by the store, so the behaviour worth testing is what placement does when it
/// draws a reference that is already taken — and at 27^8 values, waiting for a real collision
/// is not a test strategy. A test points this at a known-duplicate value instead.
/// </para>
/// <para>
/// <b>It is not here to keep Core deterministic.</b> An earlier version of this comment said so,
/// and it was false: <see cref="BookingService"/> has called <c>Guid.NewGuid()</c> inline since
/// bookings existed, so there was no purity to protect. <c>CoreIndependenceTests</c> guards
/// Core's <i>package references</i>, which is a different property — and the default
/// implementation of this port lives in Core, drawing on a <c>System</c> type, without
/// troubling it.
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
