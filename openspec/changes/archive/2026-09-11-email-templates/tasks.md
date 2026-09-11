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

- [x] 0.2 `booking-emails` / "The internal message says when a booking awaits action" — added
      in QA round 1. Carried verbatim: the state-not-setting rule and all three scenarios. Added: the
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

- [x] 7.1 Outward sibling-spec grep over capabilities this change does not touch. Needles:
      "the package composes", "what the message says", "plain text", "derived from that state",
      "no booker name". Known candidates to open: `booker-erasure`, `sensitive-data`,
      `privacy-notice` (it describes what a booker is told and was the CRITICAL last change),
      `theming` (states what a theme covers — must not read as covering email),
      `default-frontend`.
- [x] 7.2 Purpose prose check on `booking-emails` — its Purpose says "what that message carries,
      and — more of the point — what it must never carry or promise", which must still be true
      once content can come from a site.

## 7a. What the sync-time greps actually found (executed at sync, 2026-09-11)

- [x] 7a.1 **7.1 outward grep — three hits outside the touched capabilities, two benign.**
      `booker-erasure` "A hand-off carries no more than its purpose requires" survives and is
      strengthened: it requires that no message to any *other* recipient carries contact
      details, which is exactly what the two-model split makes structural. `booking-retention`
      and `sensitive-data` matched on "no booker name" but are about the sweep report and an
      API response, neither of which this change touches. `privacy-notice` survives because
      every sentence it owns is about *whether* a message is sent, and this change enumerates
      that as not narrowing.
- [x] 7a.2 **`theming` Purpose falsified, and fixed.** It called a theme "the third and last
      tier of customisation" — unqualified. Supplying email content is a fourth thing a site
      can customise, so "last" had quietly become wrong. Qualified to "of the booking flow's
      rendering", with the reason stated inline rather than left as a bare word, and a pointer
      to `email-templates`. Its completeness requirement needed nothing: it already scopes
      itself to "the views of both public view components", so email views cannot be read into
      it.
- [x] 7a.3 **7.2 `booking-emails` Purpose over-claimed, and fixed.** "what that message
      carries" is now true only of the messages the package composes. Added a paragraph
      splitting the sentence: the *carries* half narrows to composed messages, while the
      *never carry* half and the whether/to-whom of sending do not narrow at all. This is the
      same asymmetry the requirements state; the Purpose had summarised only the pre-change
      version of it.
- [x] 7a.4 One factual error corrected in the new capability’s Purpose before commit: it
      said messages "are already sent from unattended work". They are not — design §6 records
      that no such sender exists yet and the no-request requirement is anticipatory. Reworded
      so the Purpose does not assert a sender the package does not have.
- [x] 7a.5 **Both 7.1 and 7.2 were ticked before they were run**, and this section exists
      because of it. The same fault QA caught at round 2 of this change (four tasks ticked
      undone) recurred at the last step, on the two tasks whose whole purpose was to be
      executed later. Ticking is not evidence; this is.

## 8. Verification

- [x] 8.1 Clean Release build, zero warnings; full .NET and client suites green; strict validate.
- [x] 8.2 Live on the TestSite, pickup directory cleared first: no templates → messages identical
      to 0.6.0; supply `BookerPlaced` as plain text → used, subject default; state a subject →
      used; state `IsHtml` and supply HTML → arrives as HTML; break a template → fallback body
      arrives and the log names the failure; supply `InternalPlaced` → arrives with no contact
      details. Boot log lists supplied and unsupplied.
- [x] 8.3 **The no-request path could NOT be staged live, and here is exactly what was proved.**
      This task's own terms were "if that cannot be staged, say so explicitly and record what was
      actually proved" — it was ticked in round 1 with nothing recorded, which QA caught.
      Honestly:

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
      implied it sends mail; it does not. **Round 2 correction: it was removed from
      `proposal.md` only** — it stood in `design.md` §6 and in two XML doc comments, in the very
      commit recording its removal. Now gone from all four, each replaced with the accurate
      statement that no sender outside a request exists yet and the requirement is anticipatory.

## 11. QA round 2 — REJECT (one CRITICAL, four MAJOR, two MINOR, one NIT)

**A round-1 fix caused the round-2 CRITICAL** — the pattern this project has now hit on four
consecutive changes, and one I explicitly warned the reviewer about in the round-2 handover
before doing it myself.

- [x] 11.1 **CRITICAL — the 10.9 performance fix corrupted the published model.**
      `DescribeStructuredAsync` reconstituted `ResourceNames` by splitting the joined "What:" line
      on `", "`, so a resource an editor named `"Studio 2, Ground Floor"` reached content as TWO
      resources — a booking that does not exist, in a member frozen at 17.0.0, and precisely what
      design.md §3 and the member's own doc comment forbid in as many words. The round-2 guard
      could not see it: its fixture was `"Treatment Room"`, a sample rather than the class.

      **Fixed by carrying the list, never re-deriving it.** `DescribeAsync` now returns one
      `Described` record holding the text AND the parts, computed once; the line is derived from
      the list and the list is never derived from the line, because joining is lossy and no
      separator makes it otherwise. Tests on both paths use names containing the separator.
      *Round 3 correction: the parenthetical here originally claimed "the saving 10.9 was
      reaching for is still taken". That was true of the direct path and INVERTED for the service
      path — see 12.1.*
