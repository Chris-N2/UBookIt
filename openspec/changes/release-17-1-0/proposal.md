## Why

`17.1.0` is the first uBookIt release a consumer has to *read* before taking. `17.0.0` was the
first release and `17.0.1` fixed nine broken README links — neither asked anything of anybody.
This one carries five features, three new settings and, between `move-booking` and
`self-service-cancellation`, **four additions to published interfaces, none with a default
implementation**. A site that implements any of those ports stops compiling on upgrade.

The package already promises to handle this. `README.md` says a breaking change "is called out
explicitly rather than left to be discovered", and `packaging`'s version requirement makes the
*policy* a SHALL. **Neither produces a place where an actual break is named.** The archived
proposals record all four precisely, but they are written for us, live under
`openspec/changes/archive/`, and are invisible from nuget.org. So today a consumer discovers the
compile error by hitting it — which is the exact outcome the README says will not happen.

The version bump itself is the smaller half, and it is already understood: it touches three files,
and a repo-wide find-and-replace of the number is forbidden because two of the sentences carrying
it are history.

## What Changes

- **The declared version becomes `17.1.0`**, and the two prose claims that state the version
  uBookIt is *currently at* follow it: `README.md`'s API-promise callout and `docs/publishing.md`'s
  version-reuse warning. The history anchors — "the first release is `17.0.0`", "`17.0.1` exists
  because of it" — **do not move**, and the suite fails if they do.
- **A `CHANGELOG.md` is introduced**, consumer-facing and per release. It names what a site must do
  to upgrade before it names what it gains. `17.1.0`'s entry states all four interface additions by
  member, and that an existing implementation of those ports will not compile until each is added.
  Earlier releases get short honest entries rather than a reconstruction.
- **The changelog is guarded, not merely written.** A new test asserts the declared version has a
  non-empty entry, so a future bump that forgets the changelog fails the suite exactly as a bump
  that forgets the README does today. Without this it is stale by `17.2.0`.
- **The README's API-promise callout gains the pointer.** The sentence that says breaks are called
  out explicitly should be next to the place they are called out.
- **BREAKING — nothing new breaks here.** This change declares no break of its own. It *publishes*
  four that were each declared, QA'd and approved in their own change, and that have been sitting
  unreleased: `IBookingObserver` gains a moved member; `IBookingStore` gains a move write;
  `IServiceBookingService` gains a move; `IBookingService` gains `CancelAsVisitorAsync`. All four
  land in a minor, which is where this project's policy puts a break.

## Capabilities

### New Capabilities

None. Releasing is not a new capability of the product; it is `packaging` doing its job.

### Modified Capabilities

- `packaging`: **one ADDED requirement** — a release that changes a published contract names that
  change where a consumer will meet it, and the claim is verified rather than asserted. The
  existing requirement *The version a reader is told is the version the package carries* states the
  breaking-change **policy**; nothing yet requires a release to say which breaks it actually
  contains, and that gap is what ships a compile error unannounced. Left deliberately as an
  addition rather than a rewrite of that requirement, because rewriting one replaces it wholesale
  and this needs nothing there dropped.

## Non-goals

- **Publishing itself.** This change makes the tree releasable; Chris runs `docs/publishing.md`.
  The runbook is a human procedure against a one-way door, and automating a step whose failure
  costs a version number is not a trade worth taking here.
- **A CI pipeline.** Still absent, still deliberate, still the largest known gap. Naming it here so
  it is not mistaken for an oversight discovered later.
- **Release tags.** `17.0.0` and `17.0.1` were never tagged and this change does not retrofit them.
  Worth doing for humans; not load-bearing for NuGet or SourceLink, and dating tags after the fact
  invents history. Recorded as deferred rather than done.
- **`<PackageReleaseNotes>` in the package metadata.** Tempting, and a trap: metadata is frozen per
  version, so notes pushed with a typo cannot be corrected, and notes duplicated from the changelog
  are a second source of truth that drifts. A link to the changelog is the safe shape, and it is
  deferred until the changelog has survived one release.
- **Rewriting `docs/publishing.md`'s Status section.** It records that uBookIt *reached* nuget.org
  on 2026-09-15 with `17.0.0` as the first release. That is history and stays true when `17.1.0`
  publishes. The six repair tasks `release-17-0-1` needed were the one-time transition from "nothing
  is pushed" to "something is pushed", and that transition has already happened.

## Impact

**Code:** none. No `src/` change, no schema change, no migration. That is the point of batching
five approved changes into a release rather than releasing each.

**Files:** `Directory.Build.props` (the declaration), `README.md`, `docs/publishing.md`, a new
`CHANGELOG.md`, a new guard in `tests/UBookIt.Tests/`, and the `packaging` delta.

**Consumers:** a site upgrading `17.0.1` → `17.1.0` that implements `IBookingObserver`,
`IBookingStore`, `IServiceBookingService` or `IBookingService` **will not compile** until it adds
the new members. Every other site upgrades without action; the new features are behind flags that
default off, and `self-service-cancellation` additionally requires booker emails to be on.

**Risk:** a spent version number is unrecoverable. `17.1.0` cannot be reused if it ships wrong, and
the packed README and metadata are frozen at push — which is why verification happens against the
produced `.nuspec` and the packed readme, never the working tree.
