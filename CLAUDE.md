# uBookIt

Open-source booking system package for Umbraco. There is no good open-source
booking system for Umbraco; this aims to be it.

## Invariants — these survive every context and every spec

1. **Umbraco LTS is what `main` targets.** `main` tracks the current LTS —
   Umbraco 17 now, Umbraco 21 from December 2027 — on the single .NET target
   that LTS supports. **An STS line may be supported on a `dev/vXX` branch and
   published as its own major** (`18.x` for Umbraco 18), but it is secondary:
   features land on `main` first, and an STS line is dropped when its Umbraco
   version reaches EOL rather than carried. Earlier majors stay out of scope:
   v13 and the v14–16 STS line will not be supported, and there is no
   multi-targeting for them.

   *The previous wording — "Umbraco 17+ (LTS) only" — was ambiguous about
   whether STS majors were excluded. They are not, and never were: supporting
   STS was always intended. What this settles is which line `main` follows, and
   the answer is the LTS, because 18 is STS and reaches EOL in June 2027,
   seventeen months before 17 does.*

2. **Zero DevExpress in this repository.** No `PackageReference`, no npm
   dependency, no CDN script tag, no copied source. The DevExpress EULA
   forbids redistribution, so any DevExpress presence makes the repo
   unbuildable for unlicensed users. A DevExpress-based front-end
   (`UBookIt.UI.DevExpress`) is a **separate package in a separate repo**
   that consumes only the public contracts defined here. QA review enforces
   this with a dependency scan; a violation is an automatic reject.

3. **Backoffice is native Umbraco 17.** Lit web components + the Umbraco UI
   Library (`uui-*`), TypeScript, Vite. No third-party widget frameworks in
   the backoffice, ever. The backoffice is for configuration and management
   of bookable resources, availability, and reservations — not for fancy
   rendering.

4. **The front-end contract is data, not widgets.** Alternative UI
   implementations plug in at the level of **API endpoints + strongly-typed
   view models**. Never introduce widget-level abstractions
   (`IScheduler`, `ICalendarControl`, etc.) — they become either
   lowest-common-denominator or a reimplementation of a vendor API. If a
   proposed abstraction wraps a UI component rather than describing data or
   behaviour, reject it at propose time.

5. **Default rendering is dependency-free and accessible — for the part we
   actually ship.** The shipped front-end (ViewComponents/Razor + minimal
   JS) uses semantic HTML and meets **every WCAG 2.2 AA criterion
   determined by markup**. Accessibility is a differentiator for this
   package, not a checkbox: booking UIs are notorious accessibility
   failures.

   The claim names its own boundary, because uBookIt is a component inside
   somebody else's page and WCAG conformance is a property of a *page*:

   - **Ours, always.** Labelling, grouping, programmatic relationships,
     keyboard operability, reading and focus order — and each flow stays
     usable with **no author stylesheet applied at all**.
   - **Ours by default, theirs on override.** Text contrast (1.4.3), focus
     appearance (2.4.11, 2.4.13) and target size (2.5.8) are decided by CSS.
     The shipped stylesheet meets them by setting **no text colour of its
     own** — every `color` resolves to `currentColor` or `inherit` — and by
     leaving focus to the browser; a site that overrides a colour token owns
     that contrast. Non-text contrast (1.4.11) is **not** claimed for the
     package's decorative borders, and that exemption is stated in the docs
     rather than implied.

     **"Names no literal colour" is not the same claim as "cannot fail
     contrast", and conflating them shipped a defect.** A hint derived as
     75% of the host's text colour named no hue and still rendered at 2.9:1
     on a host whose own text was exactly conformant. So: **a derived colour
     is for decoration, never for text.**
   - **Theirs entirely, when a theme replaces the markup.** A theme (an RCL
     supplying views at `Views/Shared/UBookIt/Themes/<theme>/…`) renders instead
     of the package's views. The first bullet's guarantees are guarantees about
     **the views the package ships**; where a theme supplies the view, the
     markup is the theme author's and so are they. The package **claims nothing
     in either direction** — it neither asserts a theme is accessible nor
     requires anything of one, and it does not let a statement written about its
     own views be silently inherited by markup it has never seen. Same reasoning
     as the CSS narrowing: we do not take responsibility for code we did not
     write. **The narrowing reaches themed views and nothing else** — for an
     unthemed site, and for every view a theme does not supply, the first bullet
     holds exactly as written, no-author-stylesheet clause included.

   - **Never ours.** Reflow, text spacing, bypass blocks, page title, page
     language, and the document's heading outline the flow's `<h2>` sits in.

   This is a narrowing, not a lowering — **we do not take responsibility for
   code we did not write.** It is honest rather than an escape hatch only
   because of the last clause of the first bullet: since the markup is
   operable with no CSS at all, no stylesheet a site ships can make a flow
   *inoperable*, only harder to read. Never trade that clause away to
   simplify the wording.

