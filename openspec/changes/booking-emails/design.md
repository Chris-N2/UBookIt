## Context

The seam this hangs off is already built and already load-bearing. `BookingService` calls
`IBookingObserver`; `UmbracoBookingObserver` in `UBookIt.Persistence` turns those calls into
`BookingPlacedNotification` and `BookingCancelledNotification`, each carrying the whole `Booking`.
That adapter already swallows and logs a throwing handler, and already logs **the booking id
only** — *"a notification failure is not a reason to write a booker's name or email into a log."*
A handler added here inherits both properties without asking for them.

What the booking itself carries turns out to be almost everything a message needs:

```
  Booking
    ├─ Reference ──── .Display   →  "K4M2QX7R" rendered "K4M2-QX7R"
    ├─ Interval  ──── .StartUtc, .EndUtc
    │                 .TimeZoneId  ← the zone it was PLACED against
    ├─ Status    ──── Requested | Confirmed | Declined | Cancelled
    ├─ Booker    ──── .Contact  (null ⟺ erased, and nothing else)
    ├─ Service?  ──── .DisplayName  ← snapshot taken at placement
    └─ Claims[]  ──── ResourceClaim(Guid ResourceId)   ← no name, only an id
```

Two of those are quietly important. The interval carries **its own** zone id, so a message can be
formatted in the zone the booking was made against rather than whatever the site is configured
with today. And `Booker.Contact` is `null` if and only if the booker has been erased, which the
nullable-reference settings turn into a compile-time obligation rather than a review item.

The gap is the last line: a directly-booked resource has an id and nothing else.

On the Umbraco side, `IEmailSender` gives more than expected. `CanSendRequiredEmail()` is a real
*"is mail configured"* probe (SMTP host, or a pickup directory, or a registered handler).
`SendAsync` **returns quietly** when nothing is configured rather than throwing. `From` may be
`null` and falls back to `GlobalSettings.Smtp.From`. And `enableNotification: true` raises
`SendEmailNotification` first, letting a handler replace the message entirely.

## Goals / Non-Goals

**Goals:**

- A booker who closes the tab still holds their reference, their time, and what they booked.
- A site owner learns of a booking without going to look for it.
- **Upgrading the package sends nobody anything.** Sending is something a site turns on.
- The privacy notice stays true of the site rendering it, under every combination of settings.
- The wording cannot become a lie when approval/decline ships.
- 0.6.0 templating lands in a clean space rather than being retrofitted around this.

**Non-Goals:**

- Site-authored content, templates, or HTML bodies — 0.6.0, and deliberately not anticipated here
  beyond passing the flag that makes the seam available.
- Per-resource or per-service recipient routing — 0.7.0 supersedes the flat list.
- A settings screen — 0.9.0, which needs permissions we do not have.
- Retry, queueing, or delivery tracking. The observer neither retries nor queues, by design, and
  a message is not a booking.
- Emails on decline. `BookingStatus.Declined` has no producer, so there is no event to hang one on.

## Decisions

### D1 — The email path lives in `UBookIt.Persistence`, not `UBookIt.Web`

CLAUDE.md requires that a headless consumer build a full booking flow without referencing
`UBookIt.Web`. **A headless site wants confirmation emails just as much as a Razor one**, so
putting the sender in `UBookIt.Web` would make a core feature of the booking flow reachable only
by sites that also take the shipped front end.

`UBookIt.Persistence` is where the observer, both notification types and the composer already
live, and it is the one project every consumer loads. This is the same trade-off already recorded
in `BookingNotifications.cs` and accepted there for the same reason.

The privacy-notice half of this change *is* in `UBookIt.Web`, because a notice is markup. The two
halves share only the settings record.

### D2 — Two independent conditions, evaluated per message, cached never

```
  booker message   ⟺  SendBookerEmails            ∧  CanSendRequiredEmail()
  internal message ⟺  InternalRecipients ≠ empty  ∧  CanSendRequiredEmail()
```

