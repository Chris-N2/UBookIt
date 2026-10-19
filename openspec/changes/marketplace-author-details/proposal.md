## Why

The Umbraco Marketplace listing gives almost nothing about who publishes uBookIt. Observed on
2026-10-19, the author card shows:
- the name **Norwood Design & Development Ltd.**, taken from the NuGet `<Authors>` field;
- a **generic placeholder avatar**;
- **no description and no website**;
- a link reading **"Explore more packages by CNorwood69"**. That is the NuGet username of the
  personal account that owned the packages, so the link comes from the package **owners**, not
  the authors.

Two separate levers cover this:
- **The owner link** follows NuGet ownership. The packages now have the nuget.org organisation as
  an owner, and the personal account will be removed after the next release has proven the
  organisation's publishing policy. This change cannot affect the link, and does not claim to.
- **The avatar, description and website** have one documented source: `umbraco-marketplace.json`, served
from the folder the package's project URL points to. Its `AuthorDetails` block carries a name, a
short description, a website and an image, and it takes effect at the Marketplace's next sync,
with **no package release**. The `marketplace-listing` change (2026-09-25) deferred this file as
"a separate and later piece of work". This is that work.

The package-ID rename (`NDD.Umbraco.UBookIt.*`) is related only by motive, and is **parked**. This
change does not depend on it, and nothing here needs revisiting if the rename goes ahead later.

## What Changes

- **A new `umbraco-marketplace.json` at the repository root on `main`.** The package's
  `PackageProjectUrl` is `https://github.com/Chris-N2/UBookIt`, so the Marketplace reads the file
  from the root of the default branch, `main`. The file contains:
  - `$schema`, the Marketplace's published schema;
  - `AuthorDetails`: `Name`, `Description`, `Url` and `ImageUrl`, describing Norwood Design &
    Development Ltd.
- **The file states no version-dependent fact.** One listing serves both lines (17.x and 18.x), and
  one file on `main` serves that listing. It therefore does **not** set `Title`, `Description` or
  anything else that would override the NuGet metadata. The listing's description stays the
  meta-package's `<Description>`, which names the Umbraco major derived from `<Version>` under an
  existing requirement. A JSON description written once on `main` would describe both lines in one
  sentence, which is the defect the 18.0.0/18.1.0 listings shipped.
- **A test** that parses the file and checks three things: the author name agrees with
  `<Authors>`, the fields above are present and well-formed, and the version-dependent overrides
  are absent.
- **`docs/publishing.md`, Marketplace section.** It now says the listing's author details come from
  this file, that the file is read from `main` at the Marketplace's sync and not from any package,
  and that a change to it therefore needs no release.
- **The logo image** is committed to the repository and referenced from `main` by its raw URL. Its
  final form is a design decision (see `design.md`).

## Non-goals

- **The package-ID rename.** It is parked and needs a separate decision.
- **Other listing fields**: category, tags, screenshots, documentation URL, related packages,
  contributors. They could be added later in the same file. This change is about who publishes the
  package.
- **A copy on `dev/v18`.** The Marketplace reads only the default branch, so a copy there would have
  no effect and would be free to drift. This is a deliberate exception to "change both lines in one
  sitting", and `design.md` records the reason.
- **Updating the Trusted Publishing documentation for the organisation owner.** The policy table
  (`docs/publishing.md:224`) and `NUGET_USER: CNorwood69` (`publish.yml:185`) still describe the
  personal owner. Whether `NUGET_USER` must change for an organisation-owned policy cannot be
  observed until a real release, so that correction belongs to the next release, which is the floor
  raise. It is recorded as a deferred obligation, not left silent.
- **Any package release.** Nothing packed changes.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `packaging`: **adds** one requirement. The Marketplace author details come from a root
  `umbraco-marketplace.json` that names the publisher and states no version-dependent fact. No
  existing requirement is modified or replaced.

## Impact

- **New files:** `umbraco-marketplace.json` (repo root, `main` only) and the logo image.
- **Tests:** one new test class in `tests/UBookIt.Tests`. No packed artifact changes, so the
  packaging tests are unaffected.
- **Docs:** `docs/publishing.md`, Marketplace section.
- **CI:** none. The parity check compares `.github/`, `global.json`, `scripts/ci/` and
  `Directory.Build.props`, so a root JSON file on `main` alone does not trip it.
- **Outside the repo:** the listing changes at the Marketplace's next sync, and is checked on
  `https://marketplace.umbraco.com/package/ubookit` afterwards. That check is not claimed before.
- **No public API, schema or migration change.**
