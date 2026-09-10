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
- [x] 2.7 **Verify the v17 backoffice route to the bookings section view against a running
      backoffice.** Confirmed from two independent sources rather than guessed: Umbraco's own
      `section-view.extension.ts` documents the pattern `section/:sectionName/view/:pathname`, and
      the running backoffice serves `<base href="/umbraco/">`, so routes resolve under `/umbraco/`.
      With the section manifest's `ubookit` and `bookings`, the built link is
      `http://localhost:5000/umbraco/section/ubookit/view/bookings`, observed in a live message.
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
- [x] 3.8 **Mutation-check the PII-in-logs guard and the erased-booker guard one at a time** — a
      pair checked together can carry a passenger that could never have failed.

## 4. The privacy notice

- [x] 4.1 Add the send predicate to `PrivacyNoticeView`, constructed in the one place
      `PrivacyNoticeView.From` already constructs everything else.
- [x] 4.2 The predicate is the **full conjunction** — setting ∧ host can send — not the setting
      alone (design D8), so the sentence and the behaviour cannot diverge.
- [x] 4.3 Second conditional sentence in `_PrivacyNotice.cshtml`, in the same derived-not-authored
      shape the retention sentence already uses.
- [x] 4.4 Govern the email field hint in `_YourDetails.cshtml` by the same predicate — the
      requirement binds every surface of the form that mentions contact, not the notice alone.
- [x] 4.5 Tests over all four combinations of (setting on/off × host can send/cannot), plus a site
      with internal recipients and no booker messages.

## 5. Documentation

- [x] 5.1 Rewrite `docs/notifications.md` around the new claim: nothing by default, what each
      setting enables, that SMTP alone enables nothing, what internal messages withhold, and the
      unchanged delivery limits.
- [x] 5.2 Fix both statements in `docs/backoffice.md` — they are load-bearing instructions telling
      an operator to contact a booker themselves, so they need rewriting under a configuration
      that may or may not be on, not deleting.
- [x] 5.3 Update the XML docs on `BookingPlacedNotification` and `BookingCancelledNotification`,
      which both currently state the package sends nothing.
- [x] 5.4 Document the new settings wherever `RetentionDays` and `PrivacyPolicyUrl` are documented.
- [x] 5.5 `docs/mvp.md` says the package sends no email "and will not in v1" — that was true of the
      MVP and is a historical statement; check whether it reads as current and adjust only if it does.

## 6. Sweep and verify

- [x] 6.1 Re-run the falsified-sentence grep **outward** across `openspec/specs/` and `docs/` for
      any sibling requirement this change makes untrue, beyond the two already modified.
      **Result: nothing further.** Two candidates were considered and both stand:
      `default-frontend`'s "No message derived from a placement failure may assert it" is about a
      permanent-refusal message, not email; and `sensitive-data`'s requirement is scoped to what a
      **backoffice user** is shown in a **response**, which an email is not — and the internal
      message deliberately carries no contact details, so it opens no hole in that control either.
      In `docs/`, `notifications.md`, `backoffice.md` (twice), `mvp.md` and the TestSite's worked
      example all carried the old claim and are corrected.
- [x] 6.2 Re-run it **inward**: diff the guarantees of each wholesale replacement against the
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
      - `booker-erasure` — **Booker contact details SHALL have exactly one durable home**.
        **Opened at QA round 5, having been missed by the outward sweep entirely** — it names
        "confirmation emails" in its own prose as a feature that must be reconciled with it, which
        is as close to a written-in-advance trap as this repository has. Carried: one durable
        location; no second durable copy without erasing it; an erasure record naming the booking
        and not the person; all three original scenarios. Narrowed: "durable home" is scoped to
        **stores the package owns**, because both alternative readings fail — "no copy anywhere"
        forbids sending email at all, which is not what a requirement naming confirmation emails
        as a feature was written to do, and silence ships the reduction with nothing looking like
        a decision. Added: a hand-off outside the package is **disclosed rather than claimed**,
        and carries no more than its purpose requires.
      - `booker-erasure` — **What erasure does not reach is documented**. Carried: what erasure
        does, irreversibility, who may perform it, that it may happen without an actor, the
        one-booking boundary with its search direction, the future-booking boundary, the timer
        statement, all three original scenarios. Modified: "**the two** boundaries" becomes "the
        boundaries", with a third — erasure reaches neither a message already delivered nor an
        address a mail server quoted into the host's log — required **where an operator performing
        an erasure will meet it**, which is `docs/backoffice.md`, not only where sending is
        described.
