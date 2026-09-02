# Reacting to bookings

uBookIt raises an Umbraco notification when a booking is placed and when one is cancelled, so
your site can do whatever it needs to.

## uBookIt sends nothing itself

**No email. No SMS. No message of any kind, to the booker or to anyone else.** Placing a
booking shows a confirmation on screen and stores it; cancelling one from the backoffice
releases the time. Neither tells the person who booked.

That is deliberate. Mail is your site's — your templates, your wording, your sending
infrastructure, your deliverability — and a package that owned that channel would own a
support burden it cannot test on your servers. What uBookIt gives you is the moment; what you
send is yours.

**The practical consequence is worth stating plainly:** if an operator cancels a booking and
nothing on your site is listening, the customer will turn up.

## The notifications

| Notification | Raised when | Carries |
|---|---|---|
| `BookingPlacedNotification` | A booking has been placed and stored | `Booking` — the booking as stored |
| `BookingCancelledNotification` | A booking has been cancelled and the change stored | `Booking` — the booking, now cancelled |

Both live in `UBookIt.Persistence.Notifications`.

Each is raised **after** the change is committed and **only** when it succeeded. A failed
placement raises nothing, and so does an attempt to cancel a booking that is already
cancelled — so being told at all means it happened.

`BookingCancelledNotification` needs no before-and-after: a booking can only be cancelled from
`Requested` or `Confirmed`, so receiving one means the booking has just become cancelled.

### What a booking carries

Whatever you need to write your own message: the id, **the reference**, the interval and the
time zone it was booked in, the status, when it was created, the booker's name and email, each
claimed resource's id, and the service it was placed for — or nothing, if it was booked
directly against a resource.

**The reference is the one to put in your email.** It is what the person who booked was shown
on their confirmation, and what they will quote back to you — short, unambiguous when read
aloud, and stable for the life of the booking. `booking.Reference.Display` groups it for
reading (`7QX4-M2NP`); `booking.Reference.Value` is the canonical form to store or search by.
The id is a `Guid` and belongs in your own records, not in a subject line.

## Subscribing

```csharp
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using UBookIt.Persistence.Notifications;

public sealed class BookingEmailComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) =>
        builder.AddNotificationAsyncHandler<BookingPlacedNotification, SendBookingConfirmation>();
}

public sealed class SendBookingConfirmation : INotificationAsyncHandler<BookingPlacedNotification>
{
    public async Task HandleAsync(BookingPlacedNotification notification, CancellationToken cancellationToken)
    {
        var booking = notification.Booking;

        // Your mail, your wording.
        await SendAsync(booking.Booker.Email, booking.Interval.StartUtc, cancellationToken);
    }
}
```

`INotificationHandler<T>` works too, if you have nothing to await.

## What you are promised, and what you are not

**Your handler cannot break a booking.** By the time it runs the booking is already stored,
so uBookIt catches anything you throw rather than letting it reach the person who booked. A
handler that fails does not undo a booking, and does not turn a successful booking into a
reported failure.

**A handler that throws is a notification nobody receives.** There is no retry and no queue.
If the message matters — a confirmation somebody is relying on — put the reliability in your
handler: write to a queue you control, and let that fail and retry on its own terms.

Failures are logged, so a handler that throws leaves a trace naming the booking id rather
than vanishing. The log records the id and nothing else about the booker.

## What does not raise a notification

- Editing resources or services. They are configuration, not events.
- Approving or declining a booking. No v1 pathway produces those statuses.
- Amending a booking's time. There is no such operation; the shape of it is a cancellation
  and a new booking.
