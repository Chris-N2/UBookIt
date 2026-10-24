# Email templates

uBookIt sends its emails in its own wording until you give it yours. This page covers writing
that wording as Razor views: which files, what each view is given, giving a service its own set
of views, and what happens when a view goes wrong. Whether a message is sent at all, and to whom,
is decided by the settings in [Reacting to bookings](notifications.md), not by anything here.

**Put a Razor view in `~/Views/Partials/UBookIt/Emails/`, named for the message.** uBookIt finds
it and uses it in place of its own wording. Nothing else changes: whether a message is sent, who
receives it, and under what configuration are still decided by the settings above.

There are seven messages, and each file is optional — supply one and the other six keep uBookIt's
wording:

| File | Sent to | When |
|---|---|---|
| `BookerPlaced.cshtml` | the booker | their booking has been placed |
| `BookerConfirmed.cshtml` | the booker | you confirmed their requested booking |
| `BookerDeclined.cshtml` | the booker | you declined their requested booking |
| `BookerCancelled.cshtml` | the booker | their booking has been cancelled |
| `BookerMoved.cshtml` | the booker | you moved their booking to a new time |
| `InternalPlaced.cshtml` | your recipients | a booking has been placed |
| `InternalCancelled.cshtml` | your recipients | a booking has been cancelled |

There is no `InternalConfirmed`, `InternalDeclined` or `InternalMoved` because no such message
exists — you confirmed, declined or moved it yourself, and the bookings screen is where its state
lives.

`BookerMoved` is the one message whose model carries something no other does: the interval the
booking held **before** the move, as `Model.PreviousLocalStart` and `Model.PreviousLocalEnd`, in the
booking's own zone alongside the current `LocalStart` and `LocalEnd`. Both are `null` for every
other message — a view written before they existed renders unchanged. A customer holding an older
confirmation needs to be told which of the two times in their inbox is real, so uBookIt's own
wording states the old time before the new one; yours should too.

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