- [x] 11.2 **MAJOR — the round-2 predicate did not cover the class it named.** Its own remarks
      claimed it matched "an address or a number"; it matched neither word, and QA added
      `Address`, `Mobile`, `CustomerName` and `PlacedBy` with 1356 tests passing. **Inverted to an
      allow-list**, so the guess disappears: anything not explicitly permitted fails, whatever it
      is called, and adding a legitimate member is a deliberate act with the reasoning in front of
      it. Now covers fields as well as properties. Mutation: all four caught, by name.
- [x] 11.3 **MAJOR — nothing observed that `ForSiteAsync` uses supplied content.** Rendering the
      internal template and discarding it passed everything, which mattered more after round 2
      added four scenarios about what a supplied internal view owns. Two tests added.
- [x] 11.4 **MAJOR — task 8.1 was ticked against an unread result, and the build was NOT
      warning-free.** An `xUnit2031` warning was introduced by the round-1 commit whose headline
      was that very lesson. **How it happened is worth recording**: the `--no-incremental` build
      was piped to `tail -3`, which cut the warnings line off, and the follow-up `grep` ran an
      INCREMENTAL build that did not rebuild the test project — the stale-build trap, in the
      verification step itself. Fixed, and re-verified with `--no-incremental`.
- [x] 11.5 **MAJOR — the boot-check registration test did not test the boot check.** Its body
      asserted two renderer descriptors; deleting the `AddNotificationHandler` line passed.
      Now asserted. Mutation: fails.
- [x] 11.6 **MINOR — the retention-job sentence stood in three more places.** See 10.12.
- [x] 11.7 **MINOR — the absence needles were first/second person only**, so the same
      reassurance written about "the package" passed, and nothing guarded the INTERNAL over-claim
      that round 2's own new requirements had just made narrowable. Both classes added.
- [x] 11.8 **NIT accepted, not fixed.** The structural no-request test checks constructor
      parameter types — the mechanism rather than the guarantee, since a request dependency could
      arrive through the injected `IServiceProvider`. It is deliberately paired with
      `It_renders_with_no_ambient_request`, whose rig registers no accessor at all, so a lazy
      resolve would fail there. The pair carries the claim; neither does alone. Recorded rather
      than papered over.

- [x] 11.9 **Live check re-run for the CRITICAL, and it passed end to end.** QA asked
      specifically for a directly-booked booking whose resource name contains the separator,
      rendered through a template that loops `ResourceNames`. Done on the TestSite: a resource
      renamed to `Studio 2, Ground Floor`, a booking placed against it, and the internal
      template's `@foreach` printed **one** line — `- Studio 2, Ground Floor`. Before the fix it
      would have printed two. Composer → model → real Razor loop → `.eml`, not a unit rig.

      *Two things learned while staging it, neither a product defect.* The dev harness picks the
      **first resource by list order**, so renaming a resource moves it in the sort and the
      harness silently selects a different one — which cost several minutes looking for a defect
      that was not there; use `?resourceId=` to target one. And updating a resource through the
      management API requires sending `directlyBookable` explicitly, since a PUT built from the
      read model drops it. The TestSite was restored to its original state afterwards.


## 12. QA round 3 — REJECT (one MAJOR, two MINOR, two NIT)

**Fourth consecutive round in which the same resource-read optimisation produced the next
finding.** ㉓'s lesson applies exactly — *when a fix produces a defect twice, stop fixing and look
at the shape* — and it took a reviewer to say so.

- [x] 12.1 **MAJOR — the round-2 fix moved the cost onto the no-renderer path.** `DescribeAsync`
      began reading resources for a SERVICE booking, and it is called before the
      `if (!SupportsTemplates)` gate — so a site supplying nothing started paying a read per
      claim per message on every service booking, where it had paid none. Content stayed
      byte-identical, which is why nothing noticed: every other test looks at content.
      **The method's own comment claimed the opposite**, and design.md still listed "invisible —
      in behaviour and in cost" as a goal.

      **Fixed by making the cost a decision the caller states**, not a side effect of how the
      description happens to be built: `DescribeAsync(booking, withParts, ct)`. A service booking
      reads only when parts are wanted; a direct booking reads either way, because the plain-text
      line IS those names and there is nothing to save. Comment and goal both corrected to what
      the code does.

      **And the guard that was missing through all four rounds now exists**: a four-cell theory
      over booking shape × renderer presence, asserting the READ COUNT. Each round justified a
      cost with an argument and none measured it; a single-cell test would have passed in three
      of the four. Mutation — reinstating the unconditional read — fails the no-renderer service
      cell.
