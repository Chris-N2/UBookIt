## Why

Steps 1 and 9 of `docs/mvp.md`, and the last of them. Everything a booking system needs is
built and merged; **nobody can install it.**

`dotnet pack` succeeds today and produces a package that cannot be consumed:

- `UBookIt.Backoffice.nupkg` declares dependencies on **`UBookIt.Core` and
  `UBookIt.Persistence`** — packages that do not exist and are not produced. Any
  `dotnet add package` fails with NU1101.
- **`UBookIt.Web` is not packed at all.** Nothing references it, so the Razor booking page,
  the theming mechanism and `ubookit.css` — most of what a visitor ever sees — ship to
  nobody.
- Metadata is SDK defaults: `authors: UBookIt.Backoffice`, `description: Package
  Description`, version `1.0.0` from nowhere, no licence, no project URL, no readme.
- **The repository has no `LICENSE` and no `README`.** For a package whose stated purpose is
  to be open source, and which installs schema into other people's databases, that is not a
  formality.

A fourth, **found during apply and not by the measurement above**:

- **The backoffice client was built by nothing.** `wwwroot/App_Plugins/` is gitignored and no
  MSBuild target produced it, so packing a clean clone produced a `UBookIt.Backoffice` package
  with **no client in it** and said "Successfully created package". A site installing that
  gets the Management API and no Bookings section.

  It was missed at propose time for an instructive reason: the measurement was taken against a
  working copy where an earlier manual `npm run build` had already populated the directory. The
  artifact looked complete because a previous local step had quietly completed it — which is
  the same failure this whole change is about, one level up.

Every one of those was invisible to a build reporting success, which decides how this change
has to be verified: **by installing the built packages into a clean Umbraco site and taking a
booking**, not by inspecting a `.nupkg`.

## What Changes

- **Five packages: four libraries and a meta-package.** `UBookIt.Core`,
  `UBookIt.Persistence`, `UBookIt.Web` and `UBookIt.Backoffice` each pack — measured, all
  four already do — and a new `UBookIt` package depends on `Backoffice` and `Web` so a site
  installs **one** thing.
- **Real package metadata**, set once for all of them: authors, description, project and
  repository URLs, licence expression, readme, tags, and one version property.
- **A `LICENSE` file** and a **`README.md`** that is both the repository's front page and the
  package's NuGet readme.
- **The `umbraco-package.json` version stops saying `0.0.0`**, which is what the backoffice
  Packages screen shows an editor.
- **An installation verification, performed rather than asserted**: build the packages, add
  them to a local feed, install into a newly created Umbraco site, run it, publish a booking
  page, take a booking, and see it in the backoffice. Scripted so it is repeatable.

## Non-goals

- **Publishing to nuget.org.** This change makes the packages installable and proves it from
  a local feed. Pushing them is a decision about a public identity and a release moment,
  which is Chris's rather than a task in a change.
- **A CI pipeline.** The repo has none (memory says Azure DevOps); adding one is its own
  piece of work and does not gate installability.
- **An automated installation regression test.** The obligation is already recorded from
  `booking-page-packaging` and unchanged by this: the guard needs an Umbraco boot and a
  database, and it is a bigger piece than this change. **This change proves the path once and
  records precisely what was proved** — which is what the earlier packaging change did, and
  is honest so long as nobody later mistakes it for a guard.
- **Multi-targeting, or supporting Umbraco 18.** CLAUDE.md invariant 1 fixes this at
  Umbraco 17 LTS.
- **Any behaviour change.** If packaging requires a code change beyond project files and
  metadata, that is a finding to report.

## Capabilities

### Modified Capabilities

- `packaging`: gains what the package **is** as a distributable artifact — which assemblies a
  consumer receives, that installing one thing is enough, that it declares no dependency it
  does not ship, and that it carries a licence and a readme. The capability today describes
  what the package *does* on install; it says nothing about the package existing.

## Impact

- **Project files only** for the libraries: metadata properties, `IsPackable`, and a new
  `UBookIt` meta-package project. No source change is expected — and if one turns out to be
  needed, that is a finding rather than a quiet edit.
- **New at the repository root**: `LICENSE`, `README.md`.
- **`umbraco-package.json`** gains the real version.
- **Public API**: unchanged. Packaging publishes the surface; it does not alter it. **But
  publishing is the moment CLAUDE.md's compatibility promise starts**, which is why the
  version below is a real decision rather than a formality.

## Decisions needed from Chris before apply — all three settled 2026-08-31

**1. MIT. 2. `0.1.0`. 3. `UBookIt` as the meta-package**, as recommended below. Chris also
raised planning the 0.2.0 → 1.0.0 road so that 1.0 means "a fully functional product", and
deferred it — recorded as an obligation rather than answered here.

The reasoning as it was put to him:

1. **Licence.** *Recommendation: MIT.* It is the most permissive common choice, it is what
   most Umbraco community packages use, and it maximises the chance a site will adopt this
   without a legal conversation. Apache-2.0 is the alternative if an explicit patent grant
   matters. This is a company product, so it is Chris's call, and it is **blocking**: it goes
   in the repository, in every nuspec, and in the readme.

2. **First version.** *Recommendation: `0.1.0`.* CLAUDE.md says the public API is a
   compatibility promise **once published** — so `1.0.0` makes that promise the moment this
   ships, and the API has taken three deliberate breaking changes in the last three changes
   (`Booking.Rehydrate`, `BookingSummary`, `IBookingService`). `0.1.0` says *usable, not
   frozen*, and `1.0.0` remains available the moment the shape stops moving. If Chris would
   rather signal confidence than caution, `1.0.0` is defensible — but then the promise starts
   now and should be stated in the readme.

3. **Package identity.** `UBookIt` as the meta-package a site installs, with the four
   libraries alongside it. The alternative is no meta-package and a site installing
   `UBookIt.Backoffice` **and** `UBookIt.Web` — which is one command more and one thing to
   get wrong, since nothing references `Web` and its absence is silent until a booking page
   renders nothing.

## Risks

**The verification is manual, and this change is specifically about a failure that inspection
missed.** Doing it once proves the path; it does not keep it proved. The mitigation is to
script the steps and record exactly what was run and what was observed, so the next person
repeats it rather than reconstructs it — and to leave the automated guard as the recorded
obligation it already is, rather than pretending a one-off is a test.

**A meta-package can drift from what it aggregates.** If a fifth assembly is added later and
the meta is not updated, a site installing `UBookIt` silently misses it — which is exactly
today's `UBookIt.Web` failure wearing a different hat. Worth a guard that the meta depends on
every packable project.