Both models also carry `ServiceId` and `TemplateSet`. `ServiceId` is the id of the service the
booking was placed for, as the booking recorded it — `null` for a booking placed directly against a
resource, and still present when the service has since been deleted. `TemplateSet` is the service's
[template set](#template-sets) as read when the message is composed — `null` where the service names
none, no longer exists, or the booking was placed directly against a resource. A view written before
either existed renders unchanged.

**To tell services apart in a view, use `Model.TemplateSet`, not `Model.ServiceId`.** Services live
in uBookIt's own tables rather than in your content, so the same service has a different id on your
development, staging and live sites, and a view that compares against one id works on one of them
only. A set name is whatever you typed, and is the same everywhere you deploy:

```cshtml
@if (Model.TemplateSet == "remote")
{
    @:Join from a quiet room with a working microphone.
}
```

Branching on `ServiceName` is worse still: it is the name the booking was sold under, and a rename
in the backoffice changes what new bookings carry without telling your view.

`BookerMessageModel` adds `BookerName`, `BookerEmail` and `BookerPhone`, and the
`CancellationUrl` and `BookerInstructions` described below.
`InternalMessageModel` adds `AwaitsApproval` and `BackofficeUrl` — and **carries no booker name,
address or telephone number at all**, deliberately. See below.

#### `CancellationUrl` — and what happens if your view leaves it out

Where self-service cancellation is on, `BookerMessageModel.CancellationUrl` carries the link that
cancels the booking. It is a complete address, ready to render:

```razor
@if (Model.CancellationUrl is not null)
{
    <p>
        If you need to cancel this booking, use this link — you will need it, and it is the
        only one you will be sent:
        <a href="@Model.CancellationUrl">@Model.CancellationUrl</a>
    </p>
}
```

**`null` means there is no self-service cancellation for this booking**, not that one was expected
and could not be built. It is null on a site with the feature off, on a site whose own address
Umbraco has not resolved, and on **every message except the one that issued the link** — the
placement message. A later confirmation or cancellation message does not restate it, because a
single-use credential sitting in two mailbox copies leaves the reader nothing to tell them which is
live.

**If you supply your own view and omit it, your bookers have no self-service route.** The package
will not compensate by sending a second message: that would be overriding a choice you made. This is
the same narrowing that applies to everything else a supplied view says — the words are yours, and
so is their completeness.

#### `BookerInstructions` — the service's instructions for a confirmed booker

`BookerMessageModel.BookerInstructions` carries the service's
[instructions for the booker](#instructions-for-the-booker), on exactly the terms uBookIt's own
wording follows: present when the booking is confirmed as the message is composed and its service
has instructions.

```razor
@if (Model.BookerInstructions is not null)
{
    <h2>Before your appointment</h2>
    <p style="white-space: pre-line">@Model.BookerInstructions</p>
}
```

**`null` means this message does not carry instructions** — the booking is awaiting approval,
declined or cancelled, it was placed directly against a resource, or its service has none. A view
written before the member existed renders unchanged.

The value is the text as it was written, trimmed, with its line breaks, and nothing else: no
markup is interpreted, and Razor encodes it in an HTML view like any other value. How its lines are
presented is your view's choice.

**If you supply your own view and omit it, your bookers are not given the instructions.** uBookIt
does not add them to your content or send them separately — the same narrowing as for
`CancellationUrl`: the words are yours. `InternalMessageModel` has no such member.

#### Template sets

An online consultation's confirmation is often a different email from an in-person one, not the same
email with a paragraph added. A **template set** gives a service — or a group of services — its own
views, without your views branching on names or ids.

**Name the set on the service**, in the **Email template set** field of the service editor (or as
`templateSet` through the Management API) — say `remote`. Then put that service's views in a folder
of the same name beneath the email folder:

```
Views/Partials/UBookIt/Emails/
  BookerPlaced.cshtml            ← for every service
  BookerConfirmed.cshtml
  remote/
    BookerConfirmed.cshtml       ← for services whose set is "remote"
    InternalPlaced.cshtml
```

**For each message, uBookIt looks in this order:**

1. the view for that message in the service's set — `remote/BookerConfirmed.cshtml`;
2. where the set has no view for it, the view for every service — `BookerConfirmed.cshtml`;
3. where neither exists, uBookIt's own wording.

A set does not have to supply every message. In the layout above, a `remote` booking's confirmation
comes from the set, its placement message from the view for every service, and its cancellation in
uBookIt's own wording. Several services can name the same set. Every message is eligible, including
those to your own recipients, and a set's `InternalPlaced.cshtml` is given the same
`InternalMessageModel`, with no booker contact details on it, as any other.

**Set names are lower case.** The name becomes a folder name, and folder names are case-sensitive on
Linux hosts and not on Windows — a site developed on Windows and deployed to a Linux container would
otherwise find `Remote` working on one and silently falling back on the other. So uBookIt lowers the
name when the service is saved: type `Remote` and the editor shows `remote` afterwards, which is the
folder to create. A name may use only the letters `a`–`z`, the digits `0`–`9` and single hyphens
between them, up to 64 characters. Anything else is refused when the service is saved, so no set
name can point outside the email folder.

The set is **read from the service when each message is composed**, like the instructions. Change a
service's set, and the next message about any of its bookings is looked up in the new one. A booking
whose service has been deleted, and a booking placed directly against a resource, use no set.

**Two failures send uBookIt's own wording, never the view for every service:**

- **A view in the set that fails to render.** Your view for every service may have been written for
  a different kind of service, and in-person directions sent to an online booker are worse than
  neutral wording. The failure is logged, naming the set's file.
- **A service that cannot be read** while the message is composed, because uBookIt cannot then tell
  which set applies. The message is still sent and the failure is logged with the booking id only.

**If you replace uBookIt's renderer** with your own `IBookingTemplateRenderer`, it is given
`Model.TemplateSet` and must read it itself to support sets; until it does, it ignores them.

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
[Template set](#template-sets) folders inside it work the same way.

**One caveat worth knowing before you rely on it:** if both a referenced assembly and your own
site supply the same file name, the assembly's currently wins. Your own file is used wherever the
assembly supplies none. If you need to override one a package gave you, the reliable route today
is to ask the package not to supply it rather than to shadow it.

#### When something is wrong

At startup uBookIt logs which messages have content supplied and which are using its own wording,
so a misspelled file name shows up when you start the site rather than when a customer does not
get what you intended.

It does the same for each [template set](#template-sets) a service names: which messages the set
supplies, and which fall back. **A set that supplies nothing is logged as a warning** naming the
folder its views were expected in — the usual cause is a misspelt set name, or a folder whose case
differs from it. The report describes your services **as they stand at startup**, so a set typed into
the editor afterwards is reported the next time the site starts. If services cannot be read at
startup, the report says sets could not be checked, and the site starts regardless.

If a template throws while rendering, uBookIt logs the failure and **sends its own wording
instead** — the message still goes out. A broken template is never silently treated as an absent
one — including one in a template set, which does not fall back to the view for every service.

### Replacing the whole message, delivery included

uBookIt sends through Umbraco's own `IEmailSender` with notifications enabled, so you can
intercept `SendEmailNotification` and check `EmailType` for `"UBookItBooking"`.

**This is a bigger step than writing a template, and worth understanding before you take it.**
The message you receive there is an immutable copy — you cannot rewrite its subject or body. What
you can do is call `HandleEmail()` and send your own, which means you also take on delivery: the
sending conditions, retries, and the rules uBookIt applies about erased bookers and contact
details become yours to reproduce. Reach for it when you want a different **channel** or
transport; reach for a template when you want different **words**.
