## 1. Settings

- [x] 1.1 Add `BookingNotificationSettings` to `UBookIt.Core` — `SendBookerEmails` (bool) and
      `InternalRecipients` (`IReadOnlyList<string>`) — and hang it off `SiteBookingSettings`.
      Core takes no dependency to do this; it is a record.
- [x] 1.2 Resolve the settings in `UBookItPersistenceComposer` alongside `ResolveRetentionDays`
      and `ResolvePrivacyPolicyUrl`: absent, blank or unusable resolves to *not configured* and
      never to a default that enables anything.
- [x] 1.3 Validate recipient addresses. An unusable address is **dropped and reported**, the
      usable ones kept; a list of only unusable addresses configures nothing.
- [x] 1.4 Report once, at resolution, when sending to the booker is enabled and the host cannot
      send mail — the moment the mismatch is diagnosable.
- [x] 1.5 Tests: absent / blank / non-boolean / mixed-validity list / all-invalid list, and that
      no resolution path produces an enabling default.

## 2. Composing a message

- [x] 2.1 Add a message composer in `UBookIt.Persistence` (design D1 — **not** `UBookIt.Web`; a
      headless site must get emails without referencing the Razor front end).
- [x] 2.2 Booker message: reference via `Reference.Display`, start projected into
      `Interval.TimeZoneId` — the booking's own zone, not the site's current setting — and the
      "what" line.
- [x] 2.3 Derive the wording from `Booking.Status` rather than assuming `Confirmed` (design D7).
- [x] 2.4 Resolve the "what" line: `Service.DisplayName` where present, otherwise one
      `IResourceStore` read. Where neither resolves, **omit the line and still send**.
- [x] 2.5 Internal message: reference, time, what was booked, and a backoffice link. **No booker
      name, address or telephone number** (design D9 / the `sensitive-data` control).
- [x] 2.6 Build the backoffice link from `IHostingEnvironment.ApplicationMainUrl`. Where the
      application URL is not configured, send without the link.
- [ ] 2.7 **Verify the v17 backoffice route to the bookings section view against a running
      backoffice.** Do not derive it — read it from the address bar.
- [x] 2.8 Tests, including: a service booking, a direct booking, an unresolvable "what", a
      booking whose zone differs from the site's, and a status that is not `Confirmed`.

## 3. Sending

- [x] 3.1 Handlers for `BookingPlacedNotification` and `BookingCancelledNotification`, registered
      from the existing composer.
- [x] 3.2 Gate each direction on its own condition (design D2). Ask
      `CanSendRequiredEmail()` **per message**, never cached.
- [x] 3.3 Do **not** catch a throwing `CanSendRequiredEmail()` — let it reach
      `UmbracoBookingObserver`, which swallows it away from the booker and logs it. Catching would
      convert a misconfiguration into permanent silence.
- [x] 3.4 An erased booker is sent nothing; internal recipients are still told. Establish absence
      by `Booker.Contact is null`, never by inspecting contents.
- [x] 3.5 `From: null` (design D3), `IsBodyHtml: false` (design D10), `enableNotification: true`
      (design D4), and a `emailType` string that identifies the package's booking mail.
- [x] 3.6 No booker name, address or telephone number in any log line, including failure paths.
- [x] 3.7 Tests: both directions independently; neither; erased booker; a failing send leaving the
      booking untouched and unreported; nothing retried.
- [ ] 3.8 **Mutation-check the PII-in-logs guard and the erased-booker guard one at a time** — a
      pair checked together can carry a passenger that could never have failed.

## 4. The privacy notice

- [ ] 4.1 Add the send predicate to `PrivacyNoticeView`, constructed in the one place
      `PrivacyNoticeView.From` already constructs everything else.
- [ ] 4.2 The predicate is the **full conjunction** — setting ∧ host can send — not the setting
      alone (design D8), so the sentence and the behaviour cannot diverge.
- [ ] 4.3 Second conditional sentence in `_PrivacyNotice.cshtml`, in the same derived-not-authored
      shape the retention sentence already uses.
- [ ] 4.4 Govern the email field hint in `_YourDetails.cshtml` by the same predicate — the
      requirement binds every surface of the form that mentions contact, not the notice alone.
- [ ] 4.5 Tests over all four combinations of (setting on/off × host can send/cannot), plus a site
      with internal recipients and no booker messages.

## 5. Documentation

- [ ] 5.1 Rewrite `docs/notifications.md` around the new claim: nothing by default, what each
      setting enables, that SMTP alone enables nothing, what internal messages withhold, and the
      unchanged delivery limits.
- [ ] 5.2 Fix both statements in `docs/backoffice.md` — they are load-bearing instructions telling
      an operator to contact a booker themselves, so they need rewriting under a configuration
      that may or may not be on, not deleting.
- [ ] 5.3 Update the XML docs on `BookingPlacedNotification` and `BookingCancelledNotification`,
      which both currently state the package sends nothing.
- [ ] 5.4 Document the new settings wherever `RetentionDays` and `PrivacyPolicyUrl` are documented.
- [ ] 5.5 `docs/mvp.md` says the package sends no email "and will not in v1" — that was true of the
      MVP and is a historical statement; check whether it reads as current and adjust only if it does.

## 6. Sweep and verify

- [ ] 6.1 Re-run the falsified-sentence grep **outward** across `openspec/specs/` and `docs/` for
      any sibling requirement this change makes untrue, beyond the two already modified.
- [ ] 6.2 Re-run it **inward**: diff the guarantees of each wholesale replacement against the
      version in `openspec/specs/`, confirming every SHALL and every scenario is carried forward,
      deliberately dropped, or superseded by something stronger. The two being replaced are:
      - `privacy-notice` — **The package states only what it can keep true, and the site links its own policy**.
        Carried: author-only-facts; the policy link present/absent/unusable; the by-who-can-know
        division; "not a privacy policy" and its documentation duty. Superseded: *the notice SHALL
        NOT state that any message is sent* becomes *what it says SHALL be true of this site*.
        **Watch the passenger clause** — the old scenario bound "any surface of the same form that
        mentions contact", which is the email field hint, and it must survive the rewrite.
      - `packaging` — **What a site can subscribe to is documented**. Carried: naming each
        notification, when raised, what it carries, how to subscribe; the delivery limits.
        Superseded: *states plainly that the package sends nothing itself* becomes *states what it
        sends, to whom, and under what configuration*. Extended: the delivery limits now also
        cover the package's own messages.
- [ ] 6.3 `ChangeDeltaIntegrityTests` green — it is the authority on delta correctness, not
      `openspec validate --strict`.
- [ ] 6.4 Full suite green, clean Release build, **zero** warnings.
- [ ] 6.5 **Prove it live using a pickup directory** rather than an SMTP server: set
      `Umbraco:CMS:Global:Smtp:PickupDirectoryLocation` and `From`, place and cancel a booking,
      and read the `.eml` files. This also dodges the invite/SMTP blocker that made the
      sensitive-data change hard to verify.
- [ ] 6.6 Check the live booking form in both configurations — the notice and the email hint must
      change together, and neither may promise a message the site will not send.
