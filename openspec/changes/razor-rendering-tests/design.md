## Context

`UBookIt.Web` builds with `Microsoft.NET.Sdk.Razor` and `AddRazorSupportForMvc`,
so every `.cshtml` is **compiled into `UBookIt.Web.dll`** — the view paths are
present as strings in the built assembly, and the TestSite discovers them through
an MSBuild-generated `ApplicationPartAttribute`. Three facts follow, and they shape
everything below.

**There is nothing to compile at test time.** The rig renders the same artefact the
site serves. A rig that re-parsed `.cshtml` from disk would be testing a second
copy of the views, free to disagree with the shipped one — which is the class of
fault this project keeps finding elsewhere and would be absurd to introduce here.

**The views are unusually self-contained.** `UBookIt.Web` has no `_ViewStart.cshtml`
and no `_ViewImports.cshtml`. There is no layout to satisfy and no implicit
`@using` to reproduce; each view renders standalone. (The absent `_ViewImports` is
also *why* tag helpers degrade to visible text rather than failing the build — the
hazard this change exists to catch.)

**Only three views need Umbraco, and the dependency is transitive.**

```
  Booking/Default ◄──────────── BookingFlow/Default          DEFERRED
    (BeginUmbracoForm)             (one-line delegate)
  BookingFlow/Service
    (BeginUmbracoForm)
         │
         ├───────────┬──────────┬───────────┐
         ▼           ▼          ▼           ▼
   _ErrorSummary  _DateAndLength  _Times  _YourDetails        IN SCOPE
                                                              (rendered
  Catalogue    Booking/Confirmation ◄── BookingFlow/Confirmation  composed, D7)
               Booking/Unavailable  ◄── BookingFlow/Unavailable
               ServiceConfirmation      ServiceUnavailable
```

Branch density is concentrated: 33 conditional keywords across the fourteen views,
of which `_DateAndLength.cshtml` alone carries 11 — the densest file by a distance,
and where ⑩-1's defect was.

## Goals / Non-Goals

**Goals:**

- A markup defect can fail a test, for the first time in this repository.
- The accessibility bar's mechanically checkable clauses are enforced against
  rendered output rather than review.
- A view that cannot render a state its model expresses fails, without anyone
  having thought about that particular branch.
- The fast unit suite stays fast.

**Non-Goals:**

- The three Umbraco-dependent views, and with them flow-view/partial composition
  (D2).
- Full HTTP, the ViewComponents, snapshot testing, human accessibility passes —
  see the proposal's Non-goals.
- Any claim that passing these rules means a page is accessible (D6).

## Decisions

### D1 — Render the compiled views through the real view engine

The rig builds a `ServiceProvider` with MVC's Razor view engine, adds
`UBookIt.Web` as an application part so the compiled views are discoverable, and
calls `IRazorViewEngine.GetView` by absolute path, rendering into a `StringWriter`
over a `DefaultHttpContext`.

Rendering by **absolute view path** rather than by name, deliberately: the views
live under `Views/Shared/Components/…` and `Views/Shared/UBookIt/…`, and name-based
lookup depends on controller/view-component conventions the rig has no business
reproducing. The flow views reference their partials by absolute path already, so
the rig addresses views the way the product does.

**Alternatives considered.** *Runtime compilation from `.cshtml` on disk* — rejected:
it needs the content root wired up, is slower, and tests a re-parse of the source
rather than the shipped assembly. *Extracting the markup into strings and asserting
on those* — rejected as not rendering at all, which is the entire gap.

### D2 — The Umbraco views are deferred, not stubbed hastily

`Html.BeginUmbracoForm` is the only Umbraco dependency in any view. From the
rendered output captured during ⑩-1's live pass, it emits an action of the current
request URL, a `ufprt` token and an anti-forgery field — which *suggests*
`DefaultHttpContext` plus `AddDataProtection()` plus `AddAntiforgery()` and no
booted Umbraco context. **That is a hypothesis, not a finding**, and settling it is
a spike.

It is deferred rather than attempted here because the cost is asymmetric: if the
hypothesis is wrong, those three views need a booted Umbraco context, and a suite
that boots Umbraco is a different kind of suite with a different runtime. Holding
eleven views back to find out would be the wrong trade. The follow-up runs the
spike first and decides with the answer in hand.

