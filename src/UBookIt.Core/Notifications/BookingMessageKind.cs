namespace UBookIt.Core.Notifications;

/// <summary>
/// Every message this package can send, by name.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the published set a site supplies content for</b>, and each member's name is
/// literally the file name a template is saved under. Publishing it as an enum rather than as
/// loose strings is what lets a caller enumerate the set — a site author asking "what may I
/// replace?" gets an answer from the type, and a boot check can report on every member without
/// anybody maintaining a second list.
/// </para>
/// <para>
/// <b>The set is exactly what the package sends, and nothing more.</b> There is no
/// <c>InternalConfirmed</c> or <c>InternalDeclined</c> because no such message exists:
/// confirming and declining are told to the booker only, since the site's own people just
/// performed the action and the bookings screen is where its state lives. A name for a message
/// that is never sent would be an invitation to write content that never renders.
/// </para>
/// <para>
/// <b>It names the message, not the booking's state, and the two are not the same.</b> A
/// booking placed on an auto-confirming site and a booking an operator has just confirmed both
/// have <see cref="Bookings.BookingStatus.Confirmed"/>; they are
/// <see cref="BookerPlaced"/> and <see cref="BookerConfirmed"/> respectively. The state is
/// carried separately on the model, so content can vary by either.
/// </para>
/// <para>
/// <b>Frozen by the compatibility promise at the first full release.</b> Renaming a member
/// renames a file every site that supplied one has on disk.
/// </para>
/// </remarks>
public enum BookingMessageKind
{
    /// <summary>To the booker, when their booking has been placed.</summary>
    /// <remarks>
    /// Sent for an auto-confirmed booking and for one that awaits approval alike — they differ
    /// by the model's status, not by the message. That is deliberate: one message about "you
    /// have booked" reads better than two nearly identical ones, and a template that needs to
    /// distinguish them has the status to do it with.
    /// </remarks>
    BookerPlaced,

    /// <summary>To the booker, when an operator has confirmed their requested booking.</summary>
    BookerConfirmed,

    /// <summary>To the booker, when an operator has declined their requested booking.</summary>
    BookerDeclined,

    /// <summary>To the booker, when their booking has been cancelled.</summary>
    BookerCancelled,

    /// <summary>To the site's own recipients, when a booking has been placed.</summary>
    InternalPlaced,

    /// <summary>To the site's own recipients, when a booking has been cancelled.</summary>
    /// <remarks>
    /// <b>This never reports a decline.</b> A decline is told to the booker only, so a message
    /// under this name is always an actual cancellation — the name is precise rather than a
    /// convenient grouping of two outcomes.
    /// </remarks>
    InternalCancelled,
}
