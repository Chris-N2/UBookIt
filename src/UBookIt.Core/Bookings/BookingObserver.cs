namespace UBookIt.Core.Bookings;

/// <summary>
/// Told when a booking has been placed or cancelled, so a host can react.
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
    /// Called after a booking has been cancelled and the change stored. Never on failure.
    /// </summary>
    /// <remarks>
    /// <b>This does not need to describe what changed.</b> The status machine permits
    /// cancellation only from <see cref="BookingStatus.Requested"/> or
    /// <see cref="BookingStatus.Confirmed"/>, so being told at all means the booking has just
    /// become cancelled. Cancelling an already-cancelled booking fails and tells nobody.
    /// </remarks>
    Task BookingCancelledAsync(Booking booking, CancellationToken cancellationToken = default);
}

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

    public Task BookingCancelledAsync(Booking booking, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
