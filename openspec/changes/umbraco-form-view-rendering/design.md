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

### D4a — QA's correction: the silent half is the attribute, not the markup

D4 below discusses the *markup* — the rendered token — and QA's review showed it
points at the wrong half, which is worth keeping because the reasoning error is
instructive.

The markup half fails **loudly**: delete `__RequestVerificationToken` from the
rendered form and every submission is rejected by `[ValidateAntiForgeryToken]`, so
the flow visibly breaks. A test for it would be guarding something that already
announces itself.

The **silent** half is the attribute. `BookingSurfaceController.cs` and
`ServiceBookingSurfaceController.cs` carry `[ValidateAntiForgeryToken]`, and nothing
in any suite asserts it. Delete either and submissions succeed **unprotected**, with
the whole solution green. That is a live requirement in the spec
(`default-frontend`, "Anti-forgery-protected submission") guarded by nothing.

Pre-existing and genuinely outside this change — it is a controller-attribute
concern, not a rendering one, and this change touches no `src/` file. Carried to
`ubookit-deferred-obligations` as its own item rather than resolved here, because an
obligation recorded inside a paragraph about something else is an obligation nobody
finds.

### D4 — The form's `action` and token stay unguarded, and that is recorded

The spike makes both testable and both are deliberately not tested. The consequence
must be stated rather than left to be discovered: after this change, the POST form's
`action` and its anti-forgery token are rendered, are visible in the output the
rules read, and are asserted by nothing. Deleting either would pass the suite.

This is the same judgement as `role="alert"` in the previous change — a markup
assertion is not a behaviour guarantee, and dressing one as the other is worse than
an honest gap. It is recorded here and in Risks so that "the rig renders the form"
is never mistaken for "the form is checked".

### D5 — Non-determinism is stripped at the comparison, never in the renderer

**This decision was wrong as first written, was caught by QA, and is restated here
with the error left visible rather than quietly replaced.**

The original text read: *"The existing rules are structural and are unaffected — none
compares rendered output against a stored expectation. Do not add scrubbing
infrastructure."* The first clause is true of a **stored** expectation and false of
the thing that matters. Rule 2 decides whether a model member is live by rendering
twice and comparing **one render against another** — and non-determinism breaks that
harder than a snapshot, because a snapshot at least fails loudly. Here both routes
through `IsLiveAsync` silently invert: the strong route's equality is never true, the
weak route's inequality always is, and `CoVariesAsync` is never reached. **Every
member of the three new views was reported live while nothing was checked** — rule 2
made vacuous on precisely the views this change exists to cover, and on the views
hardest to check by reading.

Demonstrated, not argued: a provably dead `ServiceName` branch in `Service.cshtml`
passed the entire suite, while the identical shape in the deterministic
`_DateAndLength.cshtml` failed. The fix is confirmed by the complementary run —
with stripping disabled the dead branch passes again, so the strip is what catches
it.

The decision now: three values vary per render (the GUID form id, the anti-forgery
token, the `ufprt` token). They are removed **at the comparison** — one home,
`RenderNondeterminism` — and never in the renderer. Every other rule still reads the
real output: the id-uniqueness rule still sees the form id, the markup rules still
see the tokens. Only "did changing the model change the page" is asked of a
projection, because only that question is corrupted by an answer that changes on its
own.

The tokens are matched by **name** (`__RequestVerificationToken`, `ufprt`) rather
than by payload shape, since a data-protection prefix is an ASP.NET Core
implementation detail while those two names are the contract the form posts under.

Two guards keep this from silently reverting, which matters because the failure mode
is a rule that passes: `Two_renders_of_one_model_compare_equal` over **every** shipped
view, so a fourth source of variance is caught when it appears rather than when
someone notices a rule stopped working; and
`The_stripping_fires_where_there_is_something_to_strip`, which asserts the flow view
carries exactly three varying values and a deterministic partial carries none — so
if an Umbraco version changes either shape and the patterns match nothing, it fails
naming the count instead of going quietly vacuous.

Standing consequence, unchanged from the original: the id-uniqueness rule passes
partly on an id nobody chose. The honest version of "every id is unique" is "every id
we author is unique, plus one that is unique by GUID".

**The transferable lesson is not about tokens.** It is that admitting a new
collaborator (`BeginUmbracoForm`) admitted a new *property* (non-determinism), and
the design reasoned about that property against the wrong rule. D1 and D2 examined
what the collaborator needed; nothing examined what it changed about the output the
existing rules read.

### D6 — The exemption scenario is forward-looking, and says so

QA observed that requirement 1's scenario *"an exemption is recorded with its reason
and with what would lift it"* has no mechanism behind it and is vacuously satisfied:
`ViewInventory` now has no exemption facility at all, because nothing is exempt.

That is correct, and it is not repaired by inventing a facility with no user — an
abstraction with zero implementations is the liability this project has declined to
build before (⑨-2 D1). It is left as a constraint on the **next** change that needs
to defer a view, which is what a spec is for.

But the sentence blurs two different things, and the distinction is worth fixing in
whichever change next legitimately touches that requirement:

- A **deferral** is temporary and owes "what would lift it" — the shape the three
  `BeginUmbracoForm` views had, where the answer was "a rig that can host the
  helper", and its absence is how the deferral stayed invisible.
- A **structural exemption** is permanent and cannot have one. `ModelReferences.
  DelegatingViews` is this: a pure delegate names no model member and emits no
  literal because of what it *is*, not because of what the rig cannot yet do.
  Nothing would ever lift it, so demanding the phrase would produce a ritual
  sentence.

Recorded rather than acted on, because rewriting the requirement now would mean
replacing a requirement this change only just added — for a wording refinement, which
is the failure mode the whole ADDED-not-MODIFIED decision was made to avoid.

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

Every claim below is measured, not argued. **Re-derived on the final 597-test shape
after QA observed the first table had been tabulated on an earlier one** — the exact
hazard this project names by name, and worth recording as having recurred.

| Mutation | Result |
|---|---|
| Remove `SurfaceControllerTypeCollection` | 391 of 597 fail — required |
| Remove `IUmbracoContextAccessor` | 391 of 597 fail — required |
| **Add** `AddDataProtection()` + `AddAntiforgery()` | all pass, output unchanged — genuinely not required, D2 holds |
| Ship a view the suite does not exercise | fails, naming it: *"is shipped and no fixture renders it, so every rule passes over it while reporting green"* |
| Empty the exercised set | The vacuity guard fails, as the spec requires |
| Flow view renders `_YourDetails` **twice** | Duplicate-id rule fails across many states, **with no fixture edited** — the composition tracks the view |
| Flow view renders `_ErrorSummary` **twice** | **597 pass** — the known blind spot, confirmed still exactly that and no larger |
| Exempted delegate adds a `<div>` | The structural test fails |
| **Dead `ServiceName` branch in `Service.cshtml`** | **fails**, naming view and member — rule 2 is live on a flow view |
| The same, with stripping disabled | **passes** — the pre-fix behaviour, so the strip is what catches it |

The last two are a matched pair on purpose. The first alone would only show the test
failing; the pair shows *this fix* is why.