**What this change therefore does not cover, stated plainly** so it is not
mistaken for coverage: the four partials are only ever *included* by the three
deferred views. Rendering them — composed into one document, per D7, rather than
individually — proves the partial set is internally sound; it does not prove
`Service.cshtml` composes them, which is exactly what ⑤'s tag helper broke. The existing source scan asserting that each flow view names each
partial's path stays, and stays load-bearing, until the follow-up lands.

### D3 — The property list is derived from view source, and the derivation must be non-vacuous

For each view, the rule extracts the model properties its source references —
`Model.X`, `Model?.X`, and any other form Razor admits — and requires each to
change the rendered output. **How strictly is decided in D8**, which supersedes the
"at least one exercised state" this decision originally carried: that turned out
not to catch the defect the change exists for.

Deriving beats declaring for one reason: a declared list fails the same way the
defect does. Someone adds a branch, does not add it to the list, and the list is
silent about what it does not contain. A derived list grows when the view does.

**The derivation is itself a test asset and can fail vacuously**, which is the
trap. A regex missing `Model?.X` reports `ServiceUnavailable.cshtml` as referencing
nothing, and a view referencing nothing passes every property check trivially —
green, and worthless. Two guards, both required:

- Every view in scope SHALL yield at least one referenced property, asserted; a
  view genuinely referencing none (a pure delegate such as
  `BookingFlow/Unavailable.cshtml`) is an **explicit, named exemption**.
- The extracted set for at least one known view SHALL be asserted against its
  expected contents, so a derivation that silently stops finding things fails.

**Alternatives considered.** *Reflecting over the model type's properties instead of
the view's source* — rejected: a view is not obliged to render every property of
its model (`_Times` has no business with `ResourceChoices`), so this would demand
falsehoods. The question is about what the view *claims to use*.

### D4 — "Varying it changes the output" needs a base state per view, not one global state

Some properties are only live in particular states — `LongestAvailableMinutes` is
rendered only when `LengthIsTheProblem`. A single base model per view would report
those as dead.

So each view supplies a small set of base states spanning its own shape (has
errors / no errors, has times / none, length fixed / chosen, offers a choice / not),
and a *value* is satisfied by changing the output in at least one of them. That
keeps the rule honest for content without demanding a state matrix nobody can read.

**Flags are held to more than this** — see D8. Applying "at least one" to them is
precisely what let ⑩-1's defect through when the rule was first written.

The per-type variation strategy is mechanical: flip a bool, change a string, empty
or populate a collection, move a number. Where a property's type admits no
meaningful variation the exemption is explicit, per D3.

### D5 — One test per rule, iterating the views, naming the view it failed on

The reference-resolution rule is one test over every view and every state, not one
test per view. It reads as what it is — "the bar holds" — and a bar stated once
should be asserted once, which is the same argument `default-frontend` already
makes for stating it once.

The cost is failure-message quality, and it is paid off directly: every assertion
carries the view path and, for the property rule, the property name. A failure says
which view and which reference, which is the information needed to find it.

### D7 — Rule 1 is asked of a document, and the four partials form one together

*Added at apply time.* Rule 1's clauses — ids unique, aria references resolve —
are properties of a **document**. A shared partial is not one.

`_DateAndLength.cshtml` describes its length control with
`aria-describedby="ubookit-length-unavailable"`, an id that `_Times.cshtml` owns.
Rendered apart, that reference dangles and rule 1 fails; rendered together it
resolves, and together is what the site serves. Forward references are legal ARIA,
so the view is correct and the rig was wrong.

So the unit rule 1 is asked of is a document, built by rendering its parts in the
order a flow renders them. For ten views that is one view; for the four partials it
is the four. **Exempting the id would have been the easy fix and the wrong one** —
it would have switched off a real check to accommodate a rig error.

### D8 — "Changes the output in some state" is not enough for a flag

*Added at apply time, and the most important correction in this change.* The rule
as proposed — for each referenced property, some state in which varying it changes
the output — **does not catch ⑩-1's defect**. Verified by reintroducing the defect
and watching the rule stay green.

The reason is exactly why that defect was hard to see: `ResourceChoiceWasReset` was
live wherever a choice control existed and dead only where none did. An
exists-a-state rule finds the live states and passes.

So a settable boolean is held to a stronger form: wherever the model **sets** it,
the page must render something it would not render otherwise. Two restrictions,
each earned by a false positive rather than assumed:

- **Only where it is settable.** The shared partials render both form models, and
  `LengthIsFixed` is init-only on the service model and `=> false` on the resource
  one. Judging a state that cannot express the flag reports a fault that is not one.
