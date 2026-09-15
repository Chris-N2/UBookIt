# Reacting to bookings

uBookIt raises an Umbraco notification when a booking is placed, when a requested booking is
confirmed or declined, and when one is cancelled, so your site can do whatever it needs to.

> Every uBookIt setting, which of them can be changed in the backoffice and which are
> configuration-only, and what happens when a value cannot be read, is in
> **[Configuration](configuration.md)**.


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
| `SendBookerEmails` | Sends the person who booked a plain-text message when their booking is placed, when a requested booking is confirmed or declined, and when one is cancelled. What the placement message says follows the booking's state: confirmed under auto-confirm, received-and-awaiting-confirmation when the site requires approval (see below). Off unless set to `true`. |
| `InternalRecipients` | Sends your own people a message when a booking is placed or cancelled. **The list being non-empty is the switch** — there is no separate on/off. These addresses hear about **every** booking; for different people per resource or service, see [responsibility](#telling-the-people-responsible) below, which adds recipients rather than replacing this list. Confirming or declining sends this list nothing — you, or a colleague, just did it from the bookings screen, which is where its state lives. |

The two are independent: you can be told about bookings without anything being sent to your
customers, and the other way round. Both also require Umbraco to be able to send mail — an SMTP
`Host`, or a `PickupDirectoryLocation`, under `Umbraco:CMS:Global:Smtp`, plus a `From` address.
uBookIt does not supply a sender of its own; your site's `From` is used.

If you turn sending on and your site has no usable mail configuration, uBookIt says so in the log
once at startup rather than failing quietly. That startup check reads configuration, so it speaks
for `SendBookerEmails` and `InternalRecipients`; responsibility assignments live in the database
and can change at any moment while the site runs, so a site relying on assignments alone gets no
startup line — if mail is broken there, the symptom is the messages not arriving.

### Telling the people responsible

A flat list is the wrong shape for a site where different people look after different things, so
each **resource** and each **service** can be given responsible parties in the backoffice —
backoffice **users**, backoffice **user groups**, or both — from the same screen where the
resource or service is edited (see [the backoffice guide](backoffice.md#responsibility)).

When a booking is placed or cancelled, the internal message goes to **the configured
`InternalRecipients` list plus everyone responsible for what the booking touches**: the people
assigned to its service, if it was booked through one, and the people assigned to every resource
it occupies. It is a union, with no precedence in it —

- assigning your first responsible user does **not** stop the `InternalRecipients` list hearing
  about that resource's bookings;
- an empty `InternalRecipients` list with assignments in place gives you purely targeted mail;
- an empty list and no assignments remains a full opt-out, exactly as before.

One person reachable several ways — in the list, assigned directly, and a member of an assigned
group — is written to once.

Worth knowing before you lean on it:

- **Group membership is read at the moment of sending.** Adding someone to an assigned group
  starts emailing them about those bookings with no uBookIt action at all — that is the feature,
  but it is better read about here than discovered in an inbox.
- **Users are emailed at their backoffice account address.** A user who is **Disabled**, or
  **Invited** but has never accepted, is skipped; active users are written to even if they have
  never logged in, and a temporary lockout does not cost anyone a notification.
- **An assignment whose user or group has since been deleted sends nothing, silently.** The
  editing screen marks it so you can see the contact went away; the mail path does not log it.
- **Responsibility is not permissions.** It decides who is emailed, nothing else: assigning a
  user grants them no access to the backoffice or to anything in it, and uBookIt only ever
  *reads* group membership. Messages to responsible people are internal messages like any other —
  they carry no booker contact details, whoever receives them.

### What the messages contain

Both messages carry the booking reference, when the booking is — in the time zone it was booked
against — and what was booked.

**Messages to `InternalRecipients` deliberately carry no booker name, email address or telephone
number.** They carry a link to the bookings screen instead. Who may see a booker's contact details
is decided by the **Sensitive data** user group in the backoffice, and a list of addresses in a
configuration file is not that decision — so the message takes you to where that control still
applies rather than carrying the details past it.

A booking whose booker has been erased (see [the backoffice guide](backoffice.md)) has no address, so nothing is
sent to them. Your own recipients are still told.

### When bookings need approval

By default every placement is confirmed on the spot. Setting `UBookIt:AutoConfirm` to `false`
makes placement produce a **requested** booking instead, which an operator confirms or
declines from the bookings screen — see [the backoffice guide](backoffice.md#approving-bookings).
The messages follow the booking rather than the setting:

- The booker's placement message says the booking has been **received and is not confirmed
  yet**, and that they will hear again when the site decides. The confirmation and decline
  messages are what keep that promise — they go through the same switches above, so a site
  that sends the first will send the second.
- The message to `InternalRecipients` says the booking **awaits approval**, alongside the
  backoffice link it already carries. Confirming or declining sends this list nothing — you
  just did it, from the screen where its state lives.

**If you have built your own front end against the delivery API, read this one.** A successful
`POST /bookings` or `POST /services/{id}/bookings` returns the booking's `status`, and on a site
requiring approval that value is `Requested` rather than `Confirmed`. The response shape has not
changed — but a client that ignores `status` and tells the visitor "confirmed" is now making a
statement the booking does not support, and the same visitor may later be declined. The shipped
Razor front end derives its confirmation page from this value; yours should too.

`AutoConfirm` reads as **on** when it is absent, and as on — with an error in the log naming
the setting — when a value was written that cannot be read as a boolean. The fallback
direction is deliberate: a site accidentally auto-confirming sends confirmations it can see
and correct, while a site accidentally requiring approval parks customers' bookings in a state
nobody is watching for. If you meant to require approval, the value must be a readable
`false`.

### Writing your own message content

**Put a Razor view in `~/Views/Partials/UBookIt/Emails/`, named for the message.** uBookIt finds
it and uses it in place of its own wording. Nothing else changes: whether a message is sent, who
receives it, and under what configuration are still decided by the settings above.

There are six messages, and each file is optional — supply one and the other five keep uBookIt's
wording:

| File | Sent to | When |
|---|---|---|
| `BookerPlaced.cshtml` | the booker | their booking has been placed |
| `BookerConfirmed.cshtml` | the booker | you confirmed their requested booking |
| `BookerDeclined.cshtml` | the booker | you declined their requested booking |
| `BookerCancelled.cshtml` | the booker | their booking has been cancelled |
| `InternalPlaced.cshtml` | your recipients | a booking has been placed |
| `InternalCancelled.cshtml` | your recipients | a booking has been cancelled |

There is no `InternalConfirmed` or `InternalDeclined` because no such message exists — you
confirmed or declined it yourself, and the bookings screen is where its state lives.

A template inherits uBookIt's email page, which gives it the model and two properties it may
set:

```cshtml
@inherits UBookIt.Web.Emails.UBookItEmailPage<UBookIt.Core.Notifications.BookerMessageModel>
@{
    Subject = $"Your appointment at Acme — {Model.Reference}";
}
Hello @Model.BookerName,

We are looking forward to seeing you at @Model.LocalStart.ToString("h:mm tt") on
@Model.LocalStart.ToString("dddd d MMMM") (@Model.TimeZoneId).

Quote @Model.Reference if you need to get in touch.
```

**This example is not what uBookIt sends** — it is deliberately somebody else's wording, so that
you can see the mechanism rather than copy a duplicate of the default. uBookIt's own messages are
built in code, and there is no `.cshtml` shipped for you to start from, precisely so there is
never a second copy of them to drift out of step.

`Subject` is optional: leave it unset and uBookIt's own subject is used, which is derived from
the booking's state. `IsHtml` defaults to `false`; set it to `true` if your content is HTML.

#### What your template is given

Both models carry the booking's `Kind` (which message this is), `Status`, `Reference` in the form
a person quotes, `ServiceName`, `ResourceNames`, `LocalStart`, `LocalEnd` and `TimeZoneId`. The
times arrive **already converted to the zone the booking was placed against** — format them
however you like, and use `TimeZoneId` to say which clock you are quoting.

`ResourceNames` is a collection rather than a sentence, so a service booking that resolved to
several resources can list them:

```cshtml
@foreach (var name in Model.ResourceNames)
{
    @:- @name
}
```

`Kind` and `Status` answer different questions and can disagree, which is often what you want to
branch on: a `BookerPlaced` message describes a **confirmed** booking on a site that confirms on
placement, and a **requested** one on a site that requires approval.

`BookerMessageModel` adds `BookerName`, `BookerEmail` and `BookerPhone`.
`InternalMessageModel` adds `AwaitsApproval` and `BackofficeUrl` — and **carries no booker name,
address or telephone number at all**, deliberately. See below.

#### HTML means no plain-text alternative

If you set `IsHtml = true`, the message is sent as HTML **and nothing else**. There is no
plain-text part alongside it.

That is a limit of Umbraco's mail abstraction rather than a choice uBookIt made: `EmailMessage`
carries one body and a flag saying what it is, and sending a multipart message would mean
bypassing your site's own mail configuration — costing you your transport and your ability to
intercept what the package sends. Worth knowing before you decide, because recipients who read
mail as text would get nothing.

#### What becomes yours, and what does not

**The words become yours, including whether they are accurate.** uBookIt will not check that your
content describes the booking's state correctly, presents the reference in the quotable form, or
says which time zone it is quoting. What it gives you is everything needed to be correct —
`Status`, the reference as displayed, and instants already in the booking's own zone.

**These do not change, whatever you write:**

- Sending still needs both a uBookIt setting and a working mail configuration.
- Supplying content for a message uBookIt does not send to an audience does not create one.
- A booking whose booker has been erased is still never written to.
- **A message to your recipients still carries no booker contact details** — not by convention,
  but because `InternalMessageModel` has no member for them. If you want them there, send that
  message yourself from the notifications below; uBookIt will not do it for you.

#### Templates supplied by a package

A referenced assembly can supply templates too — a Razor class library with the same
`Views/Partials/UBookIt/Emails/` folder, precompiled, works without the site copying anything.

**One caveat worth knowing before you rely on it:** if both a referenced assembly and your own
site supply the same file name, the assembly's currently wins. Your own file is used wherever the
assembly supplies none. If you need to override one a package gave you, the reliable route today
is to ask the package not to supply it rather than to shadow it.

#### When something is wrong

At startup uBookIt logs which messages have content supplied and which are using its own wording,
so a misspelled file name shows up when you start the site rather than when a customer does not
get what you intended.

If a template throws while rendering, uBookIt logs the failure and **sends its own wording
instead** — the message still goes out. A broken template is never silently treated as an absent
one.

### Replacing the whole message, delivery included

uBookIt sends through Umbraco's own `IEmailSender` with notifications enabled, so you can
intercept `SendEmailNotification` and check `EmailType` for `"UBookItBooking"`.

**This is a bigger step than writing a template, and worth understanding before you take it.**
The message you receive there is an immutable copy — you cannot rewrite its subject or body. What
you can do is call `HandleEmail()` and send your own, which means you also take on delivery: the
sending conditions, retries, and the rules uBookIt applies about erased bookers and contact
details become yours to reproduce. Reach for it when you want a different **channel** or
transport; reach for a template when you want different **words**.

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
| `BookingConfirmedNotification` | A requested booking has been confirmed and the change stored | `Booking` — the booking, now confirmed |
| `BookingDeclinedNotification` | A requested booking has been declined and the change stored | `Booking` — the booking, now declined |
| `BookingCancelledNotification` | A booking has been cancelled and the change stored | `Booking` — the booking, now cancelled |

All four live in `UBookIt.Persistence.Notifications`.

Each is raised **after** the change is committed and **only** when it succeeded. A failed
placement raises nothing, and so does an attempt at a transition the booking's status does not
permit — so being told at all means it happened.

None of the status-change notifications needs a before-and-after: cancellation succeeds only
from `Requested` or `Confirmed`, and confirmation and decline only from `Requested`, so
receiving one means the booking has just become what its status says.

**A booking placed under auto-confirm raises `BookingPlacedNotification` and nothing else.**
Auto-confirmation is not an event; it is what placement produced, and the placed notification's
booking already says so. `BookingConfirmedNotification` is only ever an operator confirming a
requested booking.

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
than vanishing. uBookIt writes the id and nothing else about the booker into that line — though a mail server's error text, if one is attached, is not uBookIt's to control; see below.

## If you store what a notification hands you, erasure will not reach it

Your handler receives the whole booking, contact details included, because that is how a site
sends its own confirmation email. **If you write those details anywhere durable — a queue that
keeps message bodies, a CRM, an audit table, a log line, a report — you have created a second
copy of somebody's personal data, and uBookIt cannot erase it.**

This matters because uBookIt promises that erasing a booking removes the person from it, and
that promise is only honest while the booking row is the *only* place the details live. Inside
the package that is very nearly true, and the exception is worth stating plainly rather than
leaving you to discover it:

- **uBookIt never writes a booker's name, address or telephone number to a log itself.** Its own
  log lines identify a booking by its id, including when sending fails.
- **No cache holds them**, and the public booking API has no endpoint that reads a booking back.
- **But if you enable emails, a mail server's error can quote the address back at you.** An SMTP
  rejection commonly echoes the recipient — `550 5.1.1 <someone@example.com>: Recipient address
  rejected` — and that error is reported through Umbraco's normal error logging. The address is
  in text the mail server wrote, not text uBookIt wrote, and uBookIt does not attempt to redact a
  third-party error: doing so would as likely destroy the diagnostic that makes a failed send
  findable at all.

  So on a site that sends email, **treat your application log as somewhere contact details can
  appear**, and set its retention accordingly. This is the same consideration as any other system
  that sends mail on your behalf.

Outside the package, that is your side of the line.

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