6. **OpenSpec drives all changes.** explore → propose → apply → qa-review.
   No code without an approved change. Never modify anything under
   `openspec/` archive. QA review (`qa-review` skill) always runs in a
   fresh context or subagent — the context that implemented a change never
   reviews it.

## Architecture shape (initial proposal — refine via OpenSpec, don't treat as spec)

```
src/
  UBookIt.Core/          Domain: resources, availability rules, slots,
                         bookings, services, abstractions. Keep Umbraco
                         types out of here where practical.
  UBookIt.Persistence/   EF Core entities, migrations, repositories.
  UBookIt.Backoffice/    Management API controllers + backoffice client
                         (TypeScript/Lit/Vite).
  UBookIt.Web/           Delivery API + default ViewComponent/Razor
                         rendering.
tests/
  UBookIt.Tests/         Unit + integration tests.
```

A headless consumer (React/Vue/mobile) must be able to build a full booking
flow against the delivery API without referencing `UBookIt.Web`.

## Conventions

- Nullable reference types enabled everywhere; warnings are errors in CI.
- Public API surface is a compatibility promise once published, and the surface
  is declared stable from `17.0.0` — so treat it as frozen from that version
  whether or not a package has reached a feed yet. Additive changes preferred;
  a breaking change must be called out explicitly in its spec **and lands in a
  minor release (`17.x.0`), never a patch**, carrying defaults or a documented
  upgrade path so an existing site keeps working. The major is spent on the
  Umbraco major, so it cannot signal a break — the minor does. Parallel API
  versions are deliberately not used while the product is young.
- EF Core migrations are additive. Destructive schema changes require
  explicit spec approval and an upgrade path from the previous package
  version.
- Management API endpoints require backoffice authorization policies.
  Delivery endpoints validate all input; booking form submissions use
  anti-forgery protection.
- No secrets, connection strings, or personal data in logs or test
  fixtures.

## Workflow

OpenSpec is installed via `@fission-ai/openspec` (CLI: `openspec`); skills
live in `.claude/skills/`, slash commands under `/opsx:*`.

1. `openspec-explore` (`/opsx:explore`) — investigate, gather context, no
   decisions.
2. `openspec-propose` (`/opsx:propose`) — write the change spec; get human
   approval.
3. `openspec-apply-change` (`/opsx:apply`) — implement against the
   approved spec.
4. `qa-review` — **new session or subagent**, adversarial review against
   the spec. Findings go back through apply; QA never fixes code itself.
   See *How QA actually runs* below.
5. `openspec-archive-change` (`/opsx:archive`) — after QA approval,
   archive the change and sync main specs.

### How QA actually runs

**One subagent per change, reused across that change's rounds.** Spawn it for round 1
and send every later round back to the *same* agent, so it still holds what it
already found, what it accepted, and what it told you to fix. A fresh agent per
round re-derives the change from nothing and re-litigates settled findings.

The reuse is **per change and within a session**. Subagents do not survive a session
ending, so a change picked up in a new session gets a new reviewer — which is a cost,
not a feature, and a reason to finish a change's QA rounds in one sitting where you
can.

**A REJECT is a gate.** Findings go back through apply; QA never edits code. Only QA
may decide that further rounds are unwarranted.

**Treat each round's fixes as new code, not as corrections.** On this project a fix
has caused the next round's defect three changes running — and in `approval-decline`
the fix for a CRITICAL shipped a guard that could not see the thing it guarded. Tell
the reviewer that explicitly when you hand a round back.

**Report to the reviewer what you claim, and ask it to verify rather than trust.**
Build state, test counts, live checks. Twice now the reviewer has found a claim in
the handover itself to be false — including one it had made in its own previous
round, and corrected against itself.

### Rewriting a requirement destroys guarantees silently

A `## MODIFIED Requirements` entry **replaces its requirement wholesale** —
body and every scenario. Anything the old version guaranteed and the new one
forgets to restate is deleted from the spec, with nothing in the diff that
looks like a deletion. A grep for stale names cannot see it, because the
sentence is inside a requirement you are legitimately replacing.

So whenever a delta modifies a requirement, **diff the guarantees, not the
prose**:

1. List every scenario and every SHALL in the requirement as it stands in
   `openspec/specs/`.
2. For each, decide explicitly: carried forward, deliberately dropped, or
   superseded by a stronger claim. Carried-forward guarantees need a scenario
   in the new version — the same behaviour, reworded for the new shape, not
   assumed to survive.
3. A deliberate drop belongs in the proposal, stated as a removal with its
   reason. Silence is not a decision.

This is distinct from the sync-time grep for *sibling* specs the change
falsifies (which has found something on four consecutive changes and is still
worth doing). That grep looks outward at requirements you are not touching;
this looks inward at the one you are replacing.

Real case, ⑧a: change ⑧ guaranteed the readout describe the **type** when a
role required no capabilities, so it could never refer to capabilities the
editor had not named. ⑧a replaced that requirement with a three-stage chain,
did not restate the guarantee, and shipped a summary asserting "N of those have
the required capabilities" for a configuration requiring none. QA caught it;
the change's own falsified-sentence grep did not, and could not.
