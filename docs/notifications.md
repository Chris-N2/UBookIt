# Reacting to bookings

uBookIt raises an Umbraco notification when a booking is placed and when one is cancelled, so
your site can do whatever it needs to.

## What uBookIt sends, and what it does not

**Out of the box: nothing.** No email, no SMS, no message of any kind, to the booker or to
anyone else. Placing a booking shows a confirmation on screen and stores it; cancelling one from
the backoffice releases the time. Until you configure the settings below, neither tells anybody.

**Configuring your site's mail server does not change that.** uBookIt will not send anything just
because Umbraco can — SMTP is configured on most sites for password resets and backoffice
invites, and that is not the same as wanting a booking package to write to your customers.
Sending needs both: a uBookIt setting *and* a working mail configuration.

### Turning it on

```json
{
  "UBookIt": {
    "Notifications": {
      "SendBookerEmails": true,
      "InternalRecipients": [ "bookings@example.com", "reception@example.com" ]
    }
  }
}
```

| Setting | What it does |
|---|---|
| `SendBookerEmails` | Sends the person who booked a plain-text confirmation when their booking is placed, and a notice when it is cancelled. Off unless set to `true`. |
| `InternalRecipients` | Sends your own people a message when a booking is placed or cancelled. **The list being non-empty is the switch** — there is no separate on/off. |

The two are independent: you can be told about bookings without anything being sent to your
customers, and the other way round. Both also require Umbraco to be able to send mail — an SMTP
`Host`, or a `PickupDirectoryLocation`, under `Umbraco:CMS:Global:Smtp`, plus a `From` address.
uBookIt does not supply a sender of its own; your site's `From` is used.

If you turn sending on and your site has no usable mail configuration, uBookIt says so in the log
once at startup rather than failing quietly.

### What the messages contain

Both messages carry the booking reference, when the booking is — in the time zone it was booked
against — and what was booked.

**Messages to `InternalRecipients` deliberately carry no booker name, email address or telephone
number.** They carry a link to the bookings screen instead. Who may see a booker's contact details
is decided by the **Sensitive data** user group in the backoffice, and a list of addresses in a
configuration file is not that decision — so the message takes you to where that control still
applies rather than carrying the details past it.

A booking whose booker has been erased (see [privacy](privacy.md)) has no address, so nothing is
sent to them. Your own recipients are still told.

### Replacing what uBookIt sends

uBookIt sends through Umbraco's own `IEmailSender` with notifications enabled, so you can
intercept `SendEmailNotification`, check `EmailType` for `"UBookItBooking"`, and substitute your
own message entirely — different wording, HTML, your own branding — without waiting for the
package to make it configurable.

If you would rather build the whole thing yourself, ignore the settings above and handle the
notifications directly. That is what they are for.

**Two things to know, whichever route you take:** uBookIt sends each message once — nothing is
retried or queued, and a message that fails to send is not reported to the person who booked. And
if an operator cancels a booking while nothing is configured and nothing is listening, the
customer will turn up.

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

**The contact details are here deliberately, and they are not filtered.** The backoffice
redacts a booker's name and email from users outside Umbraco's Sensitive data group — see
[the backoffice section](backoffice.md) — and that does **not** apply here. It is not an
oversight: that rule answers "may this signed-in person see somebody else's details", and
there is no signed-in person in a notification handler. Your code needs the address precisely
so it can write to it. Where the details go from here is yours to decide.

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

## If you store what a notification hands you, erasure will not reach it

Your handler receives the whole booking, contact details included, because that is how a site
sends its own confirmation email. **If you write those details anywhere durable — a queue that
keeps message bodies, a CRM, an audit table, a log line, a report — you have created a second
copy of somebody's personal data, and uBookIt cannot erase it.**

This matters because uBookIt promises that erasing a booking removes the person from it, and
that promise is only honest while the booking row is the *only* place the details live. Inside
the package it is: nothing logs them, no cache holds them, and the public booking API has no
endpoint that reads a booking back. Outside the package, that is your side of the line.

So if you keep anything:

- Prefer keeping the **booking reference** rather than the person. It is stable, it is not
  personal data, it survives erasure, and it is what somebody quotes on the telephone.
- If you must keep contact details, subscribe to erasure in your own system too — a copy you
  cannot remove on request is the problem the erasure feature exists to solve, relocated to a
  place nobody thinks to look.

The same applies to anything you build on top of the erase endpoint: an audit record of *who
erased what* is a sensible thing to keep, and it should record the booking reference and the
person who performed it — never the address that was erased.

## What does not raise a notification

- Editing resources or services. They are configuration, not events.
- Approving or declining a booking. No v1 pathway produces those statuses.
- Amending a booking's time. There is no such operation; the shape of it is a cancellation
  and a new booking.
- **Erasing a booker's details.** It changes a booking and raises nothing — which matters most
  to whoever read the section above and is now wondering how to erase their own copy. There is
  no notification to subscribe to, so a system holding contact details must be reconciled some
  other way. Keeping the booking reference instead of the person avoids the problem entirely.

  **And erasure does not always follow a call you made.** If the site configures a retention
  period, bookings are erased on a timer with nothing calling the endpoint — so a downstream
  copy can go stale without any request of yours having gone anywhere. Reconciling against the
  erase endpoint's callers is therefore not enough on such a site; keeping the reference rather
  than the person still is.
