namespace UBookIt.Core.Bookings;

/// <summary>
/// Told when a booking has been placed, confirmed, declined or cancelled, so a host can react.
/// </summary>
/// <remarks>
/// <para>
/// <b>This port is declared by <c>UBookIt.Core</c>, which carries no package reference of any
/// kind.</b> That is the constraint the whole arrangement is shaped around: an Umbraco
/// notification type here would drag a framework into the domain, and a host that is not
/// Umbraco could then observe nothing. What Umbraco sites get is an adapter, registered
/// outside this assembly, that turns these calls into notifications an
/// <c>INotificationAsyncHandler</c> can take.
/// </para>
/// <para>
/// The alternative considered and rejected was a decorator around <see cref="IBookingService"/>
/// in the Umbraco layer, leaving Core untouched. It would have been airtight — nothing
/// resolves the concrete service — but it puts "a booking was placed" nowhere in the domain,
/// making the guarantee a property of dependency-injection wiring rather than of the model,
/// and it would notify Umbraco hosts only.
/// </para>
/// <para>
/// <b>Implementations must not throw.</b> They are called after the booking is already
/// stored, so an escaping exception would tell a caller their booking failed when it did not.
/// <see cref="BookingService"/> defends the boundary anyway rather than trusting this
/// sentence — see the remarks there for why the defence is silent.
/// </para>
/// <para>
/// Each member takes the booking and nothing derived from it, so there is one source of truth
/// and no computed value that could fall out of step with it.
/// </para>
/// </remarks>
public interface IBookingObserver
{
    /// <summary>Called after a booking has been placed and stored. Never on failure.</summary>
    Task BookingPlacedAsync(Booking booking, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after a booking has been placed <b>on a booker's behalf</b> — by an operator
    /// recording one taken by telephone or at a desk — and stored. Never on failure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is a separate event because it is a separate act, not a different kind of
    /// booking.</b> The booking produced is indistinguishable from any other afterwards and
    /// deliberately carries no marker, so who placed it cannot be recovered from the row. It
    /// has to be reported at the moment of placing or not at all — the same reason confirming,
    /// declining and moving are events of their own rather than states a subscriber infers.
    /// </para>
    /// <para>
    /// <b>It defaults to reporting an ordinary placement, so this is an addition and not a
    /// break.</b> A host that implemented this port before operator placement existed keeps
    /// compiling and keeps hearing "a booking was placed", which is true and is what such a
    /// host meant. Only a subscriber that must treat the two differently — the package's own
    /// notification adapter, because the site's internal recipients are not told about an
    /// action their colleague just performed — needs to override it.
    /// </para>
    /// <para>
    /// The default is safe in the direction that matters: forgetting to override it under-reports
    /// a distinction, never invents one, and never loses the placement itself.
    /// </para>
    /// </remarks>
    Task BookingPlacedOnBehalfAsync(Booking booking, CancellationToken cancellationToken = default)
        => BookingPlacedAsync(booking, cancellationToken);

    /// <summary>
    /// Called after a booking has been confirmed and the change stored. Never on failure.
    /// </summary>
    /// <remarks>
    /// <b>This does not need to describe what changed.</b> Confirmation succeeds only from
    /// <see cref="BookingStatus.Requested"/>, so being told at all means the booking has just
    /// become confirmed. A booking placed as confirmed under auto-confirm reports a
    /// <em>placement</em>, not a confirmation — auto-confirmation is not an event, it is what
    /// placement produced.
    /// </remarks>
    Task BookingConfirmedAsync(Booking booking, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after a booking has been declined and the change stored. Never on failure.
    /// </summary>
    /// <remarks>
    /// <b>This does not need to describe what changed</b>, on the same reasoning as
    /// <see cref="BookingConfirmedAsync"/>: decline succeeds only from
    /// <see cref="BookingStatus.Requested"/>.
    /// </remarks>
    Task BookingDeclinedAsync(Booking booking, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after a booking has been cancelled and the change stored. Never on failure.
    /// </summary>
    /// <remarks>
    /// <b>This does not need to describe what changed.</b> The status machine permits
    /// cancellation only from <see cref="BookingStatus.Requested"/> or
    /// <see cref="BookingStatus.Confirmed"/>, so being told at all means the booking has just
    /// become cancelled. Cancelling an already-cancelled booking fails and tells nobody.
    /// </remarks>
    Task BookingCancelledAsync(Booking booking, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after a booking has been moved and the new interval stored. Never on failure.
    /// </summary>
    /// <remarks>
    /// <b>This is the one report that must describe what changed.</b> A move changes no
    /// status: the booking after is in every respect the booking before except its interval, so
    /// "this booking has just moved" is meaningless without the interval it left — and a
    /// message to the booker that could only say where they now are, not where they were,
    /// would leave them to work out which of two times in their inbox is real. The previous
    /// interval is not a derivation; it is a fact the booking no longer holds and nothing else
    /// records. The exception is named in the spec so it cannot be read as licence for any
    /// other report to grow a before-and-after.
    /// </remarks>
    Task BookingMovedAsync(
        Booking booking, BookingInterval previousInterval, CancellationToken cancellationToken = default);
}

// BREAKING (17.1.0, called out in the spec): the port gained a moved member, again with no
// default implementation, on the reasoning above.

// BREAKING (pre-17.0.0, called out in the spec): the port gained a confirmed and a declined
// member, and there are deliberately no default implementations — an external observer
// silently deaf to declines would be a worse outcome than a compile error, on a port whose
// entire purpose is that a host hears what happened.

/// <summary>
/// The default: hears everything, does nothing.
/// </summary>
/// <remarks>
/// Registered so a host that wants no notifications pays for none, and so
/// <see cref="BookingService"/> never has to test for absence. A null check would be a second
/// way of expressing "no observer", and the one the code took would depend on how it was
/// constructed.
/// </remarks>
public sealed class NullBookingObserver : IBookingObserver
{
    public Task BookingPlacedAsync(Booking booking, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task BookingConfirmedAsync(Booking booking, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task BookingDeclinedAsync(Booking booking, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task BookingCancelledAsync(Booking booking, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task BookingMovedAsync(
        Booking booking, BookingInterval previousInterval, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