- **Only where it is set.** Turning the reset flag *on* in the refused-choice state
  changes nothing, correctly — the error against that control takes precedence and
  says the same thing more strongly. The claim is "if the model says show this, the
  page shows it", not "toggling this from anywhere changes something".

Narrowing beat exempting: an exemption would have switched the rule off for the
very member the defect was in.

### D9 — A live property does not mean a live branch, so branches are checked too

*Added at apply time, and found by the control mutation (task 5.6a) rather than by
reasoning.* Forcing the catalogue's `@if (Model.HasEntries)` to `true ||` leaves
both `HasEntries` and `Entries` changing the output — the two states still render
differently — while "there is nothing available to book" becomes unreachable. The
property rule passes.

A property can be live while a branch it selects is dead, so branches are checked
separately: every literal `id` and `class` a view can emit must appear in some
rendered state. That is a **proxy** for branch coverage rather than instrumentation
of the Razor, which is why it is paired with the property rule rather than replacing
it — neither catches the other's case, and the pair was arrived at by mutation from
opposite directions.

It found two unreached branches immediately, both fixture gaps rather than dead
markup, and both real pages: the refused-pin redraw (a choice control *with* an
error against it) and the conflict redraw (times *with* an error). Fixed by adding
those states, per task 3.3 — a branch nothing renders is either dead or untested,
and exempting it would have recorded the second as the first.

### D6 — These rules do not claim a page is accessible

Stated in the spec, and worth stating twice: a document can resolve every reference
it makes, carry no duplicate id and name every control, and still fail WCAG 2.2 AA
comprehensively — reading order, colour contrast, focus visibility, and whether the
text makes sense are all outside what a DOM assertion can see.

The rules check the half that regresses silently. The human screen-reader and
keyboard passes are still owed, and this change must not be recorded as having
discharged them.

## Risks / Trade-offs

**The rig renders views the site never renders that way** — the partials composed
into a document of their own rather than through the flow view that includes them
→ Accepted and named in D2 and D7. It is the difference between "this partial set
is sound" and "this page is sound"; the second needs the follow-up.

QA sharpened this: the composition must match what the flow views actually do, or
it masks. Both flow views render `_YourDetails` only `@if (Model.HasTimes)`, and
composing it unconditionally meant an error against a booker field always found its
target — including in states where the real page renders no booker fields at all.
The composition now carries the same guard, and the state that exposes it (errors
with no times) is exercised. That found a real defect: the error summary linked to
controls that were not on the page.

**A vacuous derivation passes silently** (D3) → The two non-vacuity guards, both
asserted rather than assumed. This is the failure mode most likely to make the
whole change worthless, because it fails *green*.

**AngleSharp's parser is lenient**, as browsers are, so malformed markup may parse
into a tidy DOM and hide the defect → Where a rule is about the raw output rather
than the tree — the tag-helper residue check especially — it is asserted against
the **string**, not the parsed document. A `<partial>` element parses perfectly
happily.

**The rules may fail on the current views** → That is a success, not a setback; the
change fixes what they find. But it means the work is not purely additive and the
estimate should not assume it is.

**`role="alert"` on the summary is unguarded, and is now the largest such property
of the surface these rules cover.** Deleting it passes the whole suite. It is the
announcement mechanism for a no-JavaScript redraw: without it a screen-reader user
lands on a re-rendered page with the problems stated and nothing said. → Named here
rather than fixed, because a DOM assertion cannot judge whether the announcement
*works* — that is the human pass still owed since ⑤ — and asserting only that the
attribute is present would be a rule about markup wearing the clothes of a rule
about behaviour. The reason it stands out is that everything around it now dies
under mutation, so the gap is worth writing down before someone later finds it and
assumes nobody looked. The WCAG requirement is out of this change's scope by its
own statement; this is where the boundary happens to fall.

**Test-only dependency added** → AngleSharp ships nothing. The package's
dependency-free promise is about what a site installs, and nothing here is
installed. Worth stating because "no dependencies" is an invariant of this repo and
a reviewer should not have to work out that it does not apply.

## Migration Plan

Additive: a new test project, a new solution entry, a new CI step. No product code
changes unless a rule finds a defect. Rollback is deleting the project.

## Open Questions

- **Does `BeginUmbracoForm` need a booted Umbraco context?** Deliberately left open
  (D2); the follow-up spikes it first.
- **Should the reference-resolution rule eventually run over the deferred three
  too?** Yes in principle — the rule is written per-view and takes them for free
  once they render. Confirming that is the follow-up's job, not a reason to
  generalise the rule speculatively now.
