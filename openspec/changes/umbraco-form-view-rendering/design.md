## Context

`UBookIt.Tests.Rendering` renders the views compiled into `UBookIt.Web.dll` through
the real Razor engine, with a `NullFileProvider` content root and no
`WebApplicationFactory`, SQL or Umbraco boot. 415 tests, ~5s. Three of the
package's fourteen views were left out because they call `Html.BeginUmbracoForm`
and its render-time requirements were unknown.

They are now known. Everything in this section was measured against the resolved
Umbraco 17.6.2 binaries by rendering the actual views, not inferred.

## The spike result

`Html.BeginUmbracoForm<T>(action)` resolves, in order:

1. `SurfaceControllerTypeCollection` — to map the surface controller *type* to its
   controller *name*. Absent, it throws `InvalidOperationException: No service for
   type '…SurfaceControllerTypeCollection' has been registered`.
2. `IUmbracoContextAccessor`, via `GetRequiredUmbracoContext()` — so an accessor
   returning `false` is not enough; it throws *"Wasn't able to get an
   UmbracoContext"*.
3. Anti-forgery and data protection, to emit `__RequestVerificationToken` and the
   encrypted `ufprt` route token.

Confirmed against Umbraco source (`Umbraco.Web.Website/Extensions/
HtmlHelperRenderExtensions.cs:803-805`), which agrees with the binaries:

```csharp
IUmbracoContext umbracoContext = umbracoContextAccessor.GetRequiredUmbracoContext();
var formAction = umbracoContext.OriginalRequestUrl.PathAndQuery;
return html.RenderForm(formAction, method, htmlAttributes, controllerName, action, area, antiforgery, additionalRouteVals);
```

Measured facts that shape the decisions below:

| Question | Answer |
|---|---|
| `AddDataProtection()` / `AddAntiforgery()` needed? | **No.** `AddMvcCore` supplies both; removing them changes the output not at all, and real tokens are still emitted. |
| What of `IUmbracoContext` is read? | **`OriginalRequestUrl.PathAndQuery`, and nothing else.** The other eight members can be `null!`. |
| Cost | 30 renders in 832ms — and that rebuilt the whole service provider per render, which the rig does not. |
| Deterministic? | **No.** A per-render GUID form id and two freshly-encrypted tokens. Scrubbing exactly those three makes two renders byte-identical. |

## Goals / Non-Goals

**Goals**

- The three `BeginUmbracoForm` views enter the suite under the existing rules.
- `ViewInventory.Deferred` ceases to exist; the shipped set and the exercised set
  are equal, and their equality is checked.
- Document-level rules are asked of the flow views' real output, retiring the
  hand-built composition.

**Non-Goals**

- Any rule about the form's `action` or its anti-forgery token — see the proposal's
  Non-goals for why, and D4 below for the standing consequence.
- Any change to shipped views except as a reported defect fix.
- Anything about editor-facing packaging.

## Decisions

### D1 — Stub `IUmbracoContext`, do not boot Umbraco

Register an `IUmbracoContextAccessor` returning a stub whose `OriginalRequestUrl`
is a fixed URL and whose remaining members throw or return `null!`.

**Why not boot Umbraco:** it would cost the rig everything that makes it useful —
speed, and the guarantee that `RuntimeCompilation` is absent from the closure. The
existing test asserting that absence is load-bearing and must keep passing.

**Why a stub is honest here.** The requirement being tested is about markup. The
context contributes exactly one string to that markup — the form's `action` — and
the stub supplies it. It is not standing in for behaviour that is being asserted;
it is supplying a URL.

**The eight null members are a deliberate decision, not an oversight**, and this is
recorded so a later reader does not mistake it for sloppiness. If a future Umbraco
version has `BeginUmbracoForm` read `PublishedRequest` or a cache, the rig throws a
`NullReferenceException` naming the member. That is a **loud** failure, and loud is
the correct direction: the alternative — a stub that returns plausible empties —
would let the rig render a page the site cannot produce and report it green. Prefer
the throw. Do **not** "harden" the stub by giving its members benign return values.

Only `OriginalRequestUrl` is populated. `CleanedUmbracoUrl` is left unpopulated even
though it exists, because the source shows it is not read; populating it would be
inventing a fact about the collaboration.

### D2 — Register only what is required, and let the absence be the assertion

Register `SurfaceControllerTypeCollection` and `IUmbracoContextAccessor`. Do **not**
add `AddDataProtection()` or `AddAntiforgery()`.

The temptation is to add them "for safety". Resist it: they are not required, and
adding an unnecessary registration to a rig whose whole claim is *"this is what the
shipped artefact needs"* makes the rig lie by a little. If a future change makes
them genuinely necessary, the rig fails loudly and the registration is added with a
reason.

### D3 — The composed document comes from the flow view, and the fixture is deleted

`ViewFixtures.BuildDocuments` currently concatenates `_ErrorSummary`,
`_DateAndLength`, `_Times`, and `_YourDetails` (the last guarded by `HasTimes`) and
calls the result a document. Replace it: render `BookingFlow/Service.cshtml` and
`Booking/Default.cshtml` and use their output.

**Deleting the hand-built composition is the point, not a tidy-up.** Keeping both —
"render the real one *and* the composed one" — would preserve the exact liability
the change exists to remove: a second description of the page, free to disagree with
the first, with nothing to say which is right. One document, rendered.

