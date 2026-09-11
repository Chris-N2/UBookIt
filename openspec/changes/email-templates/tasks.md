# email-templates — tasks

## 0. Modified requirements — the wholesale-replacement diff

One `## MODIFIED Requirements` entry. A MODIFIED entry replaces its requirement wholesale, so it
was diffed guarantee-by-guarantee before the delta was written (CLAUDE.md, "Rewriting a
requirement destroys guarantees silently").

- [ ] 0.1 `booking-emails` / "What a message tells the booker" — carried forward verbatim: the
      reference-in-quotable-form rule, the times-in-the-booking's-own-zone rule, the
      derived-from-state rule, the send-even-without-what-was-booked rule, and all five original
      scenarios. Added: the narrowing to supplied content, the reach-clause limiting it, and two
      scenarios (an unsupplied message is unchanged; what a supplied view is still given). One
      sentence updated for accuracy rather than scope — "Placement produces a confirmed booking
      today" became true-of-both-settings after 0.6.0 and is corrected here.

## 1. Core: the port and the published contract

- [x] 1.1 `IBookingTemplateRenderer` in `UBookIt.Core` — renders a model for a named message and
      reports **three** outcomes distinctly: rendered, no template, failed. Two would collapse
      "broken" into "absent", which the spec forbids.
- [x] 1.2 `BookerMessageModel` and `InternalMessageModel`. **`InternalMessageModel` has no
      member for the booker's name, email or telephone** — that absence is the guarantee.
      Structure over pre-composed text: `ServiceName` + `ResourceNames` collection;
      `LocalStart`/`LocalEnd` as `DateTimeOffset` already in the booking's zone + `TimeZoneId`;
      `Reference` in quotable form. **`Status` and the event are on BOTH models**, and that is
      load-bearing rather than incidental: it is what lets one template serve several states, so
      the set stays at six instead of multiplying. `BookerPlaced` is the live case — it renders a
      confirmed placement and a requested one, distinguished by `Status`, rather than the package
      shipping a seventh name. `AwaitsApproval` and `BackofficeUrl?` are internal-only. Members
      named for a reader — this is the future token vocabulary and 17.0.0 freezes it.
- [x] 1.3 The published message-name set (the six), as a Core constant a caller can enumerate —
      not six loose strings. Nothing may name a seventh for an audience that receives no such
      message.

      **Design detail settled during apply, worth recording because the proposal left it
      implicit.** The tasks above said the models carry "the event", but `BookingEvent` is public
      and lives in `UBookIt.Persistence`, so Core models could only carry it by moving it — a
      breaking change the proposal never called out — or by duplicating the concept. Neither was
      necessary: the six message names ARE the vocabulary, so Core owns them as
      `BookingMessageKind` and the models carry `Kind`. No move, no breaking change, one concept
      instead of two, and each enum member is literally the file name a template is saved under.
      `BookingEvent` stays where it is as Persistence's internal wiring.
- [ ] 1.4 Tests: the internal model exposes no contact-detail member **by reflection over its
      public surface**, not by inspecting a hand-written list — the guard must see a member added
      later. The name set matches the events × audiences the package actually sends.

## 2. Persistence: build the models, ask the renderer, keep every policy

- [ ] 2.1 `BookingMessageComposer` takes `IBookingTemplateRenderer?` and builds the models. All
      existing decisions stay here: audience, erased booker, `DescribeAsync` and its catch, PII.
- [ ] 2.2 Ask the renderer; on "no template" **or** "failed", fall back to today's plain text. A
      failure is logged distinguishably from an absence, and the booking id only — never a booker.
- [ ] 2.3 A stated subject wins; no stated subject keeps the package's. `IsHtml` defaults false.
- [ ] 2.4 Tests: **with no renderer registered, every message is byte-identical to today** (this
      is the migration guarantee and it is a test, not an aspiration — assert against the current
      expected strings). Renderer returning no-template → same. Renderer throwing → fallback
      body, message still sent, failure logged, id-only. Subject override and default. `IsHtml`
      reaching `EmailMessage.IsBodyHtml`. Erased booker → renderer never invoked for the booker.
      Internal model built for an internal message carries no contact details.

## 3. Web: the renderer, the base page, the registration

- [ ] 3.1 `UBookItEmailPage<TModel> : RazorPage<TModel>` with typed `Subject` and `IsHtml` backed
      by `ViewData`. **`RazorPage<T>`, not `UmbracoViewPage<T>`** — that is what keeps rendering
      free of `IUmbracoContextAccessor`/`IPublishedUrlProvider` and therefore of a request.
- [ ] 3.2 `RazorBookingTemplateRenderer`: resolve `~/Views/Partials/UBookIt/Emails/<Name>.cshtml`,
      render to a string, read `Subject`/`IsHtml` back off the `ViewDataDictionary` it owns.
      Distinguish not-found from threw.
- [ ] 3.3 Register in the Web composer. **No `AddUnique`, no `ComposeAfter`** — presence is the
      whole mechanism, and `UBookItThemeRegistration` records why ordering is not trusted here.
- [ ] 3.4 Tests: renders a template; **renders with NO ambient `HttpContext`** (the requirement
      that will otherwise fail only in the retention job); missing file → no-template, not an
      exception; throwing template → failed, and the exception does not escape; a template
      supplied by a referenced assembly is found.

## 4. Boot check

- [ ] 4.1 Report per message whether content will be used, **established the way sending will
      establish it** rather than by restating registration — the theming boot check's rule.
- [ ] 4.2 Tests: a misnamed file reports that message as unsupplied; a mixture reports both
      halves; the check does not throw when no renderer is registered at all.

## 5. Shipped default templates — decide, then act

- [ ] 5.1 **Decide whether the package ships example templates.** Forms ships one as a worked
      example. Against: the six defaults are plain text built in code, so a shipped `.cshtml`
      would be a second copy of the same wording, free to drift — the exact fault this project
      keeps finding. For: an author has nothing to copy from. **Lean: ship none, and put a worked
      example in the documentation instead**, where it cannot be mistaken for the thing that
      renders. Record the decision either way.

## 6. Documentation

- [ ] 6.1 `docs/notifications.md`: the mechanism, the six names, the path, what each model
      carries, how to state subject and content type, and the worked example.
- [ ] 6.2 **The single-body limit**, with its reason (bypassing `IEmailSender` would cost the
      site its transport and its interception seam).
- [ ] 6.3 **What narrows and what does not**, as an explicit list — wording becomes the author's;
      gating, audiences, erased-booker and the internal-message exclusion do not.
- [ ] 6.4 Documentation guards, wrap-safe in both directions. Include an absence check that no
      document tells an author supplied content is subject to the package's wording guarantees.

## 7. Sync obligations recorded now (executed at sync)

- [ ] 7.1 Outward sibling-spec grep over capabilities this change does not touch. Needles:
      "the package composes", "what the message says", "plain text", "derived from that state",
      "no booker name". Known candidates to open: `booker-erasure`, `sensitive-data`,
      `privacy-notice` (it describes what a booker is told and was the CRITICAL last change),
      `theming` (states what a theme covers — must not read as covering email), `default-frontend`.
- [ ] 7.2 Purpose prose check on `booking-emails` — its Purpose says "what that message carries,
      and — more of the point — what it must never carry or promise", which must still be true
      once content can come from a site.

## 8. Verification

- [ ] 8.1 Clean Release build, zero warnings; full .NET and client suites green; strict validate.
- [ ] 8.2 Live on the TestSite, pickup directory cleared first: no templates → messages identical
      to 0.6.0; supply `BookerPlaced` as plain text → used, subject default; state a subject →
      used; state `IsHtml` and supply HTML → arrives as HTML; break a template → fallback body
      arrives and the log names the failure; supply `InternalPlaced` → arrives with no contact
      details. Boot log lists supplied and unsupplied.
- [ ] 8.3 **Prove the no-request path for real**, not only in a unit test — the honest way is a
      message sent from the retention sweep's execution path or an equivalent with no request.
      If that cannot be staged, say so explicitly and record what was actually proved.