- [x] 6.3 `ChangeDeltaIntegrityTests` green — it is the authority on delta correctness, not
      `openspec validate --strict`.
- [x] 6.4 Full suite green, clean Release build, **zero** warnings.

      **A correction to this change's own record.** One full-suite run during apply showed a
      single failure that did not print its name and did not reproduce; I hypothesised a packaging
      test racing the Release build. **That was wrong, and QA identified the real cause in round
      3**: `EraseBookerEndpointTests.The_response_echoes_nothing_that_was_erased` asserts
      `DoesNotContain("Ada", ...)` against a payload containing a random booking id. `A` and `D`
      are hex digits, so "ada" appears in a 32-hex-digit GUID roughly 0.7% of the time and the
      test reports a PII leak in an identifier that is not personal data. It is **pre-existing and
      outside this change**, and it is recorded as a deferred obligation rather than fixed here —
      but the wrong diagnosis is removed, because "intermittent, probably environmental" is
      exactly how a guard like this gets re-run until green and waved through, which is what
      happened to it once already.

      The same collision existed in this change's own new PII guard and is fixed there, by
      redacting the booking id from the haystack rather than by weakening the needle to a full
      name — a log line carrying only a first name is a real leak the guard must still catch.
- [x] 6.5 **Proved live using a pickup directory.** A booking placed through the delivery API on
      the running TestSite produced exactly two `.eml` files, and the pickup directory was cleared
      first so what was read back could only have come from this booking.
      - Internal message: reference, what, when, **and no booker name, email or telephone number**.
      - Booker message: `To: ada@example.com`, "Your booking is confirmed".
      - `From: no-reply@example.com` — the site's own; the package supplied none.
      - `08:00Z` rendered `9:00 AM (Europe/London)`, so the BST projection is right end to end.
      - The invalid recipient was dropped and the valid one kept, and the boot check named it in
        a live warning.
      - **Both link paths observed**: with no application URL configured the message sent without
        a link (Umbraco logs that state at boot, so it is real rather than defensive), and with
        one configured the link appeared and was well-formed.
      - **Cancellation was not driven live** — it needs an authenticated management-API call, and
        standing up backoffice auth was out of proportion to what it would add over the unit
        coverage. The path it exercises beyond placement is one notification type; everything
        else on the wire is shared and is what this run proved. Stated rather than glossed.
- [x] 6.6 **Checked live on the running form.** With sending configured, the email field's hint
      reads "We'll send your booking confirmation here" and the notice reads "We will send your
      booking confirmation to that email address" — both surfaces changed together, from one
      predicate. (The first grep for the notice's sentence reported a false negative: the Razor
      `<text>` block wraps it across lines, so it only matches once whitespace is normalised.
      Worth recording — the same trap would make a careless guard pass while the sentence was
      missing.)

- [x] 6.7 **AT SYNC (done): edit `booker-erasure`'s Purpose paragraph by hand.** A delta replaces
      requirements; it cannot express a change to a capability's Purpose, and that paragraph
      repeats **both** claims this change narrows — *"contact details have exactly one durable
      home, so erasing that home erases the data"* and *"the **two** boundaries the documentation
      has to state"*. Synced as-is, the capability would ship a summary contradicting the
      requirements beneath it, in the one file that is the source of truth after archiving.

      Replace with: contact details have exactly one durable home **among the stores the package
      owns**; and **the boundaries** the documentation has to state — one erasure reaches one
      booking, it may be performed on a booking that has not yet happened, and it does not reach a
      message already sent or an address a mail server quoted into the host's log.

      *Found by sweeping for the CLASS of absolute claims about logs, copies and contact details
      rather than for the sentences QA had already named — the same enumerate-the-population move
      that rounds 4 and 5 were both about, applied one level up. QA did not name this one.*

      **The guard did its job on the real sync.** Immediately after the deltas landed and before
      this edit, `The_erasure_capabilitys_summary_does_not_outrun_its_requirements` failed — the
      requirements had moved and the summary had not, which is the one state it exists to catch and
      the state a checklist entry alone has already failed to prevent once in this repository.
