## Why

Roadmap 0.5.0. A visitor books, sees a confirmation on screen, closes the tab, and has nothing —
no reference to quote, no record of when they are expected, and no way to check they did not
mistype the date. The reference exists precisely so a person can hold it, and today the package
never puts it anywhere a person keeps. Meanwhile the site owner learns of a booking only by going
to look for it.

Both notifications this hangs off already exist and already carry the whole `Booking`, reference
included, so nothing in the domain needs to change. `IEmailSender` is Umbraco's own abstraction
and picks up SMTP from `appsettings`, environment variables or user secrets, so nothing needs
configuring twice.

**This change makes a sentence the package currently prints to visitors false**, and that is the
part that governs its shape rather than the sending. `docs/notifications.md` says in bold that
uBookIt sends nothing; the privacy notice on the booking form says the details are held so the
site *"can contact you"* — wording chosen deliberately, at QA's insistence, *because* the package
sends nothing. A change that starts sending without revisiting those has the package telling
people something untrue about what happens to their data, on the very form that collects it.

## What Changes

**Sending happens only when a site has asked for it, and off is the default.** An Umbraco site
that has SMTP configured has configured it for password resets and backoffice invites; that is
not evidence the site wants uBookIt writing to its customers. Keying off SMTP alone would mean
upgrading the package silently begins outbound contact with people who booked under a notice
saying we send nothing. So the condition is a conjunction:

```
send to the booker  ⟺  UBookIt:Notifications:SendBookerEmails  ∧  IEmailSender.CanSendRequiredEmail()
send internally     ⟺  UBookIt:Notifications:InternalRecipients is non-empty  ∧  the same
```

Internal sending is gated by **presence of the list**, not by a second flag — a site that wants
to be told about bookings without writing to its customers simply sets the list and leaves the
flag off. This mirrors `IsRetentionConfigured`, which already tests presence rather than value.

**Two handlers on the existing notifications.** A booking placed and a booking cancelled each
produce, where enabled, one message to the booker and one to the internal list. Plain text only:
deliverable everywhere, no rendering path to build, nothing to make accessible, and it leaves
0.6.0's templating a clean space to land in rather than something to retrofit around.

**The privacy notice's purpose sentence becomes conditional on whether we will actually send**,
rendered from the same predicate that gates sending, in exactly the way the retention sentence is
already rendered from `RetentionDays`. A site with emails off keeps today's wording unchanged. A
site with emails on says a confirmation will be sent, because on that site one will be.

**The documentation claim is corrected wherever it appears**, which is `docs/notifications.md`,
`docs/backoffice.md` (twice) and the XML documentation on both notification types.

Deliberately **not** in scope, and each is a decision rather than an omission: email templates or
any site-authored content (0.6.0); HTML bodies (same); per-resource or per-service recipient
routing (0.7.0, which supersedes the flat list); a settings screen (0.9.0, which needs
permissions); and emails on decline, which has no producer in the domain to hang off.

## Capabilities

### New Capabilities

- `booking-emails`: when the package sends mail, to whom, what a message contains, and what it
  must never contain or promise.

### Modified Capabilities

- `privacy-notice`: the requirement forbidding the notice to state that any message is sent is
  replaced by one requiring the notice to describe **this site's** configuration — silent about
  messages where none are sent, explicit where they are. The guarantee being preserved is that
  the notice cannot assert processing the package does not perform; only its mechanism changes.
- `packaging`: the requirement that the shipped documentation state plainly that the package
  sends nothing itself becomes a requirement that it state what the package sends, under what
  configuration, and that it sends nothing without it.

## Impact

**Code.** New: an email-composing and -sending path in `UBookIt.Web` or `UBookIt.Persistence`
(sited in design), two notification handlers, and their composer registration. Modified:
`SiteBookingSettings` gains the notification settings; `UBookItPersistenceComposer` resolves and
validates them; `PrivacyNoticeView` gains the send predicate and `_PrivacyNotice.cshtml` a second
conditional sentence; the XML docs on `BookingPlacedNotification` and `BookingCancelledNotification`.

**Public surface.** `PrivacyNoticeView` gains a member and its factory a parameter. `ResourceBookingFlow`
and `ServiceBookingFlow` are public types and each gains a constructor parameter — technically
breaking, practically not, since both are resolved from the container and a site constructing one
by hand is reaching past the contract. Called out because the compatibility promise says a breaking
change must be, and because the package is not yet released, so the cost of taking it now is zero.

**Not part of this change but carried on its branch:** a one-line fix to the TestSite's flow
harness (`UbookitBookingTest.cshtml`), which could not survive its own GET form. Unrelated to
emails; rolled in at the repository owner's instruction rather than branched separately.

**Data.** None. No schema change, no migration, no new domain state. A booking already carries
everything a message needs except a directly-booked resource's display name, which is read from
the resource store at send time.

**Privacy.** This is the first outbound path for a booker's personal data, so it inherits every
constraint the data-protection work established: an erased booker has no address and must be sent
nothing; no name or address may reach a log; and the notice on the form must remain true of the
site rendering it.

**Dependencies.** `Umbraco.Cms.Core` only — `IEmailSender`, `EmailMessage` and
`GlobalSettings.Smtp` are all Umbraco's. Nothing new is referenced.

**Documentation.** `docs/notifications.md` (rewritten around the new claim), `docs/backoffice.md`
(two statements), and a new settings section wherever `RetentionDays` and `PrivacyPolicyUrl` are
already documented.
