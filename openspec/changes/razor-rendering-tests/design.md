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
  Catalogue    Booking/Confirmation ◄── BookingFlow/Confirmation  standalone)
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
deferred views. Rendering them standalone proves each partial is internally sound;
it does not prove `Service.cshtml` composes them, which is exactly what ⑤'s tag
helper broke. The existing source scan asserting that each flow view names each
partial's path stays, and stays load-bearing, until the follow-up lands.

### D3 — The property list is derived from view source, and the derivation must be non-vacuous

For each view, the rule extracts the model properties its source references —
`Model.X`, `Model?.X`, and any other form Razor admits — and requires each to
change the rendered output in at least one exercised state.

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
and the rule is satisfied when varying a property changes the output in **at least
one** of them. That keeps the rule honest without demanding a state matrix nobody
can read.

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

### D6 — These rules do not claim a page is accessible

Stated in the spec, and worth stating twice: a document can resolve every reference
it makes, carry no duplicate id and name every control, and still fail WCAG 2.2 AA
comprehensively — reading order, colour contrast, focus visibility, and whether the
text makes sense are all outside what a DOM assertion can see.

The rules check the half that regresses silently. The human screen-reader and
keyboard passes are still owed, and this change must not be recorded as having
discharged them.

## Risks / Trade-offs

**The rig renders views the site never renders that way** — standalone partials,
without their host view → Accepted and named in D2. It is the difference between
"this partial is sound" and "this page is sound"; the second needs the follow-up.

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