The left conjunct is ours and answers *"does this site want uBookIt writing to people"*. The right
is Umbraco's and answers *"can anything be sent at all"*. They are genuinely different questions
and neither implies the other: an Umbraco site has SMTP configured for password resets and
backoffice invites long before it has any opinion about booking confirmations.

**Internal sending is gated by the list's presence, not by a second flag.** A site that wants to
hear about bookings without writing to its customers sets the list and leaves the flag off; the
absent case needs no configuration at all. `IsRetentionConfigured` already establishes presence-
not-value as this project's shape for an optional setting.

`CanSendRequiredEmail()` is asked per message rather than at boot. `EmailSender` watches
`IOptionsMonitor<GlobalSettings>` and re-reads on change, so SMTP can appear or vanish while the
site runs; a value cached at startup would be a stale answer with no way to notice.

**A throwing probe is not caught.** Umbraco's default `NotImplementedEmailSender` throws from
every member including `CanSendRequiredEmail()`; in a web application `Umbraco.Infrastructure`
replaces it, so we never meet it. Rather than catch and treat a throw as *"cannot send"* — which
would be an escape hatch converting a real misconfiguration into silence — the exception is left
to reach `UmbracoBookingObserver`, which swallows it away from the booker and logs it loudly. The
booking is unaffected either way; the difference is whether anybody finds out.

### D3 — Never invent a From address

`EmailMessage.From` is passed as `null` and Umbraco falls back to `GlobalSettings.Smtp.From`. A
`UBookIt:Notifications:From` setting would be a second source of truth for the same fact, able to
disagree with the one the rest of the site's mail already uses, and able to be set to an address
the configured SMTP server will refuse to relay for.

### D4 — `enableNotification: true`, from day one

This raises Umbraco's own `SendEmailNotification` before sending; a handler that marks it handled
replaces our message entirely. **That is 0.6.0's templating seam, already built by Umbraco, for
the cost of one boolean.** It also means a site can override the wording today without waiting
for us, which is the same "publish the seam" instinct the whole package is shaped around.

The `emailType` string discriminates our mail from Umbraco's own on that notification.
`Constants.Web.EmailTypes` has no booking member, and the parameter is a free string, so this
change introduces one.

### D5 — Composed from the booking, plus at most one read

Reference from `Reference.Display`. Times formatted from `Interval.StartUtc` projected into
`Interval.TimeZoneId` — **the booking's own zone, not the site's current setting**, so a site that
changes zone does not retrospectively restate when existing bookings are. The "what" line comes
from `Service.DisplayName` where the booking has a service, which is already a snapshot and so
already says what was sold rather than what it is called now.

Only a **directly-booked resource** needs a read, because `ResourceClaim` carries an id alone.
That read reports the resource's *current* name, which is knowingly weaker than the service
snapshot — acceptable in a message sent within seconds of the event, and the alternative
(snapshotting the name onto the claim) is a domain and schema change this change was scoped to
avoid. Recorded here so the asymmetry reads as a decision.

**Where the read finds nothing, the message omits the "what" line and still sends.** A reference
and a time are the parts a person cannot reconstruct; withholding them because a name is missing
would be the wrong trade. This is stated as a requirement with its own scenario rather than left
as a `catch`, so that a suite can prove the message still carries what matters.

### D6 — An erased booker is sent nothing, and the compiler says so

`Booker.Contact` is `null` if and only if the booker has been erased. Reaching `.Email` therefore
requires establishing presence first, and every site that must do so is enumerated by the
compiler rather than by a reviewer.

This is reachable, not theoretical: the retention sweep erases a booking some configured period
after it ends, and an operator can still cancel it afterwards. **Internal recipients are still
told** — the booking is real and the site is entitled to know it was cancelled — which is also
why the two conditions in D2 are independent rather than one switch.

### D7 — The wording is derived from `Booking.Status`, never assumed

