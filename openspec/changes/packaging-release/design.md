## Context

Measured before designing, by packing all four projects with a shared version:

- **All four already pack cleanly.** `UBookIt.Core`, `UBookIt.Persistence`, `UBookIt.Web` and
  `UBookIt.Backoffice` each produce a `.nupkg`, and with a common `PackageVersion` the
  inter-package dependencies resolve to each other correctly.
- **The static assets are already in the right place.** `UBookIt.Web`'s package carries
  `staticwebassets/ubookit.css`; `UBookIt.Backoffice`'s carries the whole
  `App_Plugins/UBookItBackoffice` bundle plus `umbraco-package.json`. The Razor views are
  compiled into `UBookIt.Web.dll` — consistent with the precompiled-views behaviour recorded
  during the theming work.
- **The dependency graph is a diamond with a missing corner**: `Persistence → Core`,
  `Web → Core`, `Backoffice → Core + Persistence`, and **nothing references `Web`**. That is
  the whole reason `Web` is absent from what ships today.
- **The `umbraco-package.json` says `version: "0.0.0"`** — the number the backoffice Packages
  screen shows an editor.
- One oddity found while measuring: `UBookIt.Persistence`'s package contains
  `lib/net10.0/UBookIt.Persistence.runtimeconfig.json`. A library package has no business
  carrying one; probably a side effect of the EF Core Design reference. Investigate at apply.

## Goals / Non-Goals

**Goals:**

- A site installs one package and gets a working booking system.
- The package declares nothing it does not ship.
- It says who wrote it, what it is, and what you may do with it.
- The installation path is proved by walking it.

**Non-Goals:**

- Publishing anywhere, a CI pipeline, an automated installation regression test, multi-
  targeting, or any behaviour change.

## Decisions

### D1. Four libraries plus a `UBookIt` meta-package

**Decision:** each project packs as itself; a new `UBookIt` package contains no code and
depends on `UBookIt.Backoffice` and `UBookIt.Web`, which transitively bring `Core` and
`Persistence`. A site installs `UBookIt`.

**Why not one package containing all four assemblies:** the assemblies are already separate
and separately meaningful. The architecture's stated goal is that a headless consumer can
build a booking flow against the contracts without the backoffice — CLAUDE.md's front-end
contract — and merging everything into one artifact would make that impossible to express.
Merging also needs ILMerge or a hand-rolled `TargetsForTfmSpecificBuildOutput`, both of which
fight the SDK for no gain.

**Why a meta-package at all, rather than telling people to install two:** because *"install
the package"* is step 1 of the MVP, and because the failure mode of forgetting the second one
is **silent** — the backoffice works, the booking page renders nothing, and there is no error
anywhere. That is today's defect exactly, and shipping the same trap with instructions
attached is not a fix.

**The meta must not drift.** A fifth assembly added later and forgotten is the same silent
failure again, so a test asserts the meta depends on every packable project rather than
trusting whoever adds one to remember.

### D2. Metadata is set once, centrally, and version is one property

**Decision:** the shared metadata (authors, description, URLs, licence, tags, readme) lives in
`Directory.Build.props` guarded on `IsPackable`, with only `PackageId`/`Description`
per-project. Version is a single property every package takes.

**Why:** five packages whose versions can differ by accident is a support problem — an
inter-package dependency resolving to a mismatched version is the kind of thing that works on
the machine that built it. One property makes a mismatch impossible rather than unlikely.

### D3. The `umbraco-package.json` version tracks the package version

The Packages screen shows an editor whatever is in that file. `0.0.0` beside a working
package reads as broken, or as something nobody maintains. It is a build-time substitution
rather than a number to keep in step by hand — the same reasoning as D2.

### D4. The verification installs into a site created from scratch

**Decision:** the proof is: pack → local feed → `dotnet new umbraco` into a temporary
directory → `dotnet add package UBookIt` → run → install the site → create and publish a
booking page → take a booking → see it in the backoffice.

**Why from scratch rather than against `UBookIt.TestSite`:** the TestSite references the
projects, so it proves nothing about the packages — it is the configuration that has hidden
this failure since the beginning. A new site is the only place `UBookIt.Web`'s absence would
have shown.

**What this is and is not.** It proves the path once. It is not a regression guard, and the
change must not describe it as one — the obligation for an automated installation test is
already recorded and this does not discharge it. What the change *can* do is leave the steps
scripted, so repeating it is a command rather than an afternoon.

### D5. If packaging needs a source change, that is a finding

The expectation is that this change touches project files, metadata, two new root files and
one JSON version. If a package turns out to need code moved between assemblies, or a type made
public, that is a design problem this change has uncovered rather than a task to absorb
quietly — report it and decide, because it means the assembly boundaries do not survive being
shipped.

## Risks / Trade-offs

- **A manual proof of a failure that inspection missed.** → Scripted and recorded verbatim;
  the automated guard stays a named obligation rather than being implied.
- **Five packages to keep in step.** → One version property, shared metadata, and a test that
  the meta aggregates everything packable.
- **The licence is a one-way door.** → Relicensing after adoption needs every contributor's
  agreement. It is a decision for Chris, stated as blocking rather than defaulted.
- **A version number is a promise.** → `0.1.0` versus `1.0.0` is the difference between "this
  may still move" and CLAUDE.md's compatibility promise starting immediately; recorded as a
  decision rather than picked.

## Open Questions

- **Does the meta-package need to carry the `umbraco-package.json`?** The manifest currently
  ships with `UBookIt.Backoffice`, which the meta depends on, so it should arrive either way
  — but "should" is exactly the word that got `UBookIt.Web` left out. Settle it by looking at
  the installed site, not by reasoning.
- **`UBookIt.Persistence.runtimeconfig.json` in the package's `lib/`.** Harmless or not,
  something is producing it that should not be.
- **Whether the readme belongs in every package or only the meta.** NuGet shows the readme on
  each package's page; four copies of one readme is noise, and none at all makes the library
  pages blank. Probably: full readme on the meta, a short pointer on the libraries.