- [x] 12.2 **MINOR — the allow-list inspects properties and fields, not methods.** Its remarks
      said "everything a type exposes publicly", which was wider than the mechanism. Methods are
      deliberately out: a record generates seven, and permitting them would turn the list into
      compiler output nobody reads — an unread allow-list stops being a decision. Residue now
      stated in the remarks rather than implied.
- [x] 12.3 **MINOR — the `EqualityContract` allow-list entry was dead.** QA showed it comes back
      only under `NonPublic`, so the entry documented a defence never exercised. Removed.
- [x] 12.4 **NITs** — design.md §6's correction rewrapped to the file's ~100 columns (this
      repository's doc guards are wrap-sensitive by design); tasks 11.8 and 11.9 reordered.
- [x] 12.5 **Banked as a deferred obligation, per QA's suggestion:** a management-API PUT built
      from the resource read model **drops `directlyBookable`**, because the read model and the
      request model disagree about it. Hit while staging 11.9's live check. A site author
      round-tripping a resource through the management API would silently un-publish it from the
      front end. Not this change's to fix — recorded in agent memory.

## 13. QA round 4 — REJECT (one MAJOR, two MINOR, one NIT)

**The residual finding for three rounds running has been the same one**, and QA named it as the
lesson worth carrying: *apply the correction to the class, not to the instance the reviewer
named.* It happened again in this very commit — the rewrap fixed three of four lines. So each
item below was fixed as a class and then swept.

- [x] 13.1 **MAJOR — the cost theory measured one of the two callers.** Reverting `ForSiteAsync`
      alone reinstated half the round-3 defect with everything green; the regression was measured
      at TWO reads per service booking, one per message, and the guard watched one. **My argument
      for leaving it — "both call the same method with the same flag" — was an argument about how
      today's code happens to be written**, which is precisely the reasoning 12.1 exists to
      retire. Accepting it would have re-adopted it in the commit that retires it. The theory now
      has an audience dimension: eight cells. Mutation — QA's exact half-revert — fails.
- [x] 13.2 **MINOR — task 12.4 was ticked and was not done.** The rewrap fixed three lines of a
      four-line paragraph and left one at 183 columns. Fixed properly by reflowing whole
      paragraphs, then **swept**: every prose line in every artifact of this change is now under
      100 characters. *(Measured in characters, not bytes — `awk length` counts bytes and reports
      false positives on lines containing `—` or `㉓`, which is what three of the "remaining"
      lines turned out to be.)* Scenario `WHEN`/`THEN` bullets are deliberately left long: the
      main specs do the same, so that is the house style rather than a violation.
- [x] 13.3 **MINOR — no composer fixture claimed more than one resource.** So the composer half
      of the guarantee the collection exists for was unobserved: it was shown only in the
      rendering suite against a hand-built model. The fixture takes a claim count, and a new test
      composes a three-resource service booking. It also makes the read-count cells mean what
      they say — with one claim they could not distinguish "reads each claim" from "reads one and
      stops".
- [x] 13.4 **NIT — the `Described` record carried two `<summary>` blocks**, the first an orphan
      from round 3 describing the old contract, which was now wrong as well as duplicated.
      Removed, and the other `.cs` files this change touches swept for the same shape.

## 14. QA round 5 — APPROVE WITH NITS, nits taken

- [x] 14.1 Two mangled fragments the round-4 rewrap left (`QA` alone at column 0; `the` /
      `difference.` on their own lines) — an earlier single-line break the paragraph reflow then
      preserved. Fixed, and swept: the only other short lines are legitimate paragraph endings,
      each terminating in punctuation.
- [x] 14.2 **The audience dimension was not self-falsifying.** All eight expectations are equal
      across the two audiences, so collapsing the dispatch so every cell ran `ForBookerAsync`
      passed 8/8 — silently restoring the half-blindness round 4 found. The dispatch is now
      total, and each cell asserts the subject its audience produces, so a wrong dispatch fails
      on its own. Mutation: four cells fail.
- [x] 14.3 **Nothing pinned the composer's message-producing surface**, so a third method — and
      design.md §6 anticipates a reminder sender — would be unmeasured by the cost theory with
      nobody told. Now asserted to be exactly `ForBookerAsync` and `ForSiteAsync`. Mutation:
      adding a third fails. *This is the one remaining place the recurring failure of this change
      could recur, which is why a NIT was worth taking.*
- [x] 14.4 Dead `ResourceId` field removed — the last reference to the single-claim fixture shape.
- [ ] 14.5 **Out of scope, flagged for whoever touches it:** one adjacent `</summary>`/`<summary>`
      pair remains in the repository, at `tests/UBookIt.Tests.Rendering/Support/ViewInventory.cs:74`,
      in a file this change never touches. Recorded so it is not found twice.
