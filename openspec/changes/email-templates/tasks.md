# email-templates — tasks

## 0. Modified requirements — the wholesale-replacement diff

One `## MODIFIED Requirements` entry. A MODIFIED entry replaces its requirement wholesale, so it
was diffed guarantee-by-guarantee before the delta was written (CLAUDE.md, "Rewriting a
requirement destroys guarantees silently").

- [x] 0.1 `booking-emails` / "What a message tells the booker" — carried forward verbatim: the
      reference-in-quotable-form rule, the times-in-the-booking's-own-zone rule, the
      derived-from-state rule, the send-even-without-what-was-booked rule, and all five original
      scenarios. Added: the narrowing to supplied content, the reach-clause limiting it, and two
      scenarios (an unsupplied message is unchanged; what a supplied view is still given). One
      sentence updated for accuracy rather than scope — "Placement produces a confirmed booking
      today" became true-of-both-settings after 0.6.0 and is corrected here.

- [x] 0.2 `booking-emails` / "The internal message says when a booking awaits action" — added in QA
      round 1. Carried verbatim: the state-not-setting rule and all three scenarios. Added: the
      wording narrowing, and the explicit statement that the no-personal-data sentence does NOT
      narrow with it.
- [x] 0.3 `booking-emails` / "A message to the site's own people carries no personal data" — added
      in QA round 1 for the same reason. Carried verbatim: the reason, the link rule and all three
      scenarios. Added: the two-halves-narrow-differently paragraph and two scenarios.

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
- [x] 1.4 Tests: the internal model exposes no contact-detail member **by reflection over its
      public surface**, not by inspecting a hand-written list — the guard must see a member added
      later. The name set matches the events × audiences the package actually sends.

## 2. Persistence: build the models, ask the renderer, keep every policy

- [x] 2.1 `BookingMessageComposer` takes `IBookingTemplateRenderer?` and builds the models. All
      existing decisions stay here: audience, erased booker, `DescribeAsync` and its catch, PII.
- [x] 2.2 Ask the renderer; on "no template" **or** "failed", fall back to today's plain text. A
      failure is logged distinguishably from an absence, and the booking id only — never a booker.
- [x] 2.3 A stated subject wins; no stated subject keeps the package's. `IsHtml` defaults false.
- [x] 2.4 Tests: **with no renderer registered, every message is byte-identical to today** (this
      is the migration guarantee and it is a test, not an aspiration — assert against the current
      expected strings). Renderer returning no-template → same. Renderer throwing → fallback
      body, message still sent, failure logged, id-only. Subject override and default. `IsHtml`
      reaching `EmailMessage.IsBodyHtml`. Erased booker → renderer never invoked for the booker.
      Internal model built for an internal message carries no contact details.

## 3. Web: the renderer, the base page, the registration

- [x] 3.1 `UBookItEmailPage<TModel> : RazorPage<TModel>` with typed `Subject` and `IsHtml` backed
      by `ViewData`. **`RazorPage<T>`, not `UmbracoViewPage<T>`** — that is what keeps rendering
      free of `IUmbracoContextAccessor`/`IPublishedUrlProvider` and therefore of a request.
- [x] 3.2 `RazorBookingTemplateRenderer`: resolve `~/Views/Partials/UBookIt/Emails/<Name>.cshtml`,
      render to a string, read `Subject`/`IsHtml` back off the `ViewDataDictionary` it owns.
      Distinguish not-found from threw.
- [x] 3.3 Register in the Web composer. **No `AddUnique`, no `ComposeAfter`** — presence is the
      whole mechanism, and `UBookItThemeRegistration` records why ordering is not trusted here.
- [x] 3.4 Tests: renders a template; **renders with NO ambient `HttpContext`** (the requirement
      that will otherwise fail only in the retention job); missing file → no-template, not an
      exception; throwing template → failed, and the exception does not escape; a template
      supplied by a referenced assembly is found.

## 4. Boot check

- [x] 4.1 Report per message whether content will be used, **established the way sending will
      establish it** rather than by restating registration — the theming boot check's rule.
- [x] 4.2 Tests: a misnamed file reports that message as unsupplied; a mixture reports both
      halves; the check does not throw when no renderer is registered at all.

*(3.1 note, recorded because it was a defect caught only by a round-trip test: the typed
`Subject`/`IsHtml` properties travel through **`HttpContext.Items`, not `ViewData`**. MVC
activates a `RazorPage<TModel>` with a **copy** of the supplied `ViewDataDictionary`, so a
template's writes land in the copy and the renderer read an empty entry — a stated subject
silently became no subject. The context is one shared instance and the renderer creates a fresh
one per message, so nothing leaks between renders. The author's surface is unchanged.)*

*(3.4 note: the email fixtures live in the existing `UBookIt.Tests.ThemeFixture` assembly, whose
exact-inventory guard promptly failed — as designed. It was extended to enumerate both kinds
rather than filtered by path prefix, so it remains an exact inventory of everything that
assembly compiles.)*

## 5. Shipped default templates — decide, then act

- [x] 5.1 **Decided: the package ships NO example templates.** Forms ships one; we do not, and
      the fixtures written in task 3.4 turned the lean into evidence. An example that reproduced
      the default wording would be a second copy of `SubjectFor`, `Details` and `ClosingLineFor`
      — three pieces of logic restated in markup, free to drift from the code that actually runs
      whenever no template exists. That is the two-copies-of-one-truth fault this project keeps
      finding, and shipping it into every install is the worst place to put it.

      **The worked example goes in the documentation instead, and is deliberately NOT a
      reproduction of the default.** It is short and obviously a site's own wording, so there is
      nothing for it to drift *from*: an illustration of the mechanism rather than a copy of the
      content. Guarded in 6.4 — the docs must not claim the example is what the package sends.

## 6. Documentation

- [x] 6.1 `docs/notifications.md`: the mechanism, the six names, the path, what each model
      carries, how to state subject and content type, and the worked example.
- [x] 6.2 **The single-body limit**, with its reason (bypassing `IEmailSender` would cost the
      site its transport and its interception seam).
- [x] 6.3 **What narrows and what does not**, as an explicit list — wording becomes the author's;
      gating, audiences, erased-booker and the internal-message exclusion do not.
- [x] 6.4 Documentation guards, wrap-safe in both directions. Include an absence check that no
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

- [x] 8.1 Clean Release build, zero warnings; full .NET and client suites green; strict validate.
- [x] 8.2 Live on the TestSite, pickup directory cleared first: no templates → messages identical
      to 0.6.0; supply `BookerPlaced` as plain text → used, subject default; state a subject →
      used; state `IsHtml` and supply HTML → arrives as HTML; break a template → fallback body
      arrives and the log names the failure; supply `InternalPlaced` → arrives with no contact
      details. Boot log lists supplied and unsupplied.
- [x] 8.3 **The no-request path could NOT be staged live, and here is exactly what was proved.**
      This task's own terms were "if that cannot be staged, say so explicitly and record what was
      actually proved" — it was ticked in round 1 with nothing recorded, which QA caught. Honestly:

      **There is no production code path today that composes a message outside a request.** Every
      send is placement (delivery API or Razor POST) or a backoffice confirm/decline/cancel. The
      retention job runs unattended but erases bookers; it sends nothing. So staging a live
      no-request send would have meant inventing a code path in order to test it, which proves
      the rig rather than the product.

      **What IS proved**, and it is not nothing:
      - `EmailTemplateRenderingTests.It_renders_with_no_ambient_request` renders from a provider
        that has no `IHttpContextAccessor` registered at all, asserted before rendering.
      - `It_declares_no_dependency_that_only_a_request_can_satisfy` pins it structurally, so the
        dependency cannot be added later without failing — the behavioural test alone could only
        speak for today's constructor.
      - The renderer builds its own `DefaultHttpContext`; nothing reads an ambient one.

      **What is NOT proved:** that a real background sender works, because there is not one yet.
      The requirement is anticipatory — it exists so that the reminder feature, or anything else
      that sends from a timer, does not discover this in production. Recorded as the honest
      residue rather than dressed up.

## 9. What the live check found, recorded rather than left in a log

- [x] 9.1 **A referenced assembly's template beats the site's own file at the same path**, and
      that is probably backwards. Observed live: the TestSite referenced the fixture assembly,
      both supplied `BookerPlaced.cshtml` at the same path, and the assembly's rendered. The
      site's own file won wherever the assembly supplied nothing (`InternalCancelled`), so loose
      site files DO work — runtime compilation resolves them, and a stated subject from one was
      used. What is unspecified is which wins when both exist.

      **Not fixed here, and the reasoning is recorded rather than the conclusion assumed.** The
      spec requires only that both sources work, which they do. The collision is an artifact of a
      test site referencing a test fixture; a real site referencing a template package is
      possible but not yet a thing that exists. Changing it means fighting the view engine's
      compiled-versus-runtime precedence, which is scope this change did not budget and which
      `theming` had to spend a whole boot check on last time. **Recorded as a deferred obligation
      and named in the docs is the honest minimum** — a site author who does hit it should not
      have to discover it from a message that ignored their file.

- [x] 9.2 **The boot check earned itself immediately.** It was what revealed the assembly's
      templates were being discovered at all — before any message was sent, and before the
      collision above could have been mistaken for "templates do not work".

## 10. QA round 1 — REJECT (six MAJOR, four MINOR, four NIT)

**The headline is not any single finding: four tasks were ticked without being done** (1.4, 6.4,
8.3, and part of 2.4). The tasks file is the record a reviewer reads to know what was verified,
and ticking unverified work corrupts the one artifact whose job is to be trustworthy. Every fix
below is mutation-proved against QA's own attack where QA supplied one.

- [x] 10.1 **MAJOR — the headline structural guarantee had no guard.** Task 1.4 claimed a
      reflection test over `InternalMessageModel`; none existed, and QA added `BookerEmail` to it
      with 2379 tests passing. Added `BookingMessageModelContractTests` over the whole public
      surface, inherited members included, plus the base type and an anti-vacuity test that the
      predicate still bites on `BookerMessageModel`. **The predicate is precise, not broad**: the
      first version flagged `ServiceName` and `ResourceNames`, and a guard that fires on
      legitimate members is one somebody relaxes. Mutation: QA's exact attack now fails.
- [x] 10.2 **MAJOR — `IsHtml` never reached the wire under test.** `isBodyHtml: false` passed
      2379 tests. Added `A_message_declared_as_html_is_sent_as_html`, a second test rather than
      another line in the existing one, because a guard that holds one value can only see one
      direction. Mutation: fails.
- [x] 10.3 **MAJOR — three outcomes collapsed to two at the composer.** Deleting the whole
      `Failed` branch passed. The two existing tests asserted identical observable facts, so
      neither could tell absence from breakage. Added a differential over the LOGS — a failure
      logs, an absence does not — plus a test that the line names the booking and no booker, with
      GUIDs redacted first. Mutation: two tests fail.
- [x] 10.4 **MAJOR — the narrowing was asymmetric.** Only the booker's requirement was narrowed,
      leaving two internal requirements stating positive wording obligations a supplied template
      can falsify. Both are now MODIFIED (see 0.2 and 0.3) — and each states explicitly that the
      **no-personal-data half does NOT narrow**, because that one is structural and sweeping it
      up would give away the guarantee this change exists to make.
- [x] 10.5 **MAJOR — task 6.4's absence check did not exist.** Added, swept across every shipped
      document, aimed at the reassurance somebody would plausibly write ("uBookIt still checks
      your wording"). Mutation, added and wrapped: fails. Also de-vacuified two presence checks
      that matched prose rather than the assignments an author writes.
- [x] 10.6 **MAJOR — task 8.3 ticked with nothing recorded.** Now records what could not be
      staged and why, and exactly what was proved instead. See 8.3.
- [x] 10.7 **MINOR — the non-leak claim was unguarded.** Hoisting the context to a field passed.
      Added a two-render test, stating one first so it can actually fail. Mutation: fails.
- [x] 10.8 **MINOR — `Contact!` would have thrown for an erased booker.** Unreachable through the
      send path, which establishes an address first, but the composer is public and a bare `!`
      turns a direct call into an NRE layers from the mistake. Now falls back, with a test.
- [x] 10.9 **MINOR — a directly-booked booking was read twice per claim.** The plain-text path
      already read those names. Now reused; the extra reads happen only for a service booking,
      which is the case that genuinely needs them. Test counts the reads.
- [x] 10.10 **MINOR — nothing tested the registrations.** Deleting either line disabled the whole
      feature silently. Both are now asserted, and the port is registered by factory so it and
      the concrete type resolve to one instance.
- [x] 10.11 **MINOR — no test pinned the message set.** Added: exactly six, and explicitly no
      `InternalConfirmed`/`InternalDeclined`.
- [x] 10.12 **NITs** — `design.md` §4 and `proposal.md` still described `ViewData` as the
      transport, which apply had measured false; both corrected in place with the reason.
      `BookingMessage`'s new positional parameter is called out as BREAKING in Impact (the 2-arity
      `Deconstruct` is gone). The proposal's claim that "the retention job already runs that way"
      implied it sends mail; it does not, and the sentence is removed.