The shared partials keep their standalone per-view cases (rule 2 and rule 3 are
per-view rules and are unaffected). It is only the **document**-level rule whose
input changes.

### D4 — The form's `action` and token stay unguarded, and that is recorded

The spike makes both testable and both are deliberately not tested. The consequence
must be stated rather than left to be discovered: after this change, the POST form's
`action` and its anti-forgery token are rendered, are visible in the output the
rules read, and are asserted by nothing. Deleting either would pass the suite.

This is the same judgement as `role="alert"` in the previous change — a markup
assertion is not a behaviour guarantee, and dressing one as the other is worse than
an honest gap. It is recorded here and in Risks so that "the rig renders the form"
is never mistaken for "the form is checked".

### D5 — Non-determinism is tolerated, not scrubbed

Three values vary per render: the GUID form id and the two tokens. The existing
rules are structural and are unaffected — none compares rendered output against a
stored expectation.

Do **not** add scrubbing infrastructure. It would exist to support snapshot testing
that nothing does, and the id's uniqueness-by-construction is a fact about the page
worth leaving visible to the id-uniqueness rule rather than hidden from it.

Consequence to know: the id-uniqueness rule now passes partly on an id nobody chose.
That weakens what a pass means by a little, and is worth stating because the honest
version of "every id is unique" is now "every id we author is unique, plus one that
is unique by GUID".

## Risks / Trade-offs

- **The honest document may fail the existing rules — expected, and the reason for
  the change.** The hand-built composition is known to have masked one defect of
  exactly this class. Any failure is a real defect in shipped markup. It should be
  reported as a finding and fixed as the defect's own fix, not folded silently into
  this change's diff.
- **A doubled `_ErrorSummary` remains invisible.** Obligation (b) noted that a flow
  view including the error summary twice is seen by no rule, because the summary
  carries no ids. Rendering the real composition does **not** fix this — it removes
  the *drift* risk, not this one. Carried forward unfixed and explicitly: it is the
  largest remaining blind spot in the document-level rule.
- **The two registrations are expected to be temporary.** Editor-facing packaging
  replaces the surface-controller round trip with a `RenderController` owning
  GET+POST, which may remove `BeginUmbracoForm` from these views entirely. This is
  scaffolding with a known demolition date and should not later be defended as
  architecture.
- **First Umbraco types in this suite.** `SurfaceControllerTypeCollection` and
  `IUmbracoContext` arrive transitively through `UBookIt.Web` — no new package
  reference, no new licence — but the test that `RuntimeCompilation` is absent from
  the closure must be re-run and must still pass. If it fails, this change stops.
- **No new third-party dependency.** Nothing is added to
  `Directory.Packages.props`. AngleSharp stays pinned at 1.7.1 (1.3.0 carries
  advisory GHSA-pgww-w46g-26qg and central package management must not drift back).

## Migration Plan

None. Test-only change; no shipped code, public API, contract or schema is altered,
so there is nothing to migrate and no upgrade path to provide.

## Open Questions — both resolved at apply

- **Does the real composition fail any existing rule? No — it found no markup
  defect.** Stated plainly because the proposal predicted it might, and a prediction
  that did not come true should be recorded as such rather than quietly dropped.
  Two tests did fail, and neither was a markup defect: `BookingFlow/Default.cshtml`
  failed the two **non-vacuity guards** (`Every_view_refers_to_at_least_one_model_member`
  and `The_literal_extraction_is_not_vacuous`), because a pure delegate names no
  model member and emits no literal of its own. Both guards already consult an
  explicit exemption registry, `ModelReferences.DelegatingViews`, holding two
  siblings of identical shape; the third was missing only because it was deferred
  and so never reached the rule. Registered with its reason. **The suite's silence
  is load-bearing here, so it was mutation-checked rather than trusted** — see
  below.
- **The two-byte difference is explained: one trailing `\r\n`.** The dispatcher's
  output is the delegated view's output plus the newline after its `PartialAsync`
  line — 3460 against 3458, and 3456 for both once trimmed. No markup difference.
  The delegate exemption rests on exactly this claim and had only ever been checked
  against view *source*, so it is now asserted against rendered output
  (`The_dispatcher_page_adds_no_markup_to_the_view_it_delegates_to`), comparing tag
  sequences rather than bytes — the form carries a per-render GUID id and two fresh
  tokens, and scrubbing them would be the snapshot infrastructure D5 rules out.

## What the mutations established

Every claim below is measured, not argued:

| Mutation | Result |
|---|---|
| Remove `SurfaceControllerTypeCollection` | 386 of 581 fail — required |
| Remove `IUmbracoContextAccessor` | 386 of 581 fail — required |
| **Add** `AddDataProtection()` + `AddAntiforgery()` | 581 pass, output unchanged — genuinely not required, D2 holds |
| Ship a view the suite does not exercise | 7 fail, naming it: *"is shipped and no fixture renders it, so every rule passes over it while reporting green"* |
| Empty the exercised set | The vacuity guard fails, as the spec requires |
| Flow view renders `_YourDetails` **twice** | Duplicate-id rule fails across many states, **with no fixture edited** — the composition tracks the view |
| Flow view renders `_ErrorSummary` **twice** | **581 pass** — the known blind spot, confirmed still exactly that and no larger |
| Dispatcher adds a `<div>` | The new structural test fails |
