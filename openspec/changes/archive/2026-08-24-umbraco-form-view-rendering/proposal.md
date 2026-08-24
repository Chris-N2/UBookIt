## Why

`razor-rendering-tests` shipped a rig that renders eleven of the package's fourteen
views and found two real defects doing it. Three views were deferred because they
call `Html.BeginUmbracoForm` and nobody knew what that needed at render time. Those
three are the two flow views a visitor actually books through, plus the delegate
into one of them — the highest-traffic markup in the package is the part no test
renders.

The hosting question is now settled empirically rather than assumed. It needs no
Umbraco boot, no `WebApplicationFactory`, no SQL, and no content root: two DI
registrations over the existing rig, and all three views render in milliseconds.

The second reason is larger and is the one carrying real risk. Because the flow
views could not render, the suite's document-level rule is asked of a **hand-built
imitation** of a page — four partials concatenated in `ViewFixtures.BuildDocuments`
in what someone believed was flow order. Nothing checks that belief. It has already
drifted once, and the drift **masked a real defect**: composing `_YourDetails`
unconditionally made an error summary linking to booker fields resolve against a
document the real page never produces. A second drift is silent in exactly the same
way. Once the flow views render, the imitation is not improved — it is deleted, and
the rules are asked of the composition the site actually serves.

## What Changes

- The rig gains two DI registrations: `SurfaceControllerTypeCollection`, and an
  `IUmbracoContextAccessor` returning a stub `IUmbracoContext`. Measured, not
  guessed: `AddDataProtection()` and `AddAntiforgery()` are **not** needed —
  `AddMvcCore` already supplies both, and the views emit a real
  `__RequestVerificationToken` and a real `ufprt` without them.
- `Booking/Default.cshtml`, `BookingFlow/Service.cshtml` and
  `BookingFlow/Default.cshtml` leave the deferred set and enter the suite. All
  three existing rules apply to them with no rule changes.
- `ViewInventory.Deferred` is **emptied and removed**. Every view the package ships
  is exercised; there is no longer a set of views the suite is silent about.
- The hand-built composed-document fixture is **replaced by the real flow views**.
  Document-level checks are asked of what `Service.cshtml` and `Default.cshtml`
  render, not of a concatenation that imitates them.
- Two new requirements in `default-frontend` state these as guarantees rather than
  as properties of a test file: that no shipped view goes unexercised, and that a
  composed document is the one a flow view actually renders.

Expected: the newly honest document may fail the existing rules. That is the point
of the change, not a problem with it — any such failure is a real defect in shipped
markup, and the composition drift is known to have hidden one already.

## Non-goals

- **No rule that the POST form posts back to the current page.** The spike showed
  this is newly cheap: the stub's URL determines the form `action`, so ⑤ design
  D3's PRG guarantee is testable for the first time. Deliberately excluded, for two
  reasons. It is a **behaviour** rule in a suite whose stated remit is markup —
  the same line drawn when `role="alert"` was left unguarded rather than replaced
  by a markup rule wearing a behaviour rule's clothes. And the form action is about
  to change: editor-facing packaging replaces the surface-controller round trip with
  a `RenderController` owning GET+POST. Recorded as a removal with its reason, not
  dropped in silence.
- **No rule that the POST form carries anti-forgery.** Same boundary. Worth noting
  that this guarantee (`default-frontend`'s "Anti-forgery-protected submission")
  remains unguarded by any test; that is a pre-existing gap this change declines to
  close rather than one it introduces.
- **No change to any shipped view**, unless a rule fails on one — in which case the
  fix is the defect's, and is reported as a finding rather than folded in quietly.
- **No `WebApplicationFactory`, no TestSite reference, no SQL, no Umbraco boot.**
  The rig's central claim — that it renders the views compiled into
  `UBookIt.Web.dll`, with `RuntimeCompilation` absent from the closure — is
  preserved, and the existing test asserting it must keep passing.
- **Not editor-facing packaging.** That is the next change and is what makes these
  two registrations temporary.

## Capabilities

### New Capabilities

None. This change adds requirements to an existing capability rather than
introducing one.

### Modified Capabilities

- `default-frontend`: two requirements **added** — "Every view the package ships is
  exercised" and "A composed document is the composition a flow view renders". No
  existing requirement is replaced. This is deliberate: the two scenarios reading
  "every view **in scope**" (`spec.md:304,367`) are the clauses that have been
  carrying the deferral, and "in scope" is defined nowhere in the spec. The first
  new requirement defines that set as everything the package ships, which pins the
  phrase without replacing two sixty-line requirements wholesale to change two
  words — the failure mode CLAUDE.md and the ⑧a precedent both warn about.

## Impact

- `tests/UBookIt.Tests.Rendering/Support/ViewRenderer.cs` — two registrations, a
  stub `IUmbracoContext`, a stub `IUmbracoContextAccessor`.
- `tests/UBookIt.Tests.Rendering/Support/ViewInventory.cs` — `Deferred` removed.
- `tests/UBookIt.Tests.Rendering/Support/ViewFixtures.cs` — composed documents come
  from the flow views; the `BookingFormModel` states gain the coverage the
  `ServiceFormModel` states already have.
- `tests/UBookIt.Tests.Rendering/ViewInventoryTests.cs` — the test asserting *why*
  three views are deferred has nothing left to assert and is replaced by the
  completeness check.
- **New test-only dependency on `Umbraco.Cms.Web.Website` types** in the rendering
  suite (`SurfaceControllerTypeCollection`, `IUmbracoContext`). It arrives
  transitively via `UBookIt.Web` — no new package reference — but it is the first
  time this suite names an Umbraco type, and it must not drag in
  `RuntimeCompilation`.
- No change to `src/`, to any public API, to the delivery or management contracts,
  or to persistence. No breaking change. No schema change.