Placement produces `BookingStatus.Confirmed` today — it is the only status literal in
`BookingService` — so *"your booking is confirmed"* is currently true. But `Requested` exists in
the enum and approval is a named future feature. A subject line that hard-codes *confirmed* would
become false the day approval ships, **silently, in a message to a customer, with nothing in that
change's diff that looks like a deletion**.

So the placement message reads its wording from the status it was given. Today that is a branch
with one reachable arm; that is the point.

### D8 — The notice's predicate is the *booker* predicate, and it is the full conjunction

The privacy notice speaks to the person filling the form, so what it may say is governed by
whether **that person** will be emailed — `SendBookerEmails ∧ CanSendRequiredEmail()`. Internal
recipients are invisible to it; a site telling its own staff about bookings is not processing the
booker's data in a way the purpose sentence describes.

**Both conjuncts, not just the flag.** A site that sets `SendBookerEmails` and has no SMTP sends
nothing, so a notice promising a confirmation on that site would assert processing the package
does not perform — which is precisely the failure the privacy-notice capability exists to
prevent. The predicate that governs the sentence is the predicate that governs the sending, and
they are the same expression for that reason.

This reuses the shape `_PrivacyNotice.cshtml` already has: the retention sentence renders one way
when `RetentionDays` is configured and another when it is not, derived from the value rather than
authored. The purpose sentence gains the same treatment.

### D9 — Settings resolve like every other setting here: absent means off, never a default

`ResolveRetentionDays` and `ResolvePrivacyPolicyUrl` established the shape — absent, blank or
malformed resolves to *off* rather than to a default, so a typo can never enable something.

`InternalRecipients` differs in one respect worth stating: **a malformed entry is dropped and the
remaining valid ones are kept**, rather than the whole list being refused. One typo silencing
every internal notification would be a worse failure than one address not receiving. The dropped
entry is logged with its value, which is safe here in a way it would not be elsewhere: these are
staff addresses a site owner typed into configuration, not a visitor's personal data. That
distinction is stated so it is not later read as a licence to log booker addresses.

### D10 — Plain text

`IsBodyHtml: false`. Deliverable everywhere, no second rendering path, no accessibility surface,
and nothing that has to be rebuilt when 0.6.0 introduces templates. Email HTML is not web HTML,
so the templating work is a separate rendering path whichever way this goes — building a throwaway
one now buys nothing.

## Risks / Trade-offs

**A quiet failure is still quiet.** `SendAsync` returns without complaint when nothing is
configured, and the observer swallows what does throw. Together those mean a site can believe it
is sending and not be. D2's conjunction is what makes this tolerable — a site that has not turned
sending on is not surprised by silence — but a site that has turned it on and misconfigured SMTP
gets a Debug-level line from Umbraco and nothing from us. Mitigated by requiring the resolver to
log **once at startup** when sending is enabled and `CanSendRequiredEmail()` is false, which is
the moment the mismatch is actually diagnosable.

**The privacy notice now depends on runtime state.** Its wording is a function of SMTP
configuration, which can change without a deployment. That is the correct behaviour — the notice
should describe the site as it stands — but it means the sentence a visitor read is not
reproducible from the repository alone. Accepted: the alternative is a notice that is right at
build time and wrong afterwards.

**One extra read per direct booking, on the notification path.** After the booking is committed
and off the caller's thread, so it cannot slow a placement or fail one. It can fail on its own,
which D5 handles by sending less rather than nothing.

**The internal list is flat and 0.7.0 replaces it.** Every site configuring it now will
reconfigure at 0.7.0. Called out rather than avoided: responsibility routing needs a model of who
owns what, and inventing half of one here to avoid a future migration would cost more than the
migration.

**Two documentation claims become false in files this change does not otherwise touch.**
`docs/backoffice.md` states twice that uBookIt sends nothing, in the course of explaining what an
operator must do by hand. Those sentences are load-bearing instructions, not incidental prose —
they tell an operator to contact a booker themselves — so they need rewriting rather than
deleting, under a configuration that may or may not be on.
